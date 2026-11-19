using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.IO;
using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Models.Api;
using IsThereAnyDeal.Models.ApiWebsite;
using IsThereAnyDeal.Views;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static CommonPluginsShared.PlayniteTools;

namespace IsThereAnyDeal.Services
{
    /// <summary>
    /// Core service class for interacting with the IsThereAnyDeal (ITAD) API and managing multi-store wishlists.
    /// Handles data retrieval for prices, giveaways, and shop configurations.
    /// </summary>
    public class IsThereAnyDealApi
    {

        private static ILogger Logger => LogManager.GetLogger();

        #region Urls

        private static string BaseUrl => @"https://isthereanydeal.com";
        private static string GiveawaysUrl => BaseUrl + @"/giveaways/api/list/";
        private static string ApiUrl => @"https://api.isthereanydeal.com";
        private static string ApiLookupTitles => ApiUrl + @"/lookup/id/title/v1";
        private static string ApiLookupAppIds => ApiUrl + @"/lookup/id/shop/{0}/v1";

        #endregion

        /// <summary>
        /// API Key for ITAD services.
        /// REMARK: Should ideally be moved to a secure configuration or encrypted store instead of being hardcoded.
        /// </summary>
        private static string Key => "fa49308286edcaf76fea58926fd2ea2d216a17ff";

        private static string _sessionToken;
        private static string SessionToken
        {
            get
            {
                if (_sessionToken == null)
                {
					_sessionToken = GetSessionToken();
                }
                return _sessionToken;

			}
            set { _sessionToken = value; }
		}

        /// <summary>
        /// Tracks the number of items fetched per store during the current session.
        /// </summary>
        public List<CountData> CountDatas { get; set; } = new List<CountData>();

		private static string CachePath => Path.Combine(IsThereAnyDeal.PluginUserDataPath, IsThereAnyDeal.PluginName, "ITAD");
		private static FileDataService FileDataService => new FileDataService(IsThereAnyDeal.PluginName, "ITAD");

		/// <summary>
		/// Process-wide ITAD API gate: all <see cref="IsThereAnyDealApi"/> instances share one limiter
		/// so parallel store fetches cannot burst concurrent HTTP calls to ITAD.
		/// </summary>
		private static readonly SemaphoreSlim RateLimiter = new SemaphoreSlim(1, 1);
		private static DateTime _lastApiCall = DateTime.MinValue;
		private const int MinApiCallIntervalMs = 500;

		private static readonly Dictionary<string, string> CurrencySymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
	        { "eur", "\u20AC" },
	        { "usd", "$" },
	        { "gbp", "\u00A3" },
	        { "aud", "$" },
	        { "brl", "R$" },
	        { "cad", "$" },
	        { "cny", "\u00A5" }
        };


		#region Api

		/// <summary>
		/// Fetches the list of available shops/services supported by ITAD for a specific country.
		/// </summary>
		/// <param name="country">ISO Country Code (Alpha-2).</param>
		/// <returns>A list of <see cref="ServiceShop"/> or null on failure.</returns>
		public async Task<List<ServiceShop>> GetServiceShops(string country)
        {
            return await WithRateLimitAsync(async () =>
            {
                try
                {
                    string url = ApiUrl + $"/service/shops/v1?country={country}";
                    string data = await Web.DownloadStringData(url).ConfigureAwait(false);

                    _ = Serialization.TryFromJson(data, out List<ServiceShop> serviceShops, out Exception ex);
                    return ex != null ? throw ex : serviceShops;
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error in GetServiceShops({country})", true, IsThereAnyDeal.PluginName);
                }

                return null;
            });
        }


        /// <summary>
        /// Looks up a game's ITAD ID using its title.
        /// </summary>
        /// <param name="title">Game title.</param>
        public async Task<GameLookup> GetGamesLookup(string title)
        {
            return await GetGamesLookup(title, 0);
        }

        /// <summary>
        /// Looks up a game's ITAD ID using its Steam AppID.
        /// </summary>
        /// <param name="appId">Steam Application ID.</param>
        public async Task<GameLookup> GetGamesLookup(uint appId)
        {
            return await GetGamesLookup(string.Empty, appId);
        }

        /// <summary>
        /// Internal method to query the lookup API by title or AppID.
        /// </summary>
        private async Task<GameLookup> GetGamesLookup(string title, uint appId)
        {
			string cachePath = Path.Combine(CachePath, $"{(title.IsNullOrEmpty() ? appId.ToString() : title)}.json");
			var data = await FileDataService.LoadDataAsync<GameLookup>(cachePath, 4320);

			if (data == null)
            {
                return await WithRateLimitAsync(async () =>
                {
                    try
                    {
                        if (!title.IsNullOrEmpty() || appId > 0)
                        {
                            // REMARK: PlayniteTools.RemoveGameEdition is used to increase match probability by stripping suffixes like "GOTY".
                            string url = ApiUrl + $"/games/lookup/v1?key={Key}&"
                                + (!title.IsNullOrEmpty()
                                        ? $"title={WebUtility.UrlEncode(WebUtility.HtmlDecode(RemoveGameEdition(title)))}"
                                        : $"appid={appId}");
                            string response = await Web.DownloadStringData(url);

                            _ = Serialization.TryFromJson(response, out data, out Exception ex);
                            if (ex != null)
                            {
                                throw ex;
                            }

                            FileDataService.SaveData(cachePath, data);
                            return data;
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, $"Error in GetGamesLookup({(title.IsNullOrEmpty() ? appId.ToString() : title)})", true, IsThereAnyDeal.PluginName);
                    }

					return null;
				});
			}

            return data;
        }

