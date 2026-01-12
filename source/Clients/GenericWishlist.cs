using IsThereAnyDeal.Models;
using Playnite.SDK;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IsThereAnyDeal.Services
{
	/// <summary>
	/// Abstract base class for managing wishlists from various game store clients.
	/// Provides common functionality for loading, saving, and retrieving wishlist data,
	/// as well as integration with the IsThereAnyDeal API for price tracking.
	/// </summary>
	public abstract class GenericWishlist
	{
		#region Properties

		/// <summary>
		/// Gets the logger instance for this class.
		/// </summary>
		private static readonly Lazy<ILogger> LazyLogger = new Lazy<ILogger>(() => LogManager.GetLogger());
		internal static ILogger Logger => LazyLogger.Value;

		/// <summary>
		/// Gets the IsThereAnyDeal plugin instance.
		/// </summary>
		internal IsThereAnyDeal Plugin { get; }

		/// <summary>
		/// Gets the plugin settings.
		/// </summary>
		internal IsThereAnyDealSettings Settings { get; }

		/// <summary>
		/// Gets the name of the store client (e.g., "Steam", "Epic", "GOG").
		/// </summary>
		internal string ClientName { get; }

		/// <summary>
		/// Gets or sets the external plugin reference.
		/// </summary>
		internal static PlayniteTools.ExternalPlugin ExternalPlugin { get; set; }

		/// <summary>
		/// Gets the file data tools instance for managing cached data.
		/// </summary>
		internal FileDataTools FileDataTools { get; }

		/// <summary>
		/// Gets the full file path where wishlist data is stored for this client.
		/// </summary>
		internal string FilePath { get; }

		/// <summary>
		/// Gets the IsThereAnyDeal API client instance (cached).
		/// </summary>
		private readonly Lazy<IsThereAnyDealApi> _lazyApi = new Lazy<IsThereAnyDealApi>(() => new IsThereAnyDealApi());
		internal IsThereAnyDealApi IsThereAnyDealApi => _lazyApi.Value;

		/// <summary>
		/// Cache timeout in minutes (24 hours).
		/// </summary>
		private const int CacheTimeoutMinutes = 1440;

		/// <summary>
		/// Maximum number of items per API request.
		/// </summary>
		private const int MaxApiRequestSize = 200;

		#endregion

		#region Constructor

		/// <summary>
		/// Initializes a new instance of the GenericWishlist class.
		/// </summary>
		/// <param name="plugin">The IsThereAnyDeal plugin instance.</param>
		/// <param name="clientName">The name of the store client (e.g., "Steam", "Epic").</param>
		/// <exception cref="ArgumentNullException">Thrown when plugin or clientName is null.</exception>
		public GenericWishlist(IsThereAnyDeal plugin, string clientName)
		{
			Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
			ClientName = clientName ?? throw new ArgumentNullException(nameof(clientName));

			Settings = Plugin.PluginSettings.Settings;

			string dirPath = Path.Combine(IsThereAnyDeal.PluginUserDataPath, IsThereAnyDeal.PluginName);
			FilePath = Path.Combine(dirPath, $"{ClientName}.json");

			FileDataTools = new FileDataTools(IsThereAnyDeal.PluginName, clientName);
		}

		#endregion

		#region Data Loading and Saving

		/// <summary>
		/// Loads wishlist data from local cache with a maximum age of 1 day.
		/// </summary>
		/// <param name="cacheOnly">If true, forces loading of expired cached data without refreshing.</param>
		/// <param name="forcePrice">If true, forces a refresh of price data for all wishlist items.</param>
		/// <returns>A list of wishlist items, or an empty list if data is unavailable or an error occurs.</returns>
		/// <remarks>
		/// When cacheOnly is false, data older than 1440 minutes (24 hours) will be considered stale.
		/// If forcePrice is true, the method will fetch current prices and save the updated data.
		/// </remarks>
		internal List<Wishlist> LoadWishlists(bool cacheOnly = false, bool forcePrice = false)
		{
			try
			{
				int cacheTimeout = cacheOnly ? -1 : CacheTimeoutMinutes;
				var data = FileDataTools.LoadData<List<Wishlist>>(FilePath, cacheTimeout);

				if (data == null)
				{
					return new List<Wishlist>();
				}

				if (forcePrice)
				{
					data = SetCurrentPrice(data, forcePrice);
					SaveWishlist(data);
				}

				return data;
			}
			catch (Exception ex)
			{
				Common.LogError(ex, false, $"Error loading {ClientName} wishlists", true, IsThereAnyDeal.PluginName);
				return new List<Wishlist>();
			}
		}

		/// <summary>
		/// Saves wishlist data to the local cache file.
		/// </summary>
		/// <param name="wishlists">The list of wishlist items to save.</param>
		public void SaveWishlist(List<Wishlist> wishlists) => FileDataTools.SaveData(FilePath, wishlists);

		#endregion

		#region Wishlist Retrieval

		/// <summary>
		/// Retrieves the wishlist, either from cache or by downloading fresh data from the store.
		/// </summary>
		/// <param name="cacheOnly">If true, only returns cached data without contacting the store.</param>
		/// <param name="forcePrice">If true, forces a refresh of price data for all wishlist items.</param>
		/// <returns>A list of wishlist items from either cache or the store, or cached data if an error occurs.</returns>
		/// <remarks>
		/// If cacheOnly is false, this method will attempt to download fresh data from the store.
		/// If the download fails, it falls back to returning the cached data.
		/// </remarks>
		public List<Wishlist> GetWishlist(bool cacheOnly = false, bool forcePrice = false)
		{
			List<Wishlist> cachedData = LoadWishlists(cacheOnly, forcePrice);

			if (cacheOnly)
			{
				return cachedData;
			}

			try
			{
				return GetStoreWishlist(cachedData);
			}
			catch (Exception ex)
			{
				Common.LogError(ex, false, true, $"Error on GetStoreWishlist({ClientName})");
				return cachedData;
			}
		}

		/// <summary>
		/// Abstract method that must be implemented by derived classes to download wishlist data from the specific store.
		/// </summary>
		/// <param name="cachedData">The currently cached wishlist data, which may be used for comparison or fallback.</param>
		/// <returns>A list of wishlist items retrieved from the store.</returns>
		internal abstract List<Wishlist> GetStoreWishlist(List<Wishlist> cachedData);

		/// <summary>
		/// Abstract method that must be implemented by derived classes to remove a game from the wishlist.
		/// </summary>
		/// <param name="storeId">The store-specific identifier of the game to remove.</param>
		/// <returns>True if the removal was successful, false otherwise.</returns>
		public abstract bool RemoveWishlist(string storeId);

		#endregion

		#region Price Management

		/// <summary>
		/// Updates the current price information for all items in the wishlist using the IsThereAnyDeal API.
		/// </summary>
		/// <param name="wishlists">The list of wishlist items to update with current prices.</param>
		/// <param name="force">If true, forces a refresh even if recent price data exists.</param>
		/// <returns>The wishlist with updated price information.</returns>
		public List<Wishlist> SetCurrentPrice(List<Wishlist> wishlists, bool force)
		{
			if (wishlists == null || wishlists.Count == 0)
			{
				return wishlists ?? new List<Wishlist>();
			}

			return IsThereAnyDealApi.GetCurrentPrice(wishlists, Settings, force).GetAwaiter().GetResult();
		}

		#endregion

		#region Game ID Resolution

		/// <summary>
		/// Retrieves game IDs from the IsThereAnyDeal API based on game titles or existing IDs.
		/// Processes requests in chunks of 200 items to comply with API limitations.
		/// </summary>
		/// <param name="titles">A list of game titles or IDs to look up.</param>
		/// <param name="isTitle">If true, treats the input as game titles; if false, treats them as IDs.</param>
		/// <returns>A dictionary mapping the input values to their corresponding game IDs.</returns>
		/// <remarks>
		/// The IsThereAnyDeal API has a maximum limit of 200 items per request.
		/// This method automatically splits larger requests into multiple API calls.
		/// </remarks>
		public Dictionary<string, string> GetGamesId(List<string> titles, bool isTitle)
		{
			if (titles == null || titles.Count == 0)
			{
				return new Dictionary<string, string>();
			}

			// Split into chunks of maximum 200 items per API request
			var chunks = titles
				.Select((item, index) => new { item, index })
				.GroupBy(x => x.index / MaxApiRequestSize)
				.Select(g => g.Select(x => x.item).ToList())
				.ToList();

			var gamesId = new Dictionary<string, string>();

			foreach (var chunk in chunks)
			{
				var ids = IsThereAnyDealApi.GetGamesId(
					isTitle ? chunk : null,
					!isTitle ? chunk : null
				).GetAwaiter().GetResult();

				if (ids != null && ids.Count > 0)
				{
					foreach (var kvp in ids)
					{
						gamesId[kvp.Key] = kvp.Value;
					}
				}
			}

			return gamesId;
		}

		#endregion

		#region UI Helpers

		/// <summary>
		/// Retrieves the display color associated with this store client from the plugin settings.
		/// </summary>
		/// <returns>
		/// The color string configured for this store, or the default text brush color if none is configured.
		/// </returns>
		/// <remarks>
		/// This method must be invoked on the UI dispatcher thread to safely access UI resources.
		/// </remarks>
		internal string GetShopColor()
		{
			string shopColor = string.Empty;

			API.Instance.MainView.UIDispatcher?.Invoke(new Action(() =>
			{
				var store = Settings.Stores?.FirstOrDefault(x =>
					x.Title?.IndexOf(ClientName, StringComparison.OrdinalIgnoreCase) >= 0);

				shopColor = string.IsNullOrEmpty(store?.Color)
					? ResourceProvider.GetResource("TextBrush")?.ToString() ?? string.Empty
					: store.Color;
			}));

			return shopColor;
		}

		#endregion
	}
}