using CommonPluginsShared;
using IsThereAnyDeal.Views;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IsThereAnyDeal.Services
{
    public class ItadTopPanelItem : TopPanelItem
    {
        public ItadTopPanelItem(IsThereAnyDeal plugin)
        {
            Icon = new TextBlock
            {
                Text = "\uea63",
                FontSize = 22,
                FontFamily = ResourceProvider.GetResource("CommonFont") as FontFamily
            };
            Title = ResourceProvider.GetString("LOCItad");
            Activated = () =>
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

                IsThereAnyDealView viewExtension = new IsThereAnyDealView(plugin);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCItad"), viewExtension, windowOptions);
                _ = windowExtension.ShowDialog();
            };
            Visible = plugin.PluginSettings.Settings.EnableIntegrationButtonHeader;
        }
    }
}
