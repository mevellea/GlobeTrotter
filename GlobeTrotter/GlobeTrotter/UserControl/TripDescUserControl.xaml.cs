using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.ApplicationModel.Resources;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using GlobeTrotter.Common;

// Pour en savoir plus sur le modèle d'élément Contrôle utilisateur, consultez la page http://go.microsoft.com/fwlink/?LinkId=234236

namespace GlobeTrotter
{
    public sealed partial class TripDescUserControl : UserControl
    {
        public static DependencyProperty DefBlockTripProperty = DependencyProperty.Register(
            "DefBlockTrip",
            typeof(TripSummary), 
            typeof(TripDescUserControl), 
            new PropertyMetadata(0));

        public TripSummary TripSummary;

        Page _parent;
        List<ImageSource> _pic;
        Boolean _unitMiles;
        DispatcherTimer _timer;
        SynchroHandle _handleProgress;

        public TripDescUserControl(Page _parentPage, Boolean _unit, Theme.EName _theme)
        {
            this.InitializeComponent();
            _parent = _parentPage;
            _pic = new List<ImageSource>();
            _unitMiles = _unit;

            _timer = new DispatcherTimer();
            _timer.Tick += timer_Tick;
            _timer.Interval = TimeSpan.FromMilliseconds(500);

            UpdateTheme(_theme);
        }

