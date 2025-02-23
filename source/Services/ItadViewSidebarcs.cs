using CommonPluginsShared.Controls;
using IsThereAnyDeal.Views;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;

namespace IsThereAnyDeal.Services
{
    public class ItadViewSidebar : SidebarItem
    {
        private static IResourceProvider ResourceProvider => new ResourceProvider();
        private SidebarItemControl SidebarItemControl { get; set; }

        public ItadViewSidebar(IsThereAnyDeal plugin)
        {
            Type = SiderbarItemType.View;
            Title = ResourceProvider.GetString("LOCItad");
            Icon = new TextBlock
            {
                Text = "\uea63",
                FontFamily = ResourceProvider.GetResource("CommonFont") as FontFamily
            };
            Opened = () =>
            {
                if (SidebarItemControl == null)
                {
                    SidebarItemControl = new SidebarItemControl();
                    SidebarItemControl.SetTitle(ResourceProvider.GetString("LOCItad"));
                    SidebarItemControl.AddContent(new IsThereAnyDealView(plugin));
                }
                return SidebarItemControl;
            };
            Visible = plugin.PluginSettings.Settings.EnableIntegrationButtonSide;
        }

        public void ResetView()
        {
            SidebarItemControl = null;
        }
    }
}