        /// <summary>
        /// Batch retrieves ITAD internal IDs for a list of titles or Steam IDs.
        /// </summary>
        public async Task<Dictionary<string, string>> GetGamesId(List<string> titles, List<string> appIds)
        {
            return await WithRateLimitAsync(async () =>
            {
                string url = string.Empty;
                string payload = string.Empty;

                try
                {
                    if (titles?.Count() > 0)
                    {
                        url = ApiLookupTitles;
                        payload = Serialization.ToJson(titles.Select(x => PlayniteTools.RemoveGameEdition(x)));
                    }

                    if (appIds?.Count() > 0)
                    {
                        // REMARK: Shop ID 61 refers specifically to Steam in ITAD's database structure.
                        url = string.Format(ApiLookupAppIds, 61);
                        payload = Serialization.ToJson(appIds.Select(x => x.Contains("app/") ? x : "app/" + x));
                    }

                    string data = await Web.PostStringDataPayload(url, payload);
                    _ = Serialization.TryFromJson(data, out Dictionary<string, string> gamesId, out Exception ex);
                    return ex != null ? throw ex : gamesId;
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error in GetGamesId({payload})", true, IsThereAnyDeal.PluginName);
                }

                return null;
            });
        }

        /// <summary>
        /// Fetches the latest price deals for a list of games from specific shops.
        /// </summary>
        /// <param name="country">Alpha-2 country code.</param>
        /// <param name="shopsId">List of shop IDs to check.</param>
        /// <param name="gamesId">List of ITAD internal game IDs.</param>
        public async Task<List<GamePrices>> GetGamesPrices(string country, List<int> shopsId, List<string> gamesId)
        {
            return await WithRateLimitAsync(async () =>
            {
                try
                {
                    string shops = string.Join(",", shopsId);
                    string url = ApiUrl + $"/games/prices/v3?key={Key}&vouchers=true&capacity=0&country={country}&shops={shops}";
                    string payload = Serialization.ToJson(gamesId);
                    string data = await Web.PostStringDataPayload(url, payload);

                    _ = Serialization.TryFromJson(data, out List<GamePrices> gamesPrices, out Exception ex);
                    return ex != null ? throw ex : gamesPrices;
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error in GetGamesPrices({country} / {string.Join(",", shopsId)} / {string.Join(",", gamesId)})", true, IsThereAnyDeal.PluginName);
                }

                return null;
            });
        }

        #endregion

        /// <summary>
        /// Loads the list of supported countries from the local JSON data file.
        /// </summary>
        public static List<Country> GetCountries()
        {
            return FileDataService.LoadData<List<Country>>(Path.Combine(IsThereAnyDeal.PluginFolder, "Data", "countries.json"), -1);
        }

        #region Plugin

        /// <summary>
        /// Loads wishlists from enabled stores, optionally enriching ITAD ids/prices.
        /// </summary>
        /// <param name="progress">Optional phase codes: cache, stores, lookup, price, save.</param>
        public List<Wishlist> LoadWishlist(IsThereAnyDeal plugin, bool cacheOnly = false, bool forcePrice = false, Action<string> progress = null)
        {
            List<Wishlist> combinedWishlist = new List<Wishlist>();
            CountDatas = new List<CountData>();

            // cache-only open → cache; price-only → lookup; store refresh → stores
            string initialPhase = cacheOnly && !forcePrice ? "cache" : (cacheOnly && forcePrice ? "lookup" : "stores");
            ReportWishlistProgress(progress, initialPhase);

            // 1. Define the configurations for all supported stores
            var storeConfigs = new List<StoreProviderConfig>
            {
                // --- Steam ---
                new StoreProviderConfig
                {
                    StoreName = "Steam",
                    IsEnabled = () => plugin.PluginSettingsViewModel.Settings.EnableSteam,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.SteamLibrary },
                    NotificationId = "IsThereAnyDeal-Steam-disabled",
                    LocErrorKey = "LOCItadNotificationErrorSteam",
                    ProviderFactory = () => new SteamWishlist(plugin)
                },

                // --- GOG ---
                new StoreProviderConfig
                {
                    StoreName = "GOG",
                    IsEnabled = () => plugin.PluginSettingsViewModel.Settings.EnableGog,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.GogLibrary, ExternalPlugin.GogOssLibrary },
                    NotificationId = "IsThereAnyDeal-GOG-disabled",
                    LocErrorKey = "LOCItadNotificationErrorGog",
                    ProviderFactory = () => new GogWishlist(plugin)
                },