        public void UpdateTheme(Theme.EName _name)
        {
            StackPanelMain.Background = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.MainFront));
            textMain.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.Text1));
            textFromTo.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.Text5));
            Datefrom.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.Text4));
            Distance.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.Text4));

        }

        public TripSummary DefBlockTrip
        {
            get
            {
                return (TripSummary)GetValue(DefBlockTripProperty);
            }
            set
            {
                ResourceLoader _res = ResourceLoader.GetForCurrentView();

                SetValue(DefBlockTripProperty, value);
                TripSummary = value;

                UpdatePicturesDisplay(value.PathThumb, value.PicturesThumb, value.Sample);
                textMain.Text = value.LocationMain;
                textFromTo.Text = value.LocationFromTo;
                if (value.Sample)
                    textMain.Text += " (" + _res.GetString("Sample") + ")";

                Datefrom.Text = DateFormat.DateDisplayLocalization(value.DateArrival, DateFormat.EMode.Day);

                UpdateDistance(_unitMiles);

                // define cross variables for synchro
                value.SyncUsb.SetUcSpecific(pic_sync1, rect_Sync1, this);
                value.SyncDropbox.SetUcSpecific(pic_sync2, rect_Sync2, this);
                value.SyncGooglePlus.SetUcSpecific(pic_sync3, rect_Sync3, this);
                value.SyncFacebook.SetUcSpecific(pic_sync4, rect_Sync4, this);

                loadRing.IsActive = false;

                String _index = value.Id;
                StackPanelMain.Tag = _index;
                pic0.Tag = _index;
                pic1.Tag = _index;
                pic2.Tag = _index;
                pic3.Tag = _index;
                pic_single.Tag = _index;
                loadRing.Tag = _index;
                textMain.Tag = _index;
                textFromTo.Tag = _index;
                Distance.Tag = _index;
            }
        }

        public void UpdateDistance(Boolean _unitMiles)
        {
            Distance.Text = Gps.GetDistanceFormat(DefBlockTrip.Distance, _unitMiles);
        }

        private async void UpdatePicturesDisplay(string _path, List<String> p, Boolean _sample)
        {
            await Update(_path, p, _sample);
        }

        private async Task Update(string _path, List<String> p, Boolean _sample)
        {
            Boolean _defaultPictures = false;
            StorageFile _file;
            try
            {
                if (p.Count() < 4)
                {
                    StorageFolder folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(_path);
                    _file = await folder.GetFileAsync(p[0]);
                    Uri uri = new Uri(_file.Path, UriKind.RelativeOrAbsolute);
                    BitmapImage bm = new BitmapImage() { UriSource = uri };
                    _pic.Add(bm);
                }

                else
                {
                    for (int idx = 0; idx < 4; idx++)
                    {
                        if (_sample)
                            _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/" + _path + "/" + p[idx]));
                        else
                        {
                            StorageFolder folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(_path);
                            _file = await folder.GetFileAsync(p[idx]);
                        }
                        Uri uri = new Uri(_file.Path, UriKind.RelativeOrAbsolute);
                        BitmapImage bm = new BitmapImage() { UriSource = uri };
                        _pic.Add(bm);
                    }
                }
            }
            catch
            {
                _defaultPictures = true;
            }

            if (_defaultPictures)
            {
                _pic.Clear();
                for (int idx = 0; idx < 4; idx++)
                {
                    _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/reward_" + idx.ToString() + ".png"));
                    StorageItemThumbnail fileThumbnail = await _file.GetThumbnailAsync(ThumbnailMode.SingleItem, 120, ThumbnailOptions.ResizeThumbnail);
                    BitmapImage bm = new BitmapImage();
                    await bm.SetSourceAsync(fileThumbnail);
                    _pic.Add(bm);
                }
            }

            if (p.Count<string>() < 4)
            {
                pic_single.Source = _pic[0];
                pic_single.Visibility = Visibility.Visible;
                pic0.Visibility = Visibility.Collapsed;
                pic1.Visibility = Visibility.Collapsed;
                pic2.Visibility = Visibility.Collapsed;
                pic3.Visibility = Visibility.Collapsed;
            }
            else
            {
                pic0.Source = _pic[0];
                pic1.Source = _pic[1];
                pic2.Source = _pic[2];
                pic3.Source = _pic[3];
                pic_single.Visibility = Visibility.Collapsed;
                pic0.Visibility = Visibility.Visible;
                pic1.Visibility = Visibility.Visible;
                pic2.Visibility = Visibility.Visible;
                pic3.Visibility = Visibility.Visible;
            }

        }

        private void StackPanelMain_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is StackPanel)
            {
                loadRing.IsActive = true;
                StackPanel panel = sender as StackPanel;
                if (_parent is ViewMapTrip)
                    (_parent as ViewMapTrip).TripDescUserControlSelected(this);
                else if (_parent is ViewHome)
                    (_parent as ViewHome).TripDescUserControlSelected(this);
            }
        }

        private void pageRoot_KeyDown(object sender, KeyRoutedEventArgs e)
        {
        }

        private void pic_sync1_Tapped(object sender, TappedRoutedEventArgs e)
        {
            pic_sync_Tapped(DefBlockTrip.SyncUsb, e);
        }

        private void pic_sync2_Tapped(object sender, TappedRoutedEventArgs e)
        {
            pic_sync_Tapped(DefBlockTrip.SyncDropbox, e);
        }

        private void pic_sync3_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
            if (_parent is ViewHome)
                (_parent as ViewHome).NavigateToConf(DefBlockTrip);
            else if (_parent is ViewMapTrip)
                (_parent as ViewMapTrip).NavigateToConf(DefBlockTrip);
        }

        private void pic_sync4_Tapped(object sender, TappedRoutedEventArgs e)
        {
            pic_sync_Tapped(DefBlockTrip.SyncFacebook, e);
        }

        private async void pic_sync_Tapped(SynchroHandle _obj, TappedRoutedEventArgs e)
        {
            e.Handled = true;
            if (_parent is ViewHome)
                await (_parent as ViewHome).SwitchSynchro(_obj, this);
            else if (_parent is ViewMapTrip)
                await (_parent as ViewMapTrip).SwitchSynchro(_obj, this);
        }

        public void SetSynchroStatus(SynchroHandle _syncObj, SynchroManager.Status _status)
        {
            // not synchronized => rect not visible
            // synchronized but not connected => rect visible red
            // synchronized connected but not finished => rect visible orange
            // synchronized connected but finished => rect visible green

            Visibility _visibility = Visibility.Collapsed;
            SolidColorBrush _color = null;

            switch (_status)
            {
                case SynchroManager.Status.NoRequest:
                    loadRing.IsActive = false;
                    _color = new SolidColorBrush(Colors.White);
                    _timer.Stop();
                    break;
                case SynchroManager.Status.ErrorOrNotConnected:
                    loadRing.IsActive = false;                    
                    _visibility = Visibility.Visible;
                    _color = new SolidColorBrush(Colors.Red);
                    _timer.Stop();
                    break;
                case SynchroManager.Status.InProgress:
                    loadRing.IsActive = true;            
                    _visibility = Visibility.Visible;
                    _color = new SolidColorBrush(Colors.Orange);
                    _handleProgress = _syncObj;
                    _timer.Start();
                    break;
                case SynchroManager.Status.Synchronized:
                    loadRing.IsActive = false;            
                    _visibility = Visibility.Visible;
                    _color = new SolidColorBrush(Colors.Green);
                    _timer.Stop();
                    break;
            }
           
            _syncObj.UpdateRectangle(_visibility, _color);
        }

        private void timer_Tick(object sender, object e)
        {
            if (_handleProgress != null)    
                _handleProgress.BlinkRectangle();
        }

        public void SetActive()
        {
            loadRing.IsActive = true;
        }

        public void SetInactive()
        {
            loadRing.IsActive = false;
        }
    }
}
