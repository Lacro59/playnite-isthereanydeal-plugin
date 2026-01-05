using IsThereAnyDeal.Clients;
using IsThereAnyDeal.Models;
using IsThereAnyDeal.Views;
using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using CommonPluginsShared.Extensions;
using IsThereAnyDeal.Models.Api;
using IsThereAnyDeal.Models.ApiWebsite;
using static CommonPluginsShared.PlayniteTools;
using System.IO;
using System.Reflection;
using CommonPlayniteShared.Common;

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

		#region Api

		/// <summary>
		/// Fetches the list of available shops/services supported by ITAD for a specific country.
		/// </summary>
		/// <param name="country">ISO Country Code (Alpha-2).</param>
		/// <returns>A list of <see cref="ServiceShop"/> or null on failure.</returns>
		public async Task<List<ServiceShop>> GetServiceShops(string country)
        {
			// REMARK: Sleep is used to respect ITAD's rate limiting. 
			// Better approach: Use a dedicated RateLimiter or SemaphoreSlim globally.
			Thread.Sleep(500);

			try
            {
                string url = ApiUrl + $"/service/shops/v1?country={country}";
                string data = await Web.DownloadStringData(url).ConfigureAwait(false);

                _ = Serialization.TryFromJson(data, out List<ServiceShop> serviceShops, out Exception ex);
                return ex != null ? throw ex : serviceShops;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error in GetGamesPrices({country})", true, "IsThereAnyDeal");
            }

            return null;
        }


		/// <summary>
		/// Looks up a game's ITAD ID using its title.
		/// </summary>
		/// <param name="title">Game title.</param>
		public async Task<GameLookup> GetGamesLookup(string title)
        {
            return await GetGamesLookup(title, string.Empty);
        }

		/// <summary>
		/// Looks up a game's ITAD ID using its Steam AppID.
		/// </summary>
		/// <param name="appId">Steam Application ID.</param>
		public async Task<GameLookup> GetGamesLookup(int appId)
        {
            return await GetGamesLookup(string.Empty, appId.ToString());
        }

		/// <summary>
		/// Internal method to query the lookup API by title or AppID.
		/// </summary>
		private async Task<GameLookup> GetGamesLookup(string title, string appId)
        {
			// REMARK: Sleep is used to respect ITAD's rate limiting. 
			// Better approach: Use a dedicated RateLimiter or SemaphoreSlim globally.
			Thread.Sleep(500);

			try
            {
                if (!title.IsNullOrEmpty() || !appId.IsNullOrEmpty())
                {
					// REMARK: PlayniteTools.RemoveGameEdition is used to increase match probability by stripping suffixes like "GOTY".
					string url = ApiUrl + $"/games/lookup/v1?key={Key}&"
                        + (!title.IsNullOrEmpty() 
                                ? $"title={WebUtility.UrlEncode(WebUtility.HtmlDecode(PlayniteTools.RemoveGameEdition(title)))}"
                                : $"appid={appId}");
                    string data = await Web.DownloadStringData(url);

                    _ = Serialization.TryFromJson(data, out GameLookup gamesLookup);
                    return gamesLookup;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error in GetGamesLookup({(title.IsNullOrEmpty() ? appId : title)})", true, "IsThereAnyDeal");
            }

            return null;
        }

		/// <summary>
		/// Batch retrieves ITAD internal IDs for a list of titles or Steam IDs.
		/// </summary>
		public async Task<Dictionary<string, string>> GetGamesId(List<string> titles, List<string> appIds)
        {
			// REMARK: Sleep is used to respect ITAD's rate limiting. 
			// Better approach: Use a dedicated RateLimiter or SemaphoreSlim globally.
			Thread.Sleep(500);

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
                Common.LogError(ex, false, $"Error in GetGamesId({payload})", true, "IsThereAnyDeal");
            }

            return null;
        }

		/// <summary>
		/// Fetches the latest price deals for a list of games from specific shops.
		/// </summary>
		/// <param name="country">Alpha-2 country code.</param>
		/// <param name="shopsId">List of shop IDs to check.</param>
		/// <param name="gamesId">List of ITAD internal game IDs.</param>
		public async Task<List<GamePrices>> GetGamesPrices(string country, List<int> shopsId, List<string> gamesId)
        {
			// REMARK: Sleep is used to respect ITAD's rate limiting. 
			// Better approach: Use a dedicated RateLimiter or SemaphoreSlim globally.
			Thread.Sleep(500);

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
                Common.LogError(ex, false, $"Error in GetGamesPrices({country} / {string.Join(",", shopsId)} / {string.Join(",", gamesId)})", true, "IsThereAnyDeal");
            }

            return null;
        }

		#endregion

		/// <summary>
		/// Loads the list of supported countries from the local JSON data file.
		/// </summary>
		public static List<Country> GetCountries()
        {
            try
            {
                string pluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _ = Serialization.TryFromJsonFile(Path.Combine(pluginPath, "Data", "countries.json"), out List<Country> countries, out Exception ex);
                return ex != null ? throw ex : countries;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error in GetCountries()", true, "IsThereAnyDeal");
            }

            return null;
        }

		#region Plugin

		/// <summary>
		/// Aggregates wishlists from all enabled external stores (Steam, GOG, Epic, etc.).
		/// It also handles de-duplication if a game is present in multiple store wishlists.
		/// </summary>
		/// <param name="plugin">The main plugin instance.</param>
		/// <param name="cacheOnly">If true, only local data is loaded without web requests.</param>
		/// <param name="forcePrice">If true, forces a price refresh regardless of cache status.</param>
		/// <returns>A unified and de-duplicated list of <see cref="Wishlist"/> objects.</returns>
		public List<Wishlist> LoadWishlist(IsThereAnyDeal plugin, bool cacheOnly = false, bool forcePrice = false)
        {
			// REMARK: This method pattern iterates through each supported store.
			// It validates if the store is enabled in settings AND if the Playnite library plugin is active.

			List<Wishlist> ListWishlistSteam = new List<Wishlist>();
            if (plugin.PluginSettings.Settings.EnableSteam)
            {
                try
                {
                    if (PlayniteTools.IsEnabledPlaynitePlugin(PlayniteTools.GetPluginId(ExternalPlugin.SteamLibrary)))
                    {
                        SteamWishlist steamWishlist = new SteamWishlist(plugin);
                        ListWishlistSteam = steamWishlist.GetWishlist(cacheOnly, forcePrice);
                        if (ListWishlistSteam == null)
                        {
                            ListWishlistSteam = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Steam",
                            Count = ListWishlistSteam.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Steam is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Steam-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorSteam"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistSteam", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlistGog = new List<Wishlist>();
            if (plugin.PluginSettings.Settings.EnableGog)
            {
                try
                {
                    if (PlayniteTools.IsEnabledPlaynitePlugin(PlayniteTools.GetPluginId(ExternalPlugin.GogLibrary)) || PlayniteTools.IsEnabledPlaynitePlugin(PlayniteTools.GetPluginId(ExternalPlugin.GogOssLibrary)))
                    {
                        GogWishlist gogWishlist = new GogWishlist(plugin);
                        ListWishlistGog = gogWishlist.GetWishlist(cacheOnly, forcePrice);
                        if (ListWishlistGog == null)
                        {
                            ListWishlistGog = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "GOG",
                            Count = ListWishlistGog.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("GOG is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-GOG-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorGog"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistGog", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlistEpic = new List<Wishlist>();
            if (plugin.PluginSettings.Settings.EnableEpic)
            {
                try
                {
                    if (PlayniteTools.IsEnabledPlaynitePlugin(PlayniteTools.GetPluginId(ExternalPlugin.EpicLibrary)) || PlayniteTools.IsEnabledPlaynitePlugin(PlayniteTools.GetPluginId(ExternalPlugin.LegendaryLibrary)))
                    {
                        EpicWishlist epicWishlist = new EpicWishlist(plugin);
                        ListWishlistEpic = epicWishlist.GetWishlist(cacheOnly, forcePrice);
                        if (ListWishlistEpic == null)
                        {
                            ListWishlistEpic = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Epic Game Store",
                            Count = ListWishlistEpic.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Epic Game Store is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-EpicGameStore-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorEpic"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistEpic", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlistHumble = new List<Wishlist>();
            if (plugin.PluginSettings.Settings.EnableHumble)
            {
                try
                {
                    if (PlayniteTools.IsEnabledPlaynitePlugin(PlayniteTools.GetPluginId(ExternalPlugin.HumbleLibrary)))
                    {
                        HumbleBundleWishlist humbleBundleWishlist = new HumbleBundleWishlist(plugin);
                        ListWishlistHumble = humbleBundleWishlist.GetWishlist(cacheOnly, forcePrice);
                        if (ListWishlistHumble == null)
                        {
                            ListWishlistHumble = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Humble Bundle",
                            Count = ListWishlistHumble.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Humble Bundle is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-HumbleBundle-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorHumble"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistHumble", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlistXbox = new List<Wishlist>();
            if (plugin.PluginSettings.Settings.EnableXbox)
            {
                try
                {
                    if (PlayniteTools.IsEnabledPlaynitePlugin(PlayniteTools.GetPluginId(ExternalPlugin.XboxLibrary)))
                    {
                        XboxWishlist xboxWishlist = new XboxWishlist(plugin);
                        ListWishlistXbox = xboxWishlist.GetWishlist(cacheOnly, forcePrice);
                        if (ListWishlistXbox == null)
                        {
                            ListWishlistXbox = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Xbox",
                            Count = ListWishlistXbox.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Xbox is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Xbox-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorXbox"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistXbox", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlistUbisoft = new List<Wishlist>();
            if (plugin.PluginSettings.Settings.EnableUbisoft)
            {
                try
                {
                    if (PlayniteTools.IsEnabledPlaynitePlugin(PlayniteTools.GetPluginId(ExternalPlugin.UplayLibrary)))
                    {
                        UplayWishlist ubisoftWishlist = new UplayWishlist(plugin);
                        ListWishlistUbisoft = ubisoftWishlist.GetWishlist(cacheOnly, forcePrice);
                        if (ListWishlistUbisoft == null)
                        {
                            ListWishlistUbisoft = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "Ubisoft Connect",
                            Count = ListWishlistUbisoft.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("Ubisoft is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-Ubisoft-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorUbisoft"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlistUbisoft", true, "IsThereAnyDeal");
                }
            }

            List<Wishlist> ListWishlisEa = new List<Wishlist>();
            if (plugin.PluginSettings.Settings.EnableOrigin)
            {
                try
                {
                    if (PlayniteTools.IsEnabledPlaynitePlugin(PlayniteTools.GetPluginId(ExternalPlugin.OriginLibrary)))
                    {
                        EaWishlist eaWishlist = new EaWishlist(plugin);
                        ListWishlisEa = eaWishlist.GetWishlist(cacheOnly, forcePrice);
                        if (ListWishlisEa == null)
                        {
                            ListWishlisEa = new List<Wishlist>();
                        }
                        CountDatas.Add(new CountData
                        {
                            StoreName = "EA",
                            Count = ListWishlisEa.Count
                        });
                    }
                    else
                    {
                        Logger.Warn("EA is enable then disabled");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"IsThereAnyDeal-EA-disabled",
                            "IsThereAnyDeal\r\n" + ResourceProvider.GetString("LOCItadNotificationErrorEa"),
                            NotificationType.Error,
                            () => plugin.OpenSettingsView()
                        ));
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error on ListWishlisOrigin", true, "IsThereAnyDeal");
                }
            }

			// Combine all individual lists
			List<Wishlist> ListWishlist = ListWishlistSteam
                .Concat(ListWishlistGog)
                .Concat(ListWishlistHumble)
                .Concat(ListWishlistEpic)
                .Concat(ListWishlistXbox)
                .Concat(ListWishlisEa)
                .Concat(ListWishlistUbisoft)
                .ToList();

			// REMARK: Game name normalization is used here to group duplicates across different storefronts.
			IEnumerable<IGrouping<string, Wishlist>> listDuplicates = ListWishlist
                .GroupBy(c => PlayniteTools.NormalizeGameName(c.Name).ToLower())
                .Where(g => g.Skip(1).Any());

            foreach (IGrouping<string, Wishlist> duplicates in listDuplicates)
            {
                bool isFirst = true;
                Wishlist keep = new Wishlist();
                foreach (Wishlist wish in duplicates)
                {
                    if (isFirst)
                    {
                        keep = wish;
                        isFirst = false;
                    }
                    else
                    {
                        List<Wishlist> keepDuplicates = keep.Duplicates;

                        if (wish.StoreName != ListWishlist.Find(x => x == keep).StoreName)
                        {
                            keepDuplicates.Add(wish);
                            keep.Duplicates = keepDuplicates;

                            ListWishlist.Find(x => x == keep).Duplicates = keepDuplicates;
                            ListWishlist.Find(x => x == keep).hasDuplicates = true;
                            _ = ListWishlist.Remove(wish);
                        }
                    }
                }
            }


            if (!cacheOnly || forcePrice)
            {
                plugin.PluginSettings.Settings.LastRefresh = DateTime.Now.ToUniversalTime();
                plugin.SavePluginSettings(plugin.PluginSettings.Settings);
            }


            return ListWishlist.OrderBy(wishlist => wishlist.Name).ToList();
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
                List<Wishlist> wishlistsData = force
                    ? wishlists
                        .Where(x => !x.Game?.Id?.IsNullOrEmpty() ?? false)
                        .ToList()
                    : wishlists
                        .Where(x => (!x.ItadGameInfos?.Keys?.Contains(DateTime.Now.ToString("yyyy-MM-dd")) ?? true) && (!x.Game?.Id?.IsNullOrEmpty() ?? false))
                        .ToList();

                // Games list
                List<string> gamesId = wishlistsData.Select(x => x.Game.Id).ToList();

                // Stores list
                List<int> shopsId = settings.Stores.Select(x => int.Parse(x.Id)).ToList();

                if (gamesId?.Count() != 0)
                {
                    // Check if in library (exclude game emulated)
                    List<Guid> ListEmulators = API.Instance.Database.Emulators.Select(x => x.Id).ToList();

					// REMARK: Split IDs into groups of 200 for batch processing.
					List<List<string>> chunks = gamesId
                        .Select((item, index) => new { item, index })
                        .GroupBy(x => x.index / 200)
                        .Select(g => g.Select(x => x.item).ToList())
                        .ToList();

                    List<GamePrices> gamesPrices = new List<GamePrices>();
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        List<GamePrices> prices = await GetGamesPrices(settings.CountrySelected.Alpha2, shopsId, chunks[i]);
                        if (prices?.Count() > 0)
                        {
                            gamesPrices.AddRange(prices);
                        }
                    }

                    foreach (Wishlist wishlist in wishlistsData)
                    {
                        ConcurrentDictionary<string, List<ItadGameInfo>> itadGameInfos = new ConcurrentDictionary<string, List<ItadGameInfo>>();
                        List<ItadGameInfo> dataCurrentPrice = new List<ItadGameInfo>();

                        try
                        {
                            GamePrices gamePrices = gamesPrices.Where(x => x.Id.IsEqual(wishlist.Game.Id))?.FirstOrDefault();
                            if (gamePrices?.Deals?.Count > 0)
                            {
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
                                        Common.LogError(ex, false, true, "IsThereAnyDeal");
                                    }
                                }

                                _ = itadGameInfos.TryAdd(DateTime.Now.ToString("yyyy-MM-dd"), dataCurrentPrice);
                                wishlist.ItadGameInfos = itadGameInfos;
                            }
                            else
                            {
                                Common.LogDebug(true, $"No data for {wishlist.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, true);
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            return wishlists;
        }

		/// <summary>
		/// Converts currency ISO codes to their corresponding visual symbols.
		/// </summary>
		private string GetCurrencySymbol(string currency)
        {
            switch (currency.ToLower())
            {
                case "eur":
                    return "€";
                case "usd":
                    return "$";
                case "gpb":
                    return "£";
                case "aud":
                    return "$";
                case "brl":
                    return "R$";
                case "cad":
                    return "$";
                case "cny":
                    return "¥";
                default:
                    return currency;
            }
        }

		/// <summary>
		/// Returns a specific Hex color code for a given shop name to ensure UI consistency.
		/// </summary>
		public static string GetShopColor(string shopName)
        {
			// REMARK: Large dictionary for shop coloring. This could be moved to a configuration file or static field for performance.
			Dictionary<string, string> shopColor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Adventure Shop", "#3e6517" },
                { "AllYouPlay", "#e9267b" },
                { "Amazon", "#fcc588" },
                { "Blizzard", "#00cbe6" },
                { "Bohemia Interactive Store", "#f74040" },
                { "Steam", "#9ffc3a" },
                { "GamersGate", "#fc5d5d" },
                { "Fanatical", "#ff9800" },
                { "Impulse", "#c63f62" },
                { "GamesPlanet UK", "#f6a740" },
                { "GamesPlanet DE", "#f6a740" },
                { "GamesPlanet FR", "#f6a740" },
                { "GamesPlanet US", "#f6a740" },
                { "GameTap", "#f6a740" },
                { "GreenManGaming", "#21a930" },
                { "GetGames", "#fa1f1f" },
                { "Desura", "#03bee0" },
                { "GOG", "#f16421" },
                { "DotEmu", "#f6931c" },
                { "Nuuvem", "#b5e0f4" },
                { "IndieGala Store", "#ffb4e0" },
                { "IndieGala", "#ffb4e0" },
                { "DLGamer", "#f5fe94" },
                { "GameFly", "#f0a690" },
                { "Direct2Drive", "#1df884" },
                { "EA Store", "#ddff1c" },
                { "Ubisoft Store", "#01657a" },
                { "Uplay", "#01657a" },
                { "ShinyLoot", "#bfa236" },
                { "Humble Store", "#ff3e1b" },
                { "Humble Widgets", "#f8300c" },
                { "IndieGameStand", "#73c175" },
                { "GamesRocket", "#e1bc4e" },
                { "Squenix", "#b41919" },
                { "Gameolith", "#80e5ff" },
                { "Fireflower", "#29698c" },
                { "Newegg", "#f79328" },
                { "Games Republic", "#ef0e38" },
                { "Coinplay", "#1b4284" },
                { "Funstock", "#7f3f98" },
                { "WinGameStore", "#2790da" },
                { "MacGameStore", "#2790da" },
                { "GameBillet", "#f22f15" },
                { "Sila Games", "#f9cf6b" },
                { "Playfield", "#e84c31" },
                { "Imperial Games", "#16a085" },
                { "Itch.io", "#fa5c5c" },
                { "Itchio", "#fa5c5c" },
                { "Game Jolt", "#2f7f6f" },
                { "Digital Download", "#0166ff" },
                { "DreamGame", "#497791" },
                { "Paradox", "#bc2a31" },
                { "Chrono", "#59c4c5" },
                { "TwoGame", "#523f95" },
                { "2Game", "#523f95" },
                { "Less4Games", "#ff9900" },
                { "Savemi", "#01add3" },
                { "Gemly", "#ce2745" },
                { "Voidu", "#f47820" },
                { "Cybermanta", "#00b2ee" },
                { "LBOstore", "#005268" },
                { "Razer", "#00ff00" },
                { "Microsoft Store", "#ffd800" },
                { "Microsoft", "#ffd800" },
                { "Oculus", "#5161a6" },
                { "Discord", "#6f85d4" },
                { "Epic", "#0078f2" },
                { "Epic Game Store", "#0078f2" },
                { "Epic Games Store", "#0078f2" },
                { "Epic Game", "#0078f2" },
                { "Epic Games", "#0078f2" },
                { "Playism", "#b8934f" },
                { "GamesLoad", "#b76cc7" },
                { "JoyBuggy", "#43c68d" },
                { "Noctre", "#1a83ff" },
                { "ETailMarket", "#358192" },
                { "ETail.Market", "#358192" }
            };

            if (shopName.IsNullOrEmpty())
            {
                return ResourceProvider.GetResource("TextBrush").ToString();
            }
            _ = !shopColor.TryGetValue(shopName, out string value);
            return value.IsNullOrEmpty() ? ResourceProvider.GetResource("TextBrush").ToString() : value;
        }

		/// <summary>
		/// Fetches available giveaways from the web and compares them with the local cache to detect new entries.
		/// </summary>
		public List<ItadGiveaway> GetGiveaways(string pluginUserDataPath, bool cacheOnly = false)
        {
            // Load previous
            string pluginDirectoryCache = pluginUserDataPath + "\\cache";
            string pluginFileCache = pluginDirectoryCache + "\\giveways.json";
            List<ItadGiveaway> itadGiveawaysCache = new List<ItadGiveaway>();

            try
            {
                FileSystem.CreateDirectory(pluginDirectoryCache, false);
                if (File.Exists(pluginFileCache))
                {
                    _ = Serialization.TryFromJsonFile(pluginFileCache, out itadGiveawaysCache);
                    if (itadGiveawaysCache == null)
                    {
                        itadGiveawaysCache = new List<ItadGiveaway>();
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error in GetGiveAway() with cache data", true, "IsThereAnyDeal");
            }


            // Load on web
            List<ItadGiveaway> itadGiveaways = new List<ItadGiveaway>();
            if (!cacheOnly && itadGiveawaysCache != new List<ItadGiveaway>())
            {
                try
                {
                    string data = string.Empty;
                    try
                    {
                        string payload = "{\"_id\":1,\"offset\":0,\"sort\":null,\"filter\":null,\"options\":[]}";
                        data = Web.PostStringDataPayload(GiveawaysUrl, payload).GetAwaiter().GetResult();
                    }
                    catch (Exception ex2)
                    {
                        Common.LogError(ex2, false, $"Failed to download {GiveawaysUrl}", true, "IsThereAnyDeal");
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
                            Common.LogError(ex2, false, $"Failed to download {GiveawaysUrl}", true, "IsThereAnyDeal");
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
                    Common.LogError(ex, false, "Error fetching web giveaways", true, "IsThereAnyDeal"); 
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
            try
            {
                File.WriteAllText(pluginFileCache, Serialization.ToJson(itadGiveaways));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error in GetGiveAway() with save data", true, "IsThereAnyDeal");
            }

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
                    List<ItadGiveaway> itadGiveaways = isThereAnyDealApi.GetGiveaways(plugin.GetPluginUserDataPath());
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
                    Common.LogError(ex, false, true, "IsThereAnyDeal");
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"Task UpdateDatas() - {string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }, globalProgressOptions);
        }

        #endregion
    }
}