                // --- Epic Games Store ---
                new StoreProviderConfig
                {
                    StoreName = "Epic Games Store",
                    IsEnabled = () => plugin.PluginSettingsViewModel.Settings.EnableEpic,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.EpicLibrary, ExternalPlugin.LegendaryLibrary },
                    NotificationId = "IsThereAnyDeal-EpicGameStore-disabled",
                    LocErrorKey = "LOCItadNotificationErrorEpic",
                    ProviderFactory = () => new EpicWishlist(plugin)
                },

                // --- Humble Bundle ---
                new StoreProviderConfig
                {
                    StoreName = "Humble Bundle",
                    IsEnabled = () => plugin.PluginSettingsViewModel.Settings.EnableHumble,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.HumbleLibrary },
                    NotificationId = "IsThereAnyDeal-HumbleBundle-disabled",
                    LocErrorKey = "LOCItadNotificationErrorHumble",
                    ProviderFactory = () => new HumbleBundleWishlist(plugin)
                },

                // --- Xbox ---
                new StoreProviderConfig
                {
                    StoreName = "Xbox",
                    IsEnabled = () => plugin.PluginSettingsViewModel.Settings.EnableXbox,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.XboxLibrary },
                    NotificationId = "IsThereAnyDeal-Xbox-disabled",
                    LocErrorKey = "LOCItadNotificationErrorXbox",
                    ProviderFactory = () => new XboxWishlist(plugin)
                },

                // --- Ubisoft Connect ---
                new StoreProviderConfig
                {
                    StoreName = "Ubisoft Connect",
                    IsEnabled = () => plugin.PluginSettingsViewModel.Settings.EnableUbisoft,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.UplayLibrary },
                    NotificationId = "IsThereAnyDeal-Ubisoft-disabled",
                    LocErrorKey = "LOCItadNotificationErrorUbisoft",
                    ProviderFactory = () => new UbisoftWishlist(plugin)
                },

                // --- EA (Origin) ---
                new StoreProviderConfig
                {
                    StoreName = "EA",
                    IsEnabled = () => plugin.PluginSettingsViewModel.Settings.EnableOrigin,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.OriginLibrary },
                    NotificationId = "IsThereAnyDeal-EA-disabled",
                    LocErrorKey = "LOCItadNotificationErrorEa",
                    ProviderFactory = () => new EaWishlist(plugin)
                }
            };

            // 2. Process each store generically (fetch only ? ITAD enrichment is centralised below)
            var lockObj = new object();
            var storeLoads = new ConcurrentBag<(GenericWishlist Provider, List<Wishlist> Items)>();
            var partitioner = Partitioner.Create(storeConfigs, EnumerablePartitionerOptions.NoBuffering);

			Parallel.ForEach(
				partitioner,
				new ParallelOptions { MaxDegreeOfParallelism = 4 },
				config =>
				{
					if (!config.IsEnabled()) return;

					try
					{
						// Check if at least one required library plugin is enabled in Playnite
						bool isPluginActive = config.RequiredPlugins.Any(p => IsEnabledPlaynitePlugin(GetPluginId(p)));

						if (isPluginActive)
						{
							var provider = config.ProviderFactory();
							var storeWishlist = provider.GetWishlist(cacheOnly, forcePrice) ?? new List<Wishlist>();

							lock (lockObj)
							{
								combinedWishlist.AddRange(storeWishlist);
								storeLoads.Add((provider, storeWishlist));
								CountDatas.Add(new CountData { StoreName = config.StoreName, Count = storeWishlist.Count });
							}
						}
						else
						{
							Logger.Warn($"{config.StoreName} is enabled in ITAD but the library plugin is disabled in Playnite.");
							API.Instance.Notifications.Add(new NotificationMessage(
								config.NotificationId,
								"IsThereAnyDeal\r\n" + ResourceProvider.GetString(config.LocErrorKey),
								NotificationType.Error,
								() => plugin.OpenSettingsView()
							));
						}
					}
					catch (Exception ex)
					{
						Common.LogError(ex, false, $"Error on ListWishlist for {config.StoreName}", true, IsThereAnyDeal.PluginName);
					}
				}
			);

			// 3. Central ITAD pipeline: batch game ids, one price pass, then persist store caches
			if (!cacheOnly || forcePrice)
			{
				Logger.Info($"[ITAD][pipeline] start items={combinedWishlist.Count} cacheOnly={cacheOnly} forcePrice={forcePrice}");
				ReportWishlistProgress(progress, "lookup");
				EnrichWishlistGameIds(combinedWishlist);
				Logger.Info($"[ITAD][price] pass force={forcePrice} items={combinedWishlist.Count}");
				ReportWishlistProgress(progress, "price");
				_ = GetCurrentPrice(combinedWishlist, plugin.PluginSettingsViewModel.Settings, forcePrice)
					.GetAwaiter()
					.GetResult();

				ReportWishlistProgress(progress, "save");
				foreach (var load in storeLoads)
				{
					try
					{
						load.Provider.SaveWishlist(load.Items);
					}
					catch (Exception ex)
					{
						Common.LogError(ex, false, $"[ITAD][pipeline] saveCache failed client={load.Provider.ClientName}", true, IsThereAnyDeal.PluginName);
					}
				}

				plugin.PluginSettingsViewModel.Settings.LastRefresh = DateTime.Now.ToUniversalTime();
				plugin.SavePluginSettings(plugin.PluginSettingsViewModel.Settings);
				Logger.Info($"[ITAD][pipeline] done savedStores={storeLoads.Count}");
			}

			// 4. Deduplication Logic (Normalized by Name)
			var finalData = DeduplicateWishlist(combinedWishlist);

            return finalData.OrderBy(x => x.Name).ToList();
        }

		private static void ReportWishlistProgress(Action<string> progress, string phase)
		{
			if (progress == null || string.IsNullOrEmpty(phase))
			{
				return;
			}

			try
			{
				progress(phase);
			}
			catch (Exception ex)
			{
				Common.LogError(ex, false, $"[ITAD][pipeline] progress callback failed phase={phase}", true, IsThereAnyDeal.PluginName);
			}
		}

		/// <summary>
		/// Resolves missing ITAD game ids: batch <see cref="GetGamesId"/> (Steam AppIDs vs titles), then 1x1 <see cref="GetGamesLookup"/> fallback.
		/// </summary>
		private void EnrichWishlistGameIds(List<Wishlist> wishlists)
		{
			if (wishlists == null || wishlists.Count == 0)
			{
				return;
			}

			var missing = wishlists
				.Where(x => string.IsNullOrEmpty(x?.Game?.Id))
				.ToList();

			if (missing.Count == 0)
			{
				Logger.Debug("[ITAD][lookup] nothingToResolve");
				return;
			}

			var steamItems = missing
				.Where(x => x.StoreName.IsEqual("Steam") && uint.TryParse(x.StoreId, out _))
				.ToList();
			var titleItems = missing.Except(steamItems).ToList();

			var steamAppIds = steamItems
				.Select(x => x.StoreId)
				.Where(id => !string.IsNullOrEmpty(id))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			var titles = titleItems
				.Select(x => x.Name)
				.Where(name => !string.IsNullOrEmpty(name))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			Logger.Info($"[ITAD][lookup] batch missing={missing.Count} steamAppIds={steamAppIds.Count} titles={titles.Count}");

			var steamMap = GetGamesIdBatched(null, steamAppIds);
			foreach (var item in steamItems)
			{
				if (TryGetBatchedId(steamMap, item.StoreId, out var gameId)
					|| TryGetBatchedId(steamMap, "app/" + item.StoreId, out gameId))
				{
					ApplyBatchedGameId(item, gameId);
				}
			}

			var titleMap = GetGamesIdBatched(titles, null);
			foreach (var item in titleItems)
			{
				if (TryGetBatchedId(titleMap, item.Name, out var gameId)
					|| TryGetBatchedId(titleMap, RemoveGameEdition(item.Name), out gameId))
				{
					ApplyBatchedGameId(item, gameId);
				}
			}

			var unresolvedAfterBatch = wishlists.Count(x => string.IsNullOrEmpty(x?.Game?.Id));
			Logger.Debug($"[ITAD][lookup] afterBatch unresolved={unresolvedAfterBatch} steamHits={steamMap.Count} titleHits={titleMap.Count}");

			var stillMissing = wishlists
				.Where(x => string.IsNullOrEmpty(x?.Game?.Id))
				.ToList();

			if (stillMissing.Count == 0)
			{
				return;
			}

			Logger.Warn($"[ITAD][lookup] fallback1x1 count={stillMissing.Count}");
			foreach (var item in stillMissing)
			{
				try
				{
					GameLookup lookup;
					if (item.StoreName.IsEqual("Steam") && uint.TryParse(item.StoreId, out uint appId) && appId > 0)
					{
						lookup = GetGamesLookup(appId).GetAwaiter().GetResult();
					}
					else if (!string.IsNullOrEmpty(item.Name))
					{
						lookup = GetGamesLookup(item.Name).GetAwaiter().GetResult();
					}
					else
					{
						continue;
					}

					if (lookup?.Found == true && lookup.Game != null)
					{
						item.Game = lookup.Game;
						if (item.StoreName.IsEqual("Steam")
							&& item.Game.Title != null
							&& item.Name.IsEqual($"SteamApp? - {item.StoreId}"))
						{
							item.Name = item.Game.Title;
						}
					}
				}
				catch (Exception ex)
				{
					Common.LogError(ex, false, $"[ITAD][lookup] fallback failed store={item.StoreName} storeId={item.StoreId} name={item.Name}", true, IsThereAnyDeal.PluginName);
				}
			}
		}

		private Dictionary<string, string> GetGamesIdBatched(List<string> titles, List<string> appIds)
		{
			var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			const int chunkSize = 200;

			var source = titles?.Count > 0 ? titles : appIds;
			if (source == null || source.Count == 0)
			{
				return result;
			}

			var useTitles = titles?.Count > 0;
			var chunks = source
				.Select((item, index) => new { item, index })
				.GroupBy(x => x.index / chunkSize)
				.Select(g => g.Select(x => x.item).ToList())
				.ToList();

			foreach (var chunk in chunks)
			{
				try
				{
					var ids = GetGamesId(
						useTitles ? chunk : null,
						useTitles ? null : chunk
					).GetAwaiter().GetResult();

					if (ids == null || ids.Count == 0)
					{
						continue;
					}

					foreach (var kvp in ids)
					{
						if (!string.IsNullOrEmpty(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
						{
							result[kvp.Key] = kvp.Value;
						}
					}
				}
				catch (Exception ex)
				{
					Common.LogError(ex, false, "[ITAD][lookup] GetGamesIdBatched failed", true, IsThereAnyDeal.PluginName);
				}
			}

			return result;
		}

		private static bool TryGetBatchedId(Dictionary<string, string> map, string key, out string gameId)
		{
			gameId = null;
			if (map == null || string.IsNullOrEmpty(key))
			{
				return false;
			}

			return map.TryGetValue(key, out gameId) && !string.IsNullOrEmpty(gameId);
		}

		private static void ApplyBatchedGameId(Wishlist item, string gameId)
		{
			if (item == null || string.IsNullOrEmpty(gameId))
			{
				return;
			}

			item.Game = new Models.Api.Game
			{
				Id = gameId,
				Title = item.Name,
				Slug = item.Game?.Slug
			};
		}

        /// <summary>
        /// Groups duplicate games across different storefronts into a single entry with a list of duplicates.
        /// </summary>
        private List<Wishlist> DeduplicateWishlist(List<Wishlist> fullList)
        {
            var grouped = fullList
                .GroupBy(c => PlayniteTools.NormalizeGameName(c.Name).ToLower())
                .ToList();

            List<Wishlist> result = new List<Wishlist>();

            foreach (var group in grouped)
            {
                var primary = group.First();
                var duplicates = group.Skip(1).ToList();

                if (duplicates.Any())
                {
                    primary.Duplicates = duplicates;
                    primary.hasDuplicates = true;
                }
                result.Add(primary);
            }

            return result;
        }


        /// <summary>
        /// Retrieves the list of shops for a country and converts them to the internal <see cref="ItadShops"/> model.
        /// </summary>
        public async Task<List<ItadShops>> GetShops(string country)
        {
            List<ServiceShop> serviceShops = await GetServiceShops(country);
            List<ItadShops> itadShops = new List<ItadShops>();
            itadShops = serviceShops?.Select(x => new ItadShops
            {
                Id = x.Id.ToString(),
                Title = x.Title,
                IsCheck = false,
                Color = string.Empty
            }).ToList();

            return itadShops;
        }

        /// <summary>
        /// Fetches the latest prices for games in the provided wishlist.
        /// </summary>
        /// <remarks>
        /// Games are processed in chunks of 200 to comply with potential URI length limitations or API batching requirements.
        /// </remarks>
        public async Task<List<Wishlist>> GetCurrentPrice(List<Wishlist> wishlists, IsThereAnyDealSettings settings, bool force)
        {
            try
            {
				var wishlistsData = (force
			        ? wishlists.Where(x => !x.Game?.Id?.IsNullOrEmpty() ?? false)
			        : wishlists.Where(x => (!x.ItadGameInfos?.Keys?.Contains(DateTime.Now.ToString("yyyy-MM-dd")) ?? true)
				        && (!x.Game?.Id?.IsNullOrEmpty() ?? false)))
			        .ToList();

				if (!wishlistsData.Any()) return wishlists;

				var gamesId = wishlistsData.Select(x => x.Game.Id).Distinct().ToList();
				var shopsId = settings.Stores.Select(x => int.Parse(x.Id)).ToList();

				const int chunkSize = 200;
				var chunks = gamesId
					.Select((item, index) => new { item, index })
					.GroupBy(x => x.index / chunkSize)
					.Select(g => g.Select(x => x.item).ToList())
					.ToList();

				var allGamesPrices = new ConcurrentBag<GamePrices>();

				foreach (var chunk in chunks)
				{
					var prices = await GetGamesPrices(settings.CountrySelected.Alpha2, shopsId, chunk);
					if (prices?.Any() ?? false)
					{
						foreach (var price in prices)
						{
							allGamesPrices.Add(price);
						}
					}
				}

				var gamesPricesLookup = allGamesPrices.ToDictionary(gp => gp.Id, StringComparer.OrdinalIgnoreCase);

				Parallel.ForEach(wishlistsData, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
	                wishlist =>
	                {
		                ProcessWishlistPrices(wishlist, gamesPricesLookup);
	                });
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            return wishlists;
        }

        private void ProcessWishlistPrices(Wishlist wishlist, Dictionary<string, GamePrices> pricesLookup)
        {
            if (!pricesLookup.TryGetValue(wishlist.Game.Id, out var gamePrices) || gamePrices?.Deals == null)
            {
                return;
            }

			var itadGameInfos = new ConcurrentDictionary<string, List<ItadGameInfo>>();
			var dataCurrentPrice = new List<ItadGameInfo>(gamePrices.Deals.Count);
			var dataLowPrice = new List<ItadGameInfo>(3);

			foreach (Deal deal in gamePrices.Deals)
			{
				try
				{
					dataCurrentPrice.Add(new ItadGameInfo
					{
						Name = wishlist.Name,
						StoreId = wishlist.StoreId,
						SourceId = wishlist.SourceId,
						Id = wishlist.Game.Id,
						Slug = wishlist.Game.Slug,
						PriceNew = Math.Round(deal.Price.Amount, 2),
						PriceOld = Math.Round(deal.Regular.Amount, 2),
						PriceCut = deal.Cut,
						CurrencySign = GetCurrencySymbol(deal.Price.Currency),
						ShopName = deal.Shop.Name,
						UrlBuy = deal.Url
					});
				}
				catch (Exception ex)
				{
					Common.LogError(ex, false, true, IsThereAnyDeal.PluginName);
				}
			}

			if (gamePrices.HistoryLow != null)
			{
				if (gamePrices.HistoryLow.All != null)
				{
					try
					{
						dataLowPrice.Add(new ItadGameInfo
						{
							Name = wishlist.Name,
							StoreId = wishlist.StoreId,
							SourceId = wishlist.SourceId,
							Id = wishlist.Game.Id,
							Slug = wishlist.Game.Slug,
							PriceNew = Math.Round(gamePrices.HistoryLow.All.Amount, 2),
							PriceOld = 0,
							PriceCut = 0,
							CurrencySign = GetCurrencySymbol(gamePrices.HistoryLow.All.Currency),
							ShopName = gamePrices.Deals.FirstOrDefault(x => x.StoreLow?.AmountInt == gamePrices.HistoryLow.All.AmountInt)?.Shop?.Name,
							UrlBuy = string.Empty,
							TypePrice = TypePrice.All
						});
					}
					catch (Exception ex)
					{
						Common.LogError(ex, false, true, IsThereAnyDeal.PluginName);
					}
				}
				if (gamePrices.HistoryLow.Y1 != null)
				{
					try
					{
						dataLowPrice.Add(new ItadGameInfo
						{
							Name = wishlist.Name,
							StoreId = wishlist.StoreId,
							SourceId = wishlist.SourceId,
							Id = wishlist.Game.Id,
							Slug = wishlist.Game.Slug,
							PriceNew = Math.Round(gamePrices.HistoryLow.Y1.Amount, 2),
							PriceOld = 0,
							PriceCut = 0,
							CurrencySign = GetCurrencySymbol(gamePrices.HistoryLow.Y1.Currency),
							ShopName = gamePrices.Deals.FirstOrDefault(x => x.StoreLow?.AmountInt == gamePrices.HistoryLow.Y1.AmountInt)?.Shop?.Name,
							UrlBuy = string.Empty,
							TypePrice = TypePrice.Y1
						});
					}
					catch (Exception ex)
					{
						Common.LogError(ex, false, true, IsThereAnyDeal.PluginName);
					}
				}
				if (gamePrices.HistoryLow.M3 != null)
				{
					try
					{
						dataLowPrice.Add(new ItadGameInfo
						{
							Name = wishlist.Name,
							StoreId = wishlist.StoreId,
							SourceId = wishlist.SourceId,
							Id = wishlist.Game.Id,
							Slug = wishlist.Game.Slug,
							PriceNew = Math.Round(gamePrices.HistoryLow.M3.Amount, 2),
							PriceOld = 0,
							PriceCut = 0,
							CurrencySign = GetCurrencySymbol(gamePrices.HistoryLow.M3.Currency),
							ShopName = gamePrices.Deals.FirstOrDefault(x => x.StoreLow?.AmountInt == gamePrices.HistoryLow.M3.AmountInt)?.Shop?.Name,
							UrlBuy = string.Empty,
							TypePrice = TypePrice.M3
						});
					}
					catch (Exception ex)
					{
						Common.LogError(ex, false, true, IsThereAnyDeal.PluginName);
					}
				}
			}

			_ = itadGameInfos.TryAdd(DateTime.Now.ToString("yyyy-MM-dd"), dataCurrentPrice);
			wishlist.ItadGameInfos = itadGameInfos;
			wishlist.ItadLow = dataLowPrice;
		}

		/// <summary>
		/// Converts currency ISO codes to their corresponding visual symbols.
		/// </summary>
		private string GetCurrencySymbol(string currency)
		{
			return CurrencySymbols.TryGetValue(currency, out var symbol) ? symbol : currency;
		}

		/// <summary>
		/// Returns a specific Hex color code for a given shop name to ensure UI consistency.
		/// </summary>
		public static string GetShopColor(string shopName)
        {
            var data = FileDataService.LoadData<List<Shop>>(Path.Combine(IsThereAnyDeal.PluginFolder, "Data", "shops.json"), -1);
            return data?.FirstOrDefault(x => x.Name.IsEqual(shopName))?.Color ?? ResourceProvider.GetResource("TextBrush").ToString();
        }

        /// <summary>
        /// Local cache path for persisted giveaways (<c>giveways.json</c>).
        /// </summary>
        public static string GetGiveawaysCachePath()
        {
            return Path.Combine(IsThereAnyDeal.PluginUserDataPath, "cache", "giveways.json");
        }

        /// <summary>
        /// Last write time of the giveaways cache file, or <c>null</c> if missing.
        /// </summary>
        public static DateTime? GetGiveawaysLastRefresh()
        {
            string path = GetGiveawaysCachePath();
            if (!File.Exists(path))
            {
                return null;
            }

            return File.GetLastWriteTime(path);
        }

        /// <summary>
        /// Fetches available giveaways from the web and compares them with the local cache to detect new entries.
        /// </summary>
        public List<ItadGiveaway> GetGiveaways(bool cacheOnly = false)
        {
            // Load previous
            string pluginFileCache = GetGiveawaysCachePath();
            string pluginDirectoryCache = Path.GetDirectoryName(pluginFileCache);
            if (!string.IsNullOrEmpty(pluginDirectoryCache) && !Directory.Exists(pluginDirectoryCache))
            {
                _ = Directory.CreateDirectory(pluginDirectoryCache);
            }

            var itadGiveawaysCache = FileDataService.LoadData<List<ItadGiveaway>>(pluginFileCache, -1) ?? new List<ItadGiveaway>();

            // Load on web
            List<ItadGiveaway> itadGiveaways = new List<ItadGiveaway>();
            if (!cacheOnly)
            {
                try
                {
                    string data = string.Empty;
                    try
                    {
                        string giveawaysUrl = "https://isthereanydeal.com/giveaways/api/list/";
                        string payload = "{\"offset\":0,\"sort\":null,\"filter\":null}";

                        var cookies = new List<HttpCookie>
                        {
                            new HttpCookie
                            {
                                Name = "sess2",
                                Value = SessionToken,
                                Domain = "isthereanydeal.com",
                                Path = "/"
                            }
                        };

                        var moreHeader = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("ITAD-SessionToken", SessionToken)
                        };

                        data = Web.PostStringDataPayload(giveawaysUrl + "?tab=live", payload, cookies, moreHeader).GetAwaiter().GetResult();
                    }
                    catch (Exception e)
                    {
                        Common.LogError(e, false, "[ITAD][giveaways] request failed", true, IsThereAnyDeal.PluginName);
                    }

                    _ = Serialization.TryFromJson(data, out Giveaways giveaways, out Exception ex);
                    if (ex != null)
                    {
                        Common.LogError(ex, false, "[ITAD][giveaways] json parse failed", true, IsThereAnyDeal.PluginName);
                    }

                    giveaways?.Data?.ForEach(x =>
                    {
                        DateTime? start = null;
                        if (x.Start != null)
                        {
                            start = DateTimeOffset.FromUnixTimeSeconds(x.Start ?? 0).DateTime;
                        }

                        DateTime? time = null;
                        if (x.Expiry != null)
                        {
                            time = DateTimeOffset.FromUnixTimeSeconds(x.Expiry ?? 0).DateTime;
                        }

                        string shop = string.Empty;
                        try
                        {
                            shop = x.Title.Split('-').Last()
                                .Replace("FREE Games on", string.Empty)
                                .Replace("Always FREE For", string.Empty)
                                .Replace("FREE For", string.Empty)
                                .Replace("FREE on", string.Empty)
                                .Trim();
                        }
                        catch (Exception ex2)
                        {
                            Logger.Warn(ex2, $"[ITAD][giveaways] shop parse failed title=\"{x.Title}\"");
                        }

                        Game firstGame = x.Games?.FirstOrDefault();

                        itadGiveaways.Add(new ItadGiveaway
                        {
                            TitleAll = x.Title,
                            Title = firstGame?.Title,
                            Start = start,
                            Time = time,
                            Link = x.Url,
                            ShopName = shop,
                            ShopId = x.Shop,
                            Count = x.Games?.Count ?? 0,
                            Banner145 = firstGame?.Assets?.Banner145,
                            Boxart = firstGame?.Assets?.Boxart,
                            InWaitlist = (x.Counts?.Waitlist ?? 0) != 0,
                            InCollection = (x.Counts?.Collection ?? 0) != 0,
                            IsMature = x.IsMature,
                            DrmFree = firstGame != null && firstGame.DrmFree,
                            Comments = x.Counts?.Comments ?? 0,
                        });
                    });

                    Logger.Info($"[ITAD][giveaways] fetched count={itadGiveaways.Count}");
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "[ITAD][giveaways] fetch failed", true, IsThereAnyDeal.PluginName);
                }
            }

            // REMARK: Compare web results with cache to preserve the "HasSeen" status of older giveaways.
            if (itadGiveaways.Count != 0)
            {
                Logger.Debug($"[ITAD][giveaways] compareCache web={itadGiveaways.Count} cache={itadGiveawaysCache.Count}");
                foreach (ItadGiveaway itadGiveaway in itadGiveawaysCache)
                {
                    if (itadGiveaways.Find(x => x.TitleAll == itadGiveaway.TitleAll) != null)
                    {
                        itadGiveaways.Find(x => x.TitleAll == itadGiveaway.TitleAll).HasSeen = true;
                    }
                }
            }
            // No data
            else
            {
                if (cacheOnly)
                {
                    Logger.Debug($"[ITAD][giveaways] cacheOnly count={itadGiveawaysCache.Count}");
                }
                else
                {
                    Logger.Info($"[ITAD][giveaways] noWebData usingCache count={itadGiveawaysCache.Count}");
                }

                itadGiveaways = itadGiveawaysCache;
            }

			// Save new
			FileDataService.SaveData(pluginFileCache, itadGiveaways);

            return itadGiveaways;
        }

        /// <summary>
        /// Checks for new notifications regarding wishlist prices and giveaways.
        /// This method runs asynchronously to prevent blocking the UI.
        /// </summary>
        public static async Task CheckNotifications(IsThereAnyDeal plugin)
        {
            await Task.Run(() =>
            {
                try
                {
                    IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();
                    IsThereAnyDealSettings settings = plugin.PluginSettingsViewModel.Settings;
                    Logger.Info($"[Notif][check] start price={settings.EnableNotification} giveaways={settings.EnableNotificationGiveaways} criteriaCount={settings.NotificationCriterias?.Count ?? 0}");

                    if (settings.EnableNotification)
                    {
                        if (settings.NotificationCriterias == null || settings.NotificationCriterias.Count == 0)
                        {
                            Logger.Warn("[Notif][check] price notifications enabled but no criteria configured");
                        }

                        List<Wishlist> listWishlist = isThereAnyDealApi.LoadWishlist(plugin, true);
                        List<Wishlist> pendingAlerts = listWishlist
                            .Where(x => x.Game != null && x.GetNotification(settings.NotificationCriterias))
                            .ToList();

                        Logger.Debug($"[Notif][aggregate] matching={pendingAlerts.Count}");

                        if (pendingAlerts.Count > 0)
                        {
                            // Fixed id replaces any previous price-alert toast (anti-spam without persistent dismiss).
                            const string aggregatedNotificationId = "IsThereAnyDeal-PriceAlerts";
                            API.Instance.Notifications.Add(new NotificationMessage(
                                aggregatedNotificationId,
                                "IsThereAnyDeal\r\n" + string.Format(
                                    ResourceProvider.GetString("LOCItadNotificationAggregated"),
                                    pendingAlerts.Count),
                                NotificationType.Info,
                                () =>
                                {
                                    if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
                                    {
                                        WindowOptions windowOptions = new WindowOptions
                                        {
                                            ShowMinimizeButton = false,
                                            ShowMaximizeButton = true,
                                            ShowCloseButton = true,
                                            CanBeResizable = true,
                                            Width = 1180,
                                            Height = 720,
                                            MinWidth = 900,
                                            MinHeight = 500,
                                            WindowPersistenceKey = "IsThereAnyDeal.MainView"
                                        };

                                        IsThereAnyDealView viewExtension = new IsThereAnyDealView(plugin, string.Empty, true);
                                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCItad"), viewExtension, windowOptions);
                                        _ = windowExtension.ShowDialog();
                                    }
                                    else
                                    {
                                        Wishlist first = pendingAlerts.FirstOrDefault();
                                        if (first != null && !first.UrlGame.IsNullOrEmpty())
                                        {
                                            _ = Process.Start(first.UrlGame);
                                        }
                                    }
                                }));

                            Logger.Info($"[Notif][aggregate] count={pendingAlerts.Count}");
                        }
                    }

                    if (settings.EnableNotificationGiveaways)
                    {
                        List<ItadGiveaway> itadGiveaways = isThereAnyDealApi.GetGiveaways();
                        int unseenGiveaways = itadGiveaways.Count(x => !x.HasSeen);
                        Logger.Debug($"[Notif][giveaways] total={itadGiveaways.Count} unseen={unseenGiveaways}");
                        itadGiveaways.Where(x => !x.HasSeen).ForEach(x =>
                        {
                            API.Instance.Notifications.Add(new NotificationMessage(
                                  $"IsThereAnyDeal-{x.Title}",
                                  "IsThereAnyDeal\r\n" + string.Format(ResourceProvider.GetString("LOCItadNotificationGiveaway"), x.TitleAll, x.Count),
                                  NotificationType.Info,
                                  () => Process.Start(x.Link)
                              ));
                        });
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "[Notif][check] failed", true, IsThereAnyDeal.PluginName);
                }
            });
        }

        /// <summary>
        /// Global data update task that refreshes all wishlists with a progress dialog.
        /// </summary>
        public static void UpdateDatas(IsThereAnyDeal plugin)
        {
            IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                ResourceProvider.GetString("LOCITADDataDownloading"),
                false
            )
            {
                IsIndeterminate = true
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress((a) =>
            {
                Logger.Info($"Task UpdateDatas()");
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                try
                {
                    _ = isThereAnyDealApi.LoadWishlist(plugin);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, IsThereAnyDeal.PluginName);
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"Task UpdateDatas() - {string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }, globalProgressOptions);
        }

		#endregion

		private static async Task<T> WithRateLimitAsync<T>(Func<Task<T>> apiCall)
		{
			await RateLimiter.WaitAsync().ConfigureAwait(false);
			try
			{
				var elapsed = (DateTime.Now - _lastApiCall).TotalMilliseconds;
				if (elapsed < MinApiCallIntervalMs)
				{
					await Task.Delay(MinApiCallIntervalMs - (int)elapsed).ConfigureAwait(false);
				}
				_lastApiCall = DateTime.Now;
				return await apiCall().ConfigureAwait(false);
			}
			finally
			{
				_ = RateLimiter.Release();
			}
		}

        private static string GetSessionToken()
        {
            using (var webView = API.Instance.WebViews.CreateOffscreenView(new WebViewSettings
            {
                JavaScriptEnabled = true,
                UserAgent = Web.UserAgent
            }))
            {
                webView.NavigateAndWait("https://isthereanydeal.com/");
                var cookies = webView.GetCookies();
                var sessionCookie = cookies.FirstOrDefault(c => c.Name == "sess2" && c.Domain.IsEqual("isthereanydeal.com"));
                return sessionCookie?.Value;
            }
        }
	}
}
