﻿using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using DI.FM.Common;
using DI.FM.Controls;
using DI.FM.ViewModel;
using System;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;

namespace DI.FM.View
{
    public sealed partial class ChannelPage : Page
    {
        private bool IsRightDirection;
        private MainViewModel Model;
        private ChannelItem SelectedItem;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();

        /// <summary>
        /// This can be changed to a strongly typed view model.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return defaultViewModel; }
        }

        public ChannelPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Model = (App.Current.Resources["Locator"] as ViewModelLocator).Main;
            SelectedItem = e.Parameter as ChannelItem;

            DefaultViewModel.Add("Model", Model);
            DefaultViewModel.Add("Channel", SelectedItem);

            UpdatePlayStatus();
            UpdateChannelStatus();
        }

        private void ButtonPlayStop_Click(object sender, RoutedEventArgs e)
        {
            if (Model.NowPlayingItem == SelectedItem)
            {
                Model.TogglePlay();
            }
            else
            {
                Model.PlayChannel(SelectedItem);
                UpdatePlayStatus();
            }
        }

        private void ButtonPrev_Click(object sender, RoutedEventArgs e)
        {
            SelectedItem = SelectedItem.Prev;
            IsRightDirection = false;
            FadeOutRightStory.Begin();
        }

        private void ButtonNext_Click(object sender, RoutedEventArgs e)
        {
            SelectedItem = SelectedItem.Next;
            IsRightDirection = true;
            FadeOutLeftStory.Begin();
        }

        private void ButtonPrev1_Click(object sender, RoutedEventArgs e)
        {
            SelectedItem = SelectedItem.Prev;
            DefaultViewModel["Channel"] = SelectedItem;

            UpdatePlayStatus();
            UpdateChannelStatus();
        }

        private void ButtonNext1_Click(object sender, RoutedEventArgs e)
        {
            SelectedItem = SelectedItem.Next;
            DefaultViewModel["Channel"] = SelectedItem;

            UpdatePlayStatus();
            UpdateChannelStatus();
        }

        private void UpdateChannelStatus()
        {
            UpdateFavoriteStatus();
            UpdatePinnedStatus();

            // Add to the live update list
            Model.LiveUpdateList.Clear();
            Model.LiveUpdateList.Add(SelectedItem);
        }

        private void UpdateFavoriteStatus()
        {
            if (Model.FavoriteChannels.Contains(SelectedItem))
            {
                ButtonFavorite.Style = App.Current.Resources["UnfavoriteAppBarButtonStyle"] as Style;
            }
            else
            {
                ButtonFavorite.Style = App.Current.Resources["FavoriteAppBarButtonStyle"] as Style;
            }
        }

        private void UpdatePinnedStatus()
        {
            if (SecondaryTile.Exists(SelectedItem.Key))
            {
                ButtonPin.Style = App.Current.Resources["UnPinAppBarButtonStyle"] as Style;
            }
            else
            {
                ButtonPin.Style = App.Current.Resources["PinAppBarButtonStyle"] as Style;
            }
        }

        private void UpdatePlayStatus()
        {
            if (Model.NowPlayingItem == SelectedItem)
            {
                var binding = new Binding();
                binding.Path = new PropertyPath("Model.IsPlaying");
                binding.Converter = new PlayStopButtonConverter();
                ButtonPlayStop.SetBinding(StyleProperty, binding);
                ButtonPlayStop1.SetBinding(StyleProperty, binding);
            }
            else
            {
                ButtonPlayStop.Style = App.Current.Resources["PlayIconButtonStyle"] as Style;
                ButtonPlayStop1.Style = ButtonPlayStop.Style;
            }
        }

        private async void ButtonFavorite_Click(object sender, RoutedEventArgs e)
        {
            BottomAppBar.IsOpen = false;

            if (Model.FavoriteChannels.Contains(SelectedItem)) Model.FavoriteChannels.Remove(SelectedItem);
            else Model.FavoriteChannels.Insert(0, SelectedItem);
            UpdateFavoriteStatus();

            await Model.SaveFavoriteChannels();
        }

        private async void ButtonPin_Click(object sender, RoutedEventArgs e)
        {
            BottomAppBar.IsSticky = true;

            var button = (FrameworkElement)sender;
            var buttonTransform = button.TransformToVisual(null);
            var point = buttonTransform.TransformPoint(new Point(0, -20));
            var rect = new Rect(point, new Size(button.ActualWidth, button.ActualHeight));

            if (SecondaryTile.Exists(SelectedItem.Key))
            {
                var tiles = await SecondaryTile.FindAllAsync();
                var secondaryTile = tiles.FirstOrDefault(tile => tile.TileId == SelectedItem.Key);
                if (secondaryTile != null) await secondaryTile.RequestDeleteForSelectionAsync(rect, Placement.Above);
            }
            else
            {
                var logo = new Uri(SelectedItem.Image);
                var tileActivationArguments = SelectedItem.Key + " was pinned at " + DateTime.Now.ToLocalTime().ToString();
                var secondaryTile = new SecondaryTile(SelectedItem.Key, SelectedItem.Name, SelectedItem.Name, tileActivationArguments, TileOptions.ShowNameOnLogo, logo);
                var result = await secondaryTile.RequestCreateForSelectionAsync(rect, Placement.Above);

                if (result)
                {
                    try
                    {
                        // Template
                        var tileTemplate = TileTemplateType.TileSquarePeekImageAndText04;
                        var tileXml = TileUpdateManager.GetTemplateContent(tileTemplate);

                        // Create notification.
                        var notification = new TileNotification(tileXml);

                        // Set notification text.
                        XmlNodeList nodes = tileXml.GetElementsByTagName("image");
                        nodes[0].Attributes[1].NodeValue = SelectedItem.Image;
                        nodes = tileXml.GetElementsByTagName("text");
                        nodes[0].InnerText = SelectedItem.Name;

                        // Update Live Tile.
                        var upd = TileUpdateManager.CreateTileUpdaterForSecondaryTile(SelectedItem.Key);
                        upd.Update(notification);
                    }
                    catch { }
                }
            }

            UpdatePinnedStatus();
            BottomAppBar.IsSticky = false;
        }

        private void ButtonVolume_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new Flyout
            {
                Content = new VolumeControl(),
                Placement = FlyoutPlacementMode.Top,
            };

            flyout.ShowAt(sender as FrameworkElement);
        }

        private void FadeOutStory_Completed(object sender, object e)
        {
            DefaultViewModel["Channel"] = SelectedItem;

            UpdatePlayStatus();
            UpdateChannelStatus();

            if (IsRightDirection) FadeInLeftStory.Begin();
            else FadeInRightStory.Begin();
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}
