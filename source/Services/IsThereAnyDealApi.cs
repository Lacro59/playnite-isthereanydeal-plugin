using CommonPluginsShared;
using CommonPluginsShared.Extensions;
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

        /// <summary>
        /// Tracks the number of items fetched per store during the current session.
        /// </summary>
        public List<CountData> CountDatas { get; set; } = new List<CountData>();

		private static string CachePath => Path.Combine(IsThereAnyDeal.PluginUserDataPath, IsThereAnyDeal.PluginName, "ITAD");
		private static FileDataTools FileDataTools => new FileDataTools(IsThereAnyDeal.PluginName, "ITAD");

		private readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(1, 1);
		private DateTime _lastApiCall = DateTime.MinValue;
		private const int MinApiCallIntervalMs = 500;

		private static readonly Dictionary<string, string> CurrencySymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
	        { "eur", "€" },
	        { "usd", "$" },
	        { "gbp", "£" },
	        { "aud", "$" },
	        { "brl", "R$" },
	        { "cad", "$" },
	        { "cny", "¥" }
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
                    Common.LogError(ex, false, $"Error in GetGamesPrices({country})", true, IsThereAnyDeal.PluginName);
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
			var data = await FileDataTools.LoadDataAsync<GameLookup>(cachePath, 4320);

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

                            FileDataTools.SaveData(cachePath, data);
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
            return FileDataTools.LoadData<List<Country>>(Path.Combine(IsThereAnyDeal.PluginFolder, "Data", "countries.json"), -1);
        }

        #region Plugin

        public List<Wishlist> LoadWishlist(IsThereAnyDeal plugin, bool cacheOnly = false, bool forcePrice = false)
        {
            List<Wishlist> combinedWishlist = new List<Wishlist>();
            CountDatas = new List<CountData>();

            // 1. Define the configurations for all supported stores
            var storeConfigs = new List<StoreProviderConfig>
            {
                // --- Steam ---
                new StoreProviderConfig
                {
                    StoreName = "Steam",
                    IsEnabled = () => plugin.PluginSettings.Settings.EnableSteam,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.SteamLibrary },
                    NotificationId = "IsThereAnyDeal-Steam-disabled",
                    LocErrorKey = "LOCItadNotificationErrorSteam",
                    ProviderFactory = () => new SteamWishlist(plugin)
                },

                // --- GOG ---
                new StoreProviderConfig
                {
                    StoreName = "GOG",
                    IsEnabled = () => plugin.PluginSettings.Settings.EnableGog,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.GogLibrary, ExternalPlugin.GogOssLibrary },
                    NotificationId = "IsThereAnyDeal-GOG-disabled",
                    LocErrorKey = "LOCItadNotificationErrorGog",
                    ProviderFactory = () => new GogWishlist(plugin)
                },

                // --- Epic Games Store ---
                new StoreProviderConfig
                {
                    StoreName = "Epic Games Store",
                    IsEnabled = () => plugin.PluginSettings.Settings.EnableEpic,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.EpicLibrary, ExternalPlugin.LegendaryLibrary },
                    NotificationId = "IsThereAnyDeal-EpicGameStore-disabled",
                    LocErrorKey = "LOCItadNotificationErrorEpic",
                    ProviderFactory = () => new EpicWishlist(plugin)
                },

                // --- Humble Bundle ---
                new StoreProviderConfig
                {
                    StoreName = "Humble Bundle",
                    IsEnabled = () => plugin.PluginSettings.Settings.EnableHumble,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.HumbleLibrary },
                    NotificationId = "IsThereAnyDeal-HumbleBundle-disabled",
                    LocErrorKey = "LOCItadNotificationErrorHumble",
                    ProviderFactory = () => new HumbleBundleWishlist(plugin)
                },

                // --- Xbox ---
                new StoreProviderConfig
                {
                    StoreName = "Xbox",
                    IsEnabled = () => plugin.PluginSettings.Settings.EnableXbox,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.XboxLibrary },
                    NotificationId = "IsThereAnyDeal-Xbox-disabled",
                    LocErrorKey = "LOCItadNotificationErrorXbox",
                    ProviderFactory = () => new XboxWishlist(plugin)
                },

                // --- Ubisoft Connect ---
                new StoreProviderConfig
                {
                    StoreName = "Ubisoft Connect",
                    IsEnabled = () => plugin.PluginSettings.Settings.EnableUbisoft,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.UplayLibrary },
                    NotificationId = "IsThereAnyDeal-Ubisoft-disabled",
                    LocErrorKey = "LOCItadNotificationErrorUbisoft",
                    ProviderFactory = () => new UbisoftWishlist(plugin)
                },

                // --- EA (Origin) ---
                new StoreProviderConfig
                {
                    StoreName = "EA",
                    IsEnabled = () => plugin.PluginSettings.Settings.EnableOrigin,
                    RequiredPlugins = new List<ExternalPlugin> { ExternalPlugin.OriginLibrary },
                    NotificationId = "IsThereAnyDeal-EA-disabled",
                    LocErrorKey = "LOCItadNotificationErrorEa",
                    ProviderFactory = () => new EaWishlist(plugin)
                }
            };

            // 2. Process each store generically
            var lockObj = new object();
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

			// 3. Deduplication Logic (Normalized by Name)
			var finalData = DeduplicateWishlist(combinedWishlist);

            if (!cacheOnly || forcePrice)
            {
                plugin.PluginSettings.Settings.LastRefresh = DateTime.Now.ToUniversalTime();
                plugin.SavePluginSettings(plugin.PluginSettings.Settings);
            }

            return finalData.OrderBy(x => x.Name).ToList();
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
            var data = FileDataTools.LoadData<List<Shop>>(Path.Combine(IsThereAnyDeal.PluginFolder, "Data", "shops.json"), -1);
            return data?.FirstOrDefault(x => x.Name.IsEqual(shopName))?.Color ?? ResourceProvider.GetResource("TextBrush").ToString();
        }

        /// <summary>
        /// Fetches available giveaways from the web and compares them with the local cache to detect new entries.
        /// </summary>
        public List<ItadGiveaway> GetGiveaways(bool cacheOnly = false)
        {
            // Load previous
            string pluginDirectoryCache = IsThereAnyDeal.PluginUserDataPath + "\\cache";
            string pluginFileCache = pluginDirectoryCache + "\\giveways.json";

            var itadGiveawaysCache = FileDataTools.LoadData<List<ItadGiveaway>>(pluginFileCache, -1) ?? new List<ItadGiveaway>();

            // Load on web
            List<ItadGiveaway> itadGiveaways = new List<ItadGiveaway>();
            if (!cacheOnly && itadGiveawaysCache != new List<ItadGiveaway>())
            {
                try
                {
                    string data = string.Empty;
                    try
                    {
                        string giveawaysUrl = "https://isthereanydeal.com/giveaways/api/list/";
                        string payload = "{\"offset\":0,\"sort\":null,\"filter\":null}";
                        string sessionToken = "JiCZgSx7tdBwB9D6zJnBq6nMJjiscvx5zznJWQa_ucgH3sOp";

                        var cookies = new List<HttpCookie>
                        {
                            new HttpCookie
                            {
                                Name = "sess2",
                                Value = sessionToken,
                                Domain = "isthereanydeal.com",
                                Path = "/"
                            }
                        };

                        var moreHeader = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("ITAD-SessionToken", sessionToken)
                        };

                        data = Web.PostStringDataPayload(giveawaysUrl + "?tab=live", payload, cookies, moreHeader).GetAwaiter().GetResult();
                    }
                    catch (Exception e)
                    {
                        Common.LogError(e, false, "Erreur lors de la requête ITAD");
                    }

                    _ = Serialization.TryFromJson(data, out Giveaways giveaways, out Exception ex);
                    if (ex != null)
                    {
                        Common.LogError(ex, false, true, "IsThereAnyDeals");
                    }

                    giveaways?.Data?.ForEach(x =>
                    {
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
                            Common.LogError(ex2, false, $"Failed to download {GiveawaysUrl}", true, IsThereAnyDeal.PluginName);
                        }

                        itadGiveaways.Add(new ItadGiveaway
                        {
                            TitleAll = x.Title,
                            Title = x.Games?.FirstOrDefault()?.Title,
                            Time = time,
                            Link = x.Url,
                            ShopName = shop,
                            Count = x.Games?.Count() ?? 0,
                            InWaitlist = x.Counts.Waitlist != 0,
                            InCollection = x.Counts.Collection != 0,
                        });
                    });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error fetching web giveaways", true, IsThereAnyDeal.PluginName);
                }
            }

            // REMARK: Compare web results with cache to preserve the "HasSeen" status of older giveaways.
            if (itadGiveaways.Count != 0)
            {
                Common.LogDebug(true, $"Compare with cache");
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
                Logger.Info("No new data for GetGiveaways()");
                itadGiveaways = itadGiveawaysCache;
            }

			// Save new
			FileDataTools.SaveData(pluginFileCache, itadGiveaways);

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
                IsThereAnyDealApi isThereAnyDealApi = new IsThereAnyDealApi();

                if (plugin.PluginSettings.Settings.EnableNotification)
                {
                    List<Wishlist> ListWishlist = isThereAnyDealApi.LoadWishlist(plugin, true);
                    ListWishlist.Where(x => x.Game != null && x.GetNotification(plugin.PluginSettings.Settings.NotificationCriterias))
                      .ForEach(x =>
                      {
                          API.Instance.Notifications.Add(new NotificationMessage(
                                  $"IsThereAnyDeal-{x.Game.Slug}",
                                  "IsThereAnyDeal\r\n" + string.Format(ResourceProvider.GetString("LOCItadNotification"),
                                      x.Name, x.ItadBestPrice.PriceNew, x.ItadBestPrice.CurrencySign, x.ItadBestPrice.PriceCut),
                                  NotificationType.Info,
                                  () =>
                                  {
                                      if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
                                      {
                                          WindowOptions windowOptions = new WindowOptions
                                          {
                                              ShowMinimizeButton = false,
                                              ShowMaximizeButton = false,
                                              ShowCloseButton = true,
                                              CanBeResizable = false,
                                              Width = 1180,
                                              Height = 720
                                          };

                                          IsThereAnyDealView viewExtension = new IsThereAnyDealView(plugin, x.Game.Id);
                                          Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCItad"), viewExtension, windowOptions);
                                          _ = windowExtension.ShowDialog();
                                      }
                                      else
                                      {
                                          _ = Process.Start(x.UrlGame);
                                      }
                                  }
                              ));
                      });
                }

                if (plugin.PluginSettings.Settings.EnableNotificationGiveaways)
                {
                    List<ItadGiveaway> itadGiveaways = isThereAnyDealApi.GetGiveaways();
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

		private async Task<T> WithRateLimitAsync<T>(Func<Task<T>> apiCall)
		{
			await _rateLimiter.WaitAsync();
			try
			{
				var elapsed = (DateTime.Now - _lastApiCall).TotalMilliseconds;
				if (elapsed < MinApiCallIntervalMs)
				{
					await Task.Delay(MinApiCallIntervalMs - (int)elapsed);
				}
				_lastApiCall = DateTime.Now;
				return await apiCall();
			}
			finally
			{
				_rateLimiter.Release();
			}
		}
	}
}