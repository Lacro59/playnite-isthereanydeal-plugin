using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CommonPluginsShared.PlayniteTools;

namespace IsThereAnyDeal.Services
{
	public class StoreProviderConfig
	{
		public string StoreName { get; set; }
		public Func<bool> IsEnabled { get; set; }
		public List<ExternalPlugin> RequiredPlugins { get; set; }
		public string NotificationId { get; set; }
		public string LocErrorKey { get; set; }
		public Func<GenericWishlist> ProviderFactory { get; set; }
	}
}