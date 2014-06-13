using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Xml;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Runtime.ExceptionServices;
using System.Xml.Serialization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Resources.Core;
using Windows.System;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Popups;
using Windows.UI.ApplicationSettings;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.Search;
using Windows.Storage.FileProperties;
using Windows.Networking.Connectivity;
using Bing.Maps;
using Bing.Maps.Search;
using Bing.Maps.Directions;
using GlobeTrotter;
using GlobeTrotter.Common;

namespace GlobeTrotter
{
    public sealed partial class ViewMapTrip : Page
    {
        public enum VIEW_MODE
        {
            UNDEFINED,
            WORLD,
            ALBUMS,
            PICTURES,
            PICTURE_FULLSCREEN
        }

        public Boolean SliderSelected { get; set; }

        static int ZOOM_NULL = 0;
        static int ZOOM_SLOW = 3;

        MapShapeLayer _routeLayer;
        Trip _tripCurrent;
        Trip _tripPrevious;
        Album _albumCurrent;
        Picture _pictureCurrent;
        ObservableCollection<TripDescUserControl> _listTripDesc = new ObservableCollection<TripDescUserControl>();
        ObservableCollection<AlbumDescUserControl> _listAlbumDesc = new ObservableCollection<AlbumDescUserControl>();
        ObservableCollection<Image> _listPictures = new ObservableCollection<Image>();
        List<TripSummary> _listSummary;
        List<MapShapeLayer> _countriesLayerList = new List<MapShapeLayer>();
        List<MarkerPosition> _countriesMarkerList = new List<MarkerPosition>();
        PercentToDateConverter _convSliderDateTime;
        Double _movementOrigin;
        int _currentIndexFullScreen;
        MarkerPosition _markerPrevious;
        NavigationHelper navigationHelper;
        VIEW_MODE _viewMode;
        VIEW_MODE _diaporamaBase;
        VIEW_MODE _diaporamaState;
        DispatcherTimer _timerDiaporama;
        AppBarPage _appBar;
        Boolean ImageViewModeActive;
        ResourceLoader _res;
        List<GpsLocation> _locationList;
        List<String> _listCountriesUser;
        List<Country> _listCountries;
        int _diaporamaTripCurrent;
        int _diaporamaAlbumCurrent;
        int _diaporamaPictureFullScreenCurrent;
        int _countDiaporama;
        Boolean _displayOnceFullScreenMode;
        Boolean _displayOnceDiaporamaMode;
        Boolean _displayOnceUnknownLocation;
        ConfigurationPanel _configurationPanel;
        Album _albumLocal;
        Trip _tripLocal;
        AlbumDescUserControl _albumDesc_Cb;
        Boolean _mutexTreatment;
        App _app;

        public ViewMapTrip()
        {
            this.InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Enabled;

            _convSliderDateTime = new PercentToDateConverter();
            sliderMap.ThumbToolTipValueConverter = _convSliderDateTime;

            navigationHelper = new NavigationHelper(this);
            navigationHelper.LoadState += navigationHelper_LoadState;  

            _timerDiaporama = new DispatcherTimer();
            _timerDiaporama.Tick += timer_Tick;
            _timerDiaporama.Interval = TimeSpan.FromMilliseconds(1000);

            _appBar = new AppBarPage(this, AppBarPage.SIDE.BOTH, AppBarOpened_callback);

            InitCountries();

            imageTrash.Source = new BitmapImage(new Uri(this.BaseUri, "Icons/trash_empty.png"));
            imageTrash.Visibility = Visibility.Collapsed;

            _res = ResourceLoader.GetForCurrentView();
            btnPlay.Label = _res.GetString("Play");
            btnPause.Label = _res.GetString("Pause");
            btnAddSec.Label = _res.GetString("AddSec");
            btnRemSec.Label = _res.GetString("RemSec");
            txtSeconds.Text = _res.GetString("Seconds");
            txtSearch.Text = _res.GetString("SearchLocation");
        }

        /* Display list levels */

        public async Task DisplayWorldLevel(int _index, String _countryName)
        {
            if (_viewMode == VIEW_MODE.WORLD)
                return;

            if (!MutextGet())
                return;

            ViewModeUpdate(VIEW_MODE.WORLD);

            TripPreviousRemove();

            _listSummary = await TripSummary.Load();

            if (_listSummary.Count == 0)
            {
                if (_app.AppSettings.LearnInProgress("TOPIC_SAMPLE_A"))
                {
                    StorageFile _fileSample = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/SampleA.desc"));
                    TripSummary _sample = Serialization.DeserializeFromXmlFile<TripSummary>(_fileSample.Path);
                    _listSummary.Add(_sample);
                }

                if (_app.AppSettings.LearnInProgress("TOPIC_SAMPLE_B"))
                {
                    StorageFile _fileSample = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/SampleB.desc"));
                    TripSummary _sample = Serialization.DeserializeFromXmlFile<TripSummary>(_fileSample.Path);
                    _listSummary.Add(_sample);
                }
            }

            if (_listSummary.Count > 0)
            {
                _listTripDesc.Clear();

                List<TripSummary> _tripSummOrd = _listSummary.OrderByDescending<TripSummary, DateTime>(o => o.DateArrival).ToList<TripSummary>();

                foreach (TripSummary summary in _tripSummOrd)
                {
                    // displayed trip description block
                    TripDescUserControl blk = new TripDescUserControl(this, _app.AppSettings.GetConfig("CONFIG_MILES"), _app.AppSettings.ThemeColors);
                    blk.DefBlockTrip = summary;
                    _listTripDesc.Add(blk);
                }

                mainList.ItemsSource = _listTripDesc;

                if ((_index >= 0) && (mainList.Items.Count > _index))
                    mainList.SelectedItem = mainList.Items[_index];
                else if ((_index == -1) && (_tripCurrent != null) && (mainList.Items.Count == _listTripDesc.Count()))
                {
                    foreach (TripDescUserControl _tripDesc in _listTripDesc)
                    {
                        if (_tripDesc.TripSummary.Id == _tripCurrent.Summary.Id)
                        {
                            mainList.SelectedItem = _tripDesc;
                            mainList.ScrollIntoView(_tripDesc, ScrollIntoViewAlignment.Leading);
                        }
                    }
                }
            }
            if (_countryName == null)
                mapMain.SetView(new Location(20, 100), 2, new TimeSpan(0, 0, ZOOM_SLOW));
            else
            {
                foreach (Country _country in _listCountries)
                    if (_country.Name == _countryName)
                    {
                        mapMain.SetView(Gps.ConvertGpsToLoc(_country.Center), 4, new TimeSpan(0, 0, ZOOM_SLOW));
                        break;
                    }
            }

            _listCountriesUser.Clear();
            await UpdateCountriesLayer();

            MutexRelease();
        }

        public void DisplayAlbumsList(int _index, int _zoomSpeed)
        {
            if (!MutextGet())
                return;

            AlbumPreviousRemove();
            _tripCurrent.DisplayMarkers(this, mapMain.Children);
            _tripCurrent.DisplayItineraries(_routeLayer, _app.AppSettings.GetConfig("CONFIG_PERFO"));
            _tripPrevious = _tripCurrent;

            List<AlbumSummary> _albumsToCarousel = new List<AlbumSummary>();
            AlbumDescUserControl _albumDesc;
            Boolean _missingPosition = false;
            ViewModeUpdate(VIEW_MODE.ALBUMS);

            PrepareSlider(_tripCurrent.Albums);

            mainList.ItemsSource = _listAlbumDesc;

            if (_zoomSpeed != ZOOM_NULL)
            {
                if (_tripCurrent.PositionPresent)
                    mapMain.SetView(Gps.ConvertGpsToRect(_tripCurrent.Rect), new TimeSpan(0, 0, _zoomSpeed));
                else
                    mapMain.SetView(new Location(43, 60), 3, new TimeSpan(0, 0, _zoomSpeed));
            }

            _listAlbumDesc.Clear();

            foreach (Album _album in _tripCurrent.Albums)
            {
                _albumDesc = new AlbumDescUserControl(_app.AppSettings.ThemeColors, this);
                _albumDesc.DefBlockTrip = _album.Summary;
                _albumDesc.Tapped += AlbumSelected_tapped;
                _listAlbumDesc.Add(_albumDesc);
                if (!_album.PositionPresent)
                {
                    _missingPosition = true;
                    _albumDesc.PositionWarning();
                }
                else
                    _albumDesc.PositionOk();
            }

            if ((_albumCurrent != null) && (mainList.Items.Count > _tripCurrent.Albums.IndexOf(_albumCurrent)))
                UpdateSliderValue(_tripCurrent.Albums.IndexOf(_albumCurrent));

            if ((!_displayOnceUnknownLocation) && (_missingPosition) && (_app.AppSettings != null) && _app.AppSettings.LearnInProgress("TOPIC_DRAG_LOCATION"))
            {
                Toast.DisplayTwoLines(_res.GetString("UnknownLocation"), _res.GetString("Relocation"), "Icons/toastImageAndText.png");
                _displayOnceUnknownLocation = true;
            }
            UpdateBtnContent(_tripCurrent.Summary.LocationMain, "");

            MutexRelease();
        }

        public async Task DisplayPicturesList(String _id, int _index)
        {
            if (!MutextGet())
                return;

            AlbumPreviousRemove();
            ViewModeUpdate(VIEW_MODE.PICTURES);

            foreach (Album _album in _tripCurrent.Albums)
            {
                if (_id.Equals(_album.Id))
                {
                    _albumCurrent = _album;

                    int FLAGS_MAX = 8;
                    List<int> _indexDelete = new List<int>();

                    int _countFlagsModulo = Math.Max(_album.Pictures.Count / FLAGS_MAX, 1);
                    int _countFlags = 0;

                    if (_album.PositionPresent)
                    {
                        mapMain.SetView(Gps.ConvertGpsToRect(_album.Rect), new TimeSpan(0, 0, ZOOM_SLOW));
                        if (_album.MarkerAlbum != null)
                            _album.MarkerAlbum.Activate();
                    }

                    _listPictures.Clear();

                    mainList.ItemsSource = _listPictures;
                    UpdateBtnContent("", _album.Summary.StrLocation);

                    int _countThumb = 0;
                    for (int _idx = 0; _idx < _album.Pictures.Count; _idx++)
                    {
                        StorageItemThumbnail fileThumbnail;
                        StorageFile _file;
                        String _path = _album.Pictures[_idx].PathFolder + "\\" + _album.Pictures[_idx].Name + _album.Pictures[_idx].Extension;
                        try
                        {
                            if (_tripCurrent.Sample)
                                _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/" + _tripCurrent.Summary.PathThumb + "/" + _album.Summary.PicturesThumb[_countThumb++]));
                            else
                                _file = await StorageFile.GetFileFromPathAsync(_path);

                            // get the image url
                            fileThumbnail = await _file.GetThumbnailAsync(ThumbnailMode.SingleItem);

                            BitmapImage bm = new BitmapImage();
                            await bm.SetSourceAsync(fileThumbnail);

                            Image image = new Image();
                            image.Source = bm;
                            image.Tag = _album.Pictures.IndexOf(_album.Pictures[_idx]);

                            // add and set image position
                            _listPictures.Add(image);

                            if (!_album.GpsLocationCommon && _album.Pictures[_idx].PositionPresent)
                            {
                                if (_countFlags++ % _countFlagsModulo == 0)
                                    _album.Pictures[_idx].CreateMarker(this, mapMain.Children, (_countFlags - 1) / _countFlagsModulo);
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            _indexDelete.Add(_idx);
                        }
                        catch (OutOfMemoryException)
                        {
                            //DisplayWorldLevel(-1, "");
                            Debug.WriteLine("Out of memory!");
                            break;
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                        }
                    }
                    for (int _idxDel = _indexDelete.Count; _idxDel > 0; _idxDel--)
                        _album.Pictures.RemoveAt(_indexDelete[_idxDel - 1]);

                    if (_album.Pictures.Count > 0)
                    {
                        PrepareSlider(_album.Pictures);

                        if (_pictureCurrent != null)
                            UpdateSliderValue(_albumCurrent.Pictures.IndexOf(_pictureCurrent));
                    }
                    else
                        sliderMap.Visibility = Visibility.Collapsed;
                }
            }
            MutexRelease();
        }

        private async Task DisplayPicturesList_FullScreen(int _index)
        {
            if (!MutextGet())
                return;

            if ((_albumCurrent != null) && (_index >= 0) && (_index <= _albumCurrent.Pictures.Count - 1))
            {
                UpdateBtnContent("", _albumCurrent.Summary.StrLocation);

                _pictureCurrent = _albumCurrent.Pictures[_index];
                _currentIndexFullScreen = _index;

                //patch: only first picture available in FS when sample mode
                if (_tripCurrent.Sample)
                {
                    _pictureCurrent = _albumCurrent.Pictures.First<Picture>();
                    _currentIndexFullScreen = 1;
                }

                ViewModeUpdate(VIEW_MODE.PICTURE_FULLSCREEN);
                StorageFile file;
                try
                {
                    if (_tripCurrent.Sample)
                        file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/" + _tripCurrent.Summary.PathThumb + "/" +
                            _albumCurrent.Summary.PicturesThumb[0]));
                    else
                        file = await StorageFile.GetFileFromPathAsync(_pictureCurrent.GetPath());

                    StorageItemThumbnail fileThumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 1600, ThumbnailOptions.ResizeThumbnail);
                    BitmapImage bm = new BitmapImage();
                    await bm.SetSourceAsync(fileThumbnail);
                    imageFullScreen.Source = bm;
                }
                catch (FileNotFoundException)
                {
                    _albumCurrent.Pictures.Remove(_pictureCurrent);
                }

                mainList.Focus(FocusState.Programmatic);
            }

            MutexRelease();
        }

        /* Display list levels end */

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //if (this._pageKey != null && e.NavigationMode == NavigationMode.Back) return;

            if (this.Frame.BackStack.Count > 0)
            {
                PageStackEntry _appRefEntry = this.Frame.BackStack.First<PageStackEntry>(); ;
                if (_appRefEntry.Parameter is App)
                {
                    _app = (App)_appRefEntry.Parameter;
                    _app.SynchroManager.Parent = this;
                }
            }

            _configurationPanel = new ConfigurationPanel(_app);
            _configurationPanel.AddSwitchCallback(ConfigSwitchChanged_callback);
            _configurationPanel.AddComboBoxCallback(ConfigThemeChanged_callback);

            UpdateTheme(_app.AppSettings.ThemeColors);

            SettingsPane.GetForCurrentView().CommandsRequested += _configurationPanel.OnCommandsRequested;
            AccountsSettingsPane.GetForCurrentView().AccountCommandsRequested += _configurationPanel.AccountCommandsRequested;

            navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedFrom(e);

            SettingsPane.GetForCurrentView().CommandsRequested -= _configurationPanel.OnCommandsRequested;
        }

        public async void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            _routeLayer = new MapShapeLayer();
            mapMain.ShapeLayers.Add(_routeLayer);


            if (e.NavigationParameter is TripDescUserControl)
            {
                _tripCurrent = await Trip.Load(e.NavigationParameter as TripDescUserControl);

                mapMain.ShowNavigationBar = false;
                mapMain.ShowBreadcrumb = false;
                mapMain.ShowScaleBar = false;

                if (Connection.MeteredOrLimited())
                    mapMain.PreloadArea = PreloadArea.None;
                else
                    mapMain.PreloadArea = PreloadArea.Large;

                this.DataContext = _tripCurrent;

                if (_tripCurrent.PositionPresent)
                    mapMain.SetView(Gps.ConvertGpsToLoc(_tripCurrent.Rect.Center), 2, new TimeSpan(0));

                logoGoogle.Source = new BitmapImage(new Uri(this.BaseUri, "Assets/powered-by-google-on-white.png"));

                DisplayAlbumsList(-1, ZOOM_SLOW);
            }
            else
            {
                await DisplayWorldLevel(-1, e.NavigationParameter as String);
            }
        }

        private async void InitCountries()
        {
            _listCountriesUser = new List<String>();

            StorageFile _fileCountries = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/CountryDefinition.desc"));
            _listCountries = Serialization.DeserializeFromXmlFile<List<Country>>(_fileCountries.Path);
        }

        private async void timer_Tick(object sender, object e)
        {
            switch (_diaporamaState)
            {
                case VIEW_MODE.WORLD:
                    {
                        if (_countDiaporama == 0)
                            await DisplayWorldLevel(_diaporamaTripCurrent++, null);
                        else if (_countDiaporama == 3)
                        {
                            _countDiaporama = -1;
                            _diaporamaState = VIEW_MODE.ALBUMS;

                            if ((_listTripDesc.Count != 0) && (_diaporamaTripCurrent >= _listTripDesc.Count-1))
                                _diaporamaTripCurrent = 0;
                        }
                        break;
                    }
                case VIEW_MODE.ALBUMS:
                    {
                        if (_countDiaporama == 0)
                        {
                            if ((_diaporamaBase == VIEW_MODE.ALBUMS)&&(_diaporamaAlbumCurrent+1 < _tripCurrent.Albums.Count))
                                mainList.SelectedItem = mainList.Items[_diaporamaAlbumCurrent+1];

                            DisplayAlbumsList(_diaporamaAlbumCurrent, ZOOM_SLOW);
                        }
                        else if (_countDiaporama == 3)
                        {
                            _countDiaporama = -1;
                            _diaporamaState = VIEW_MODE.PICTURE_FULLSCREEN;

                            if (_diaporamaAlbumCurrent >= _tripCurrent.Albums.Count)
                            {
                                _diaporamaAlbumCurrent = 0;
                                if (_diaporamaBase == VIEW_MODE.WORLD)
                                    _diaporamaState = VIEW_MODE.WORLD;
                            }
                        }
                        break;
                    }
                case VIEW_MODE.PICTURE_FULLSCREEN:
                    {
                        if (_countDiaporama == 0)
                        {
                            if ((_diaporamaBase == VIEW_MODE.ALBUMS) || (_diaporamaBase == VIEW_MODE.WORLD))
                                _albumCurrent = _tripCurrent.Albums[_diaporamaAlbumCurrent];

                            await DisplayPicturesList_FullScreen(_diaporamaPictureFullScreenCurrent++);
                        }
                        else if (_countDiaporama == 1)
                        {
                            _countDiaporama = -1;

                            if (_diaporamaPictureFullScreenCurrent >= _albumCurrent.Pictures.Count)
                            {
                                _diaporamaPictureFullScreenCurrent = 0;
                                if ((_diaporamaBase == VIEW_MODE.WORLD) || (_diaporamaBase == VIEW_MODE.ALBUMS))
                                {
                                    _diaporamaState = VIEW_MODE.ALBUMS;
                                    _diaporamaAlbumCurrent++;
                                }
                            }
                        }
                        break;
                    }
            }
            _countDiaporama++;
        }

        private void ConfigThemeChanged_callback(object sender, SelectionChangedEventArgs args)
        {
            ComboBox _input = sender as ComboBox;
            Theme.EName _theme = (Theme.EName)_input.SelectedIndex;

            UpdateTheme(_theme);
        }

        private void UpdateTheme(Theme.EName _theme)
        {
            btnHome.Background = new SolidColorBrush(Theme.GetColorFromTheme(_theme, Theme.EColorPalet.MainFront));
            btnWorld.Background = new SolidColorBrush(Theme.GetColorFromTheme(_theme, Theme.EColorPalet.Text1));
            btnMain.Background = new SolidColorBrush(Theme.GetColorFromTheme(_theme, Theme.EColorPalet.Text2));
            btnSub.Background = new SolidColorBrush(Theme.GetColorFromTheme(_theme, Theme.EColorPalet.Text3));

            foreach (TripDescUserControl _userControl in _listTripDesc)
                _userControl.UpdateTheme(_theme);

            foreach (AlbumDescUserControl _userControl in _listAlbumDesc)
                _userControl.UpdateTheme(_theme);
        }

        private void ConfigSwitchChanged_callback(object sender, RoutedEventArgs args)
        {
            ToggleSwitch _input = sender as ToggleSwitch;

            if (_input.Name.Equals("CONFIG_MILES"))
                foreach (TripDescUserControl _userControl in _listTripDesc)
                    _userControl.UpdateDistance(_input.IsOn);

            _app.AppSettings.SetConfig(_input.Name, _input.IsOn);
        }

        private void ConfigComboChanged_callback(object sender, SelectionChangedEventArgs args)
        {
            if (sender is ComboBox)
            {
                //ComboBox _input = sender as ComboBox;
                //if (_input.Name.Equals("comboCountry"))
                //    _app.AppSettings.CountryConfig = _input.SelectedItem.ToString();
                //UpdateCountriesLayer();
            }
        }

        private void UpdateBtnContent(String _main, String _sub)
        {
            if (_main.Length > 0)
            {
                btnMain.Content = _main;
                int _strLength = _main.Length;
                btnMain.Width = _strLength * 10 + 50;
                Thickness _thickness = btnMain.Margin;
                _thickness.Left += btnMain.Width + 72;
                btnSub.Margin = _thickness;
            }
            
            if (_sub.Length > 0)
            {
                if (_sub.Length > 32)
                {
                    _sub = _sub.Remove(32);
                    _sub += "...";
                }

                btnSub.Content = _sub;
                int _strLength = _sub.Length;
                btnSub.Width = Math.Min(550, _strLength * 10 + 50);
            }
        }

        public void FlagSelected(Int32 _flagIdx, MarkerPosition _marker)
        {
            if (_markerPrevious != null)
                _markerPrevious.DeActivate();

            _marker.Activate();
            _markerPrevious = _marker;

            UpdateSliderValue(_flagIdx);
        }

        private void AlbumPreviousRemove()
        {
            if (_albumCurrent != null)
                _albumCurrent.RemoveMarkerPictures();
        }

        private void TripPreviousRemove()
        {
            if (_tripPrevious != null)
            {
                _tripPrevious.RemoveMarkers();
                _tripPrevious.RemoveItineraries(_routeLayer);
            }
        }

        public async void TripDescUserControlSelected(TripDescUserControl _tripDesc)
        {
            // _tripCurrent must be defined at this stage
            _tripCurrent = await Trip.Load(_tripDesc);
            DisplayAlbumsList(-1, ZOOM_SLOW);
        }

        private Boolean MutextGet()
        {
            if (MutexBusy())
                return false;
            else
            {
                ProgressRingMap.IsActive = true;
                _mutexTreatment = true;
            }
            return true;
        }

        private void MutexRelease()
        {
            ProgressRingMap.IsActive = false;
            _mutexTreatment = false;
        }

        private Boolean MutexBusy()
        {
            return _mutexTreatment;
        }

        private async Task UpdateCountriesLayer()
        {
            if (_locationList == null)
                await LoadLocationList();

            List<String> _listCountriesRef = new List<string>();
            foreach (String _country in _listCountriesUser)
                _listCountriesRef.Add(_country);

            _listCountriesUser.Clear();

            foreach (Country _country in _listCountries)
            {
                if (_app.AppSettings.GetCountryActive(_country.Code))
                    _listCountriesUser.Add(_country.Name);
                _countriesMarkerList.Add(SetFlag(_country));
            }

            foreach (TripDescUserControl _tripDesc in _listTripDesc)
                foreach (String _str in _tripDesc.DefBlockTrip.Countries)
                    if (!_listCountriesUser.Contains(_str))
                        _listCountriesUser.Add(_str);

            _listCountriesUser = _listCountriesUser.OrderBy<String, String>(o => o).ToList<String>();

            for (int _idx = _listCountriesRef.Count - 1; _idx >= 0; _idx--)
                if (!_listCountriesUser.Contains(_listCountriesRef[_idx]))
                    foreach (Country _country in _listCountries)
                        if (_country.Name.Equals(_listCountriesRef[_idx]))
                        {
                            RemoveCountryLayer(_country, _listCountriesRef);
                            break;
                        }

            foreach (String _countryName in _listCountriesUser)
                if (!_listCountriesRef.Contains(_countryName))
                    foreach (Country _country in _listCountries)
                        if (_country.Name.Equals(_countryName))
                            AddCountryLayer(_country);
        }

        private MarkerPosition SetFlag(Country _country)
        {
            return new MarkerPosition(this, mapMain.Children, _country, "Flags/" + _country.Code.ToLower() + ".png", _country.Center, true, 16);
        }

        private void ClearAllCountriesLayer()
        {
            foreach (MapShapeLayer _layer in _countriesLayerList)
                mapMain.ShapeLayers.Remove(_layer);

            foreach (MarkerPosition _marker in _countriesMarkerList)
                _marker.Hide();

            _countriesLayerList.Clear();
            _countriesMarkerList.Clear();
        }

        private void RemoveCountryLayer(Country _countryLoc, List<String> _listRef)
        {
            int _index = _listRef.IndexOf(_countryLoc.Name);
            mapMain.ShapeLayers.Remove(_countriesLayerList[_index]);
        }

        private MapShapeLayer AddCountryLayer(Country _countryLoc)
        {
            MapShapeLayer _countryLayer = new MapShapeLayer();
            int _index = _listCountriesUser.IndexOf(_countryLoc.Name);

            foreach (List<Country.PointUnit> _listPointUnit in _countryLoc.PointsBlock)
            {
                LocationCollection _locations = new LocationCollection();
                foreach (Country.PointUnit _pointUnit in _listPointUnit)
                {
                    if ((_pointUnit.IndexStart < _locationList.Count) && (_pointUnit.IndexStop < _locationList.Count))
                    {
                        if ((_pointUnit.IndexStart > _pointUnit.IndexStop) && (_locationList.Count > _pointUnit.IndexStop))
                            for (int _idxTab = _pointUnit.IndexStart; _idxTab >= _pointUnit.IndexStop; _idxTab--)
                                _locations.Add(Gps.ConvertGpsToLoc(_locationList[_idxTab]));
                        else
                            for (int _idxTab = _pointUnit.IndexStart; _idxTab <= _pointUnit.IndexStop; _idxTab++)
                                _locations.Add(Gps.ConvertGpsToLoc(_locationList[_idxTab]));
                    }
                }

                if (_locations.Count > 0)
                {
                    //Create a MapPolyline of the country boundaries and add it to the map
                    MapPolygon _polygone = new MapPolygon();
                    _polygone.FillColor = Colors.Yellow;
                    _polygone.Locations = _locations;
                    _countryLayer.Shapes.Add(_polygone);

                    MapPolyline _polyLine = new MapPolyline();
                    _polyLine.Locations = _locations;
                    _polyLine.Color = Colors.Orange;
                    _polyLine.Width = 3;
                    _countryLayer.Shapes.Add(_polyLine);

                    MapPolyline _polyLineInit = new MapPolyline();
                    _polyLine.Locations.Add(_locations.Last<Location>());
                    _polyLine.Locations.Add(_locations.First<Location>());
                    _polyLine.Color = Colors.Orange;
                    _polyLine.Width = 3;
                    _countryLayer.Shapes.Add(_polyLineInit);
                }
            }

            if (_countriesLayerList.Count >= _index)
            {
                _countriesLayerList.Insert(_index, _countryLayer);
                mapMain.ShapeLayers.Add(_countriesLayerList[_index]);
            }
            return _countryLayer;
        }

        private async Task<Boolean> LoadLocationList()
        {
            StorageFile sourceFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/CountryData.dat"));

            Stream outputStream = await sourceFile.OpenStreamForReadAsync();
            outputStream.Seek(0, SeekOrigin.Begin);

            byte[] _octets = new byte[2];
            _locationList = new List<GpsLocation>();

            for (int _idx = 0; _idx < outputStream.Length; _idx = _idx + 4)
            {
                GpsLocation _location = new GpsLocation();

                outputStream.Read(_octets, 0, 2);
                UInt16 _lsb = Convert.ToUInt16(_octets[0]);
                UInt16 _msb = Convert.ToUInt16(_octets[1] << 8);
                Double _data = (Double)(_msb + _lsb);
                _location.Latitude = (_data - 18000) / 100;

                outputStream.Read(_octets, 0, 2);
                _lsb = Convert.ToUInt16(_octets[0]);
                _msb = Convert.ToUInt16(_octets[1] << 8);
                _data = (Double)(_msb + _lsb);
                _location.Longitude = (_data - 18000) / 100;

                _locationList.Add(_location);
            }
            outputStream.Dispose();
            return true;
        }

        private Image GetFlag(String _code)
        {
            Image _image = new Image();
            _image.Source = new BitmapImage(new Uri(this.BaseUri, "Flags/" + _code + ".png"));
            _image.Width = 30;
            _image.Height = 30;
            foreach (Country _country in _listCountries)
            {
                if (_country.Code.Equals(_code.ToUpper()))
                {
                    _image.Tag = _country.Name;
                    break;
                }
            }
            return _image;
        }

        private async void AlbumSelected_tapped(object sender, TappedRoutedEventArgs e)
        {
            AlbumDescUserControl _albumDesc = sender as AlbumDescUserControl;
            await DisplayPicturesList(_albumDesc.DefBlockTrip.Id, -1);
        }

        private void UpdateTiles()
        {
            MapTileLayer layerGoogleMap = new MapTileLayer();
            layerGoogleMap.GetTileUri += (s, ev) =>
            {
                ev.Uri = new Uri(string.Format("https://mts0.google.com/vt/hl=he&src=api&x={0}&s=&y={1}&z={2}", ev.X, ev.Y, ev.LevelOfDetail));
            };

            mapMain.TileLayers.Add(layerGoogleMap);
        }

        private async void map_Drop(object sender, DragEventArgs e)
        {
            Location _newLocation;
            GpsLocation _location;
            int _index;
            imageTrash.Visibility = Visibility.Collapsed; 
            DataPackageView _package = e.Data.GetView();
            bool _requestUpdateAllPicturesPosition = false;
            
            if ((_viewMode != VIEW_MODE.ALBUMS) && (_viewMode != VIEW_MODE.PICTURES))
                return;

            if (_tripCurrent.Sample)
            {
                Toast.DisplayTwoLines(_res.GetString("SampleMode"), _res.GetString("DragDropEnabled"), "Icons/toastImageAndText.png");
                return;
            }

            _app.AppSettings.LearnDone("TOPIC_DRAG_LOCATION");

            if (_package.Contains("itemIndex"))
                _index = (int)Convert.ToDecimal(await _package.GetTextAsync("itemIndex"));
            else
                return;

            if (!MutextGet())
                return;

            mapMain.TryPixelToLocation(e.GetPosition(mapMain), out _newLocation);
            _location = new GpsLocation(_newLocation);

            // save variables in case user change current view
            _albumLocal = _albumCurrent;
            _tripLocal = _tripCurrent;

            _albumDesc_Cb = _listAlbumDesc[_index];

            if (_viewMode == VIEW_MODE.PICTURES)
            {
                await _albumLocal.Pictures[_index].UpdateMetadata(_location);
                _albumLocal.RequestLocationUpdate();
                _albumLocal.Pictures[_index].RemoveMarker();
                _albumLocal.Pictures[_index].CreateMarker(this, mapMain.Children, _index);

                await _tripLocal.Update(false, true, CallbackPosition, CallbackItinerary, null, this);
            }
            else
            {
                // force position of first picture
                await _albumLocal.ForcePosition(_location);
                _listAlbumDesc[_tripLocal.Albums.IndexOf(_albumLocal)].SetActive();
                _requestUpdateAllPicturesPosition = true;

                await _tripLocal.Update(false, true, CallbackPosition, CallbackItinerary, null, this);

            }
            _albumLocal.RemoveMarker();
            _albumLocal.CreateMarker(this, mapMain.Children);
            
            if (_listSummary == null)
                _listSummary = await TripSummary.Load();

            foreach (TripSummary _summary in _listSummary)
            {
                if (_tripLocal.Summary.Id == _summary.Id)
                {
                    _summary.LocationMain = _tripLocal.Summary.LocationMain;
                    _summary.LocationFromTo = _tripLocal.Summary.LocationFromTo;
                    _summary.ClearSyncRequest();
                    break;
                }
            }

            await SaveSummary();

            if (_requestUpdateAllPicturesPosition)
                foreach (Picture _picture in _albumLocal.Pictures)
                    if (!_picture.PositionPresent)
                        await _picture.UpdateMetadata(_location);
            
            if (_viewMode == VIEW_MODE.ALBUMS)
                _listAlbumDesc[_index].PositionOk();

            MutexRelease();
        }

        private void CallbackItinerary()
        {
            _tripLocal.DisplayItineraries(_routeLayer, _app.AppSettings.GetConfig("CONFIG_PERFO"));
        }

        private void CallbackPosition()
        {
            _albumDesc_Cb.UpdateName(_albumLocal.Summary.StrLocation);
            UpdateBtnContent(_tripLocal.Summary.LocationMain, _albumLocal.Summary.StrLocation);
        }

        private void mainList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (_viewMode == VIEW_MODE.ALBUMS)
            {
                imageTrash.Visibility = Visibility.Visible;
                imageTrash.Source = new BitmapImage(new Uri(this.BaseUri, "Icons/trash_empty.png"));

                int _index = _listAlbumDesc.IndexOf(e.Items[0] as AlbumDescUserControl);
                if (_index < _tripCurrent.Albums.Count)
                {
                    _albumCurrent = _tripCurrent.Albums[_index];
                    e.Data.SetData("itemIndex", _index.ToString());
                }
            }
            else if (_viewMode == VIEW_MODE.PICTURES)
            {
                imageTrash.Visibility = Visibility.Visible;
                imageTrash.Source = new BitmapImage(new Uri(this.BaseUri, "Icons/trash_empty.png"));

                e.Data.SetData("itemIndex", _listPictures.IndexOf(e.Items[0] as Image).ToString());
            }
            else if (_viewMode == VIEW_MODE.WORLD)
            {
                imageTrash.Visibility = Visibility.Visible;
                imageTrash.Source = new BitmapImage(new Uri(this.BaseUri, "Icons/trash_empty.png"));

                e.Data.SetData("itemIndex", _listTripDesc.IndexOf(e.Items[0] as TripDescUserControl).ToString());
            }
        }
        
        private void backAlbum_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(ViewHome));
        }
        
        private void sliderMap_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (SliderSelected)
                UpdateSliderValue((int)Math.Round(e.NewValue));
        }

        private void UpdateSliderValue(int _index)
        {
            if ((_convSliderDateTime != null) && (_convSliderDateTime.DateNameDisplay != null))
            {
                if ((_convSliderDateTime.DateNameDisplay.Count > _index) && (mainList.Items.Count > _index) && (_index >= 0))
                {
                    _convSliderDateTime.CurrentName = _convSliderDateTime.DateNameDisplay[_index].Name;

                    if (_convSliderDateTime.TypeDisplay == typeof(Album))
                    {
                        for (int _idx = 0; _idx < _tripCurrent.Albums.Count; _idx++)
                        {
                            if (_convSliderDateTime.CurrentName.Equals(_tripCurrent.Albums[_idx].Id))
                            {
                                mainList.SelectedIndex = _idx;
                                mainList.ScrollIntoView(mainList.Items[_idx], ScrollIntoViewAlignment.Leading);

                                if (_tripCurrent.Albums[_idx].PositionPresent)
                                    mapMain.SetView(Gps.ConvertGpsToRect(_tripCurrent.Albums[_idx].Rect), new TimeSpan(0, 0, 2));
                               

                                if ((_tripCurrent.Albums[_idx].PositionPresent) && (_tripCurrent.Albums[_idx].MarkerAlbum != null))
                                    _tripCurrent.Albums[_idx].MarkerAlbum.Activate();
                            }
                            else
                            {
                                if ((_tripCurrent.Albums[_idx].PositionPresent) && (_tripCurrent.Albums[_idx].MarkerAlbum != null))
                                    _tripCurrent.Albums[_idx].MarkerAlbum.DeActivate();
                            }
                        }
                    }
                    else
                    {
                        if (_albumCurrent!= null)
                        {
                            foreach (Picture _picture in _albumCurrent.Pictures)
                                if (_picture.MarkerPicture != null)
                                    _picture.MarkerPicture.DeActivate();

                            for (int _idx = 0; _idx < _albumCurrent.Pictures.Count; _idx++)
                            {
                                DateTime _dateRef = _convSliderDateTime.DateNameDisplay[_index].Date;
                                if ((_albumCurrent.Pictures[_idx].Date >= _dateRef) && (mainList.Items.Count > _idx))
                                {
                                    //mainList.SelectedIndex = _idx;
                                    mainList.SelectedIndex = -1;
                                    mainList.ScrollIntoView(mainList.Items[_idx], ScrollIntoViewAlignment.Leading);

                                    if (_albumCurrent.Pictures[_idx].MarkerPicture != null)
                                        _albumCurrent.Pictures[_idx].MarkerPicture.Activate();

                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (!SliderSelected)
                sliderMap.Value = (Double)_index;
        }

        private void PrepareSlider(object _obj)
        {
            if (ImageViewModeActive)
            {
                sliderMap.Visibility = Visibility.Collapsed;
                return;
            }

            List<StAlbumDateName> _listDateTime = new List<StAlbumDateName>();
            List<StAlbumDateName> _listDateTimeOrd = new List<StAlbumDateName>();

            if (_obj.GetType() == typeof(List<Picture>))
            {
                _convSliderDateTime.TypeDisplay = typeof(Picture);
                foreach (Picture _picture in (_obj as List<Picture>))
                    if ((_listDateTime.Count == 0) || (_picture.Date > _listDateTime.Last<StAlbumDateName>().Date))
                        _listDateTime.Add(new StAlbumDateName(_picture.Name, _picture.Date));
            }
            else if (_obj.GetType() == typeof(List<Album>))
            {
                _convSliderDateTime.TypeDisplay = typeof(Album);
                foreach (Album _album in (_obj as List<Album>))
                    if ((_listDateTime.Count == 0) || (_album.DateArrival > _listDateTime.Last<StAlbumDateName>().Date))
                        _listDateTime.Add(new StAlbumDateName(_album.Id, _album.DateArrival));
            }
            TimeSpan _dateOffset;

            int _difference = Math.Max(1, _listDateTime.Last<StAlbumDateName>().Date.Year - _listDateTime.First<StAlbumDateName>().Date.Year) *
                Math.Max(1, (Math.Abs(_listDateTime.Last<StAlbumDateName>().Date.DayOfYear - _listDateTime.First<StAlbumDateName>().Date.DayOfYear)));

            _convSliderDateTime.Mode = DateFormat.EMode.Hour;
            _dateOffset = new TimeSpan(0, 1, 0, 0);

            foreach (StAlbumDateName _tabTime in _listDateTime)
            {
                if ((_listDateTimeOrd.Count == 0) || (_tabTime.Date > (_listDateTimeOrd.Last<StAlbumDateName>().Date + _dateOffset)))
                    _listDateTimeOrd.Add(_tabTime);
            }

            if (_listDateTime.Count > 2)
            {
                _convSliderDateTime.DateNameDisplay = _listDateTime;
                sliderMap.Maximum = _convSliderDateTime.DateNameDisplay.Count - 1;
                sliderMap.Value = sliderMap.Minimum;
            }
            else
            {
                sliderMap.Visibility = Visibility.Collapsed;
            }
        }

        private void sliderEntered(object sender, PointerRoutedEventArgs e)
        {
            SliderSelected = true;
        }

        private void sliderExited(object sender, PointerRoutedEventArgs e)
        {
        //    SliderSelected = false;
        }

        private void backButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(ViewHome));
        }

        private void StopDiaporama()
        {
            btnPlay.IsEnabled = true;
            btnPause.IsEnabled = false;
            _timerDiaporama.Stop();
        }

        private void StartDiaporama()
        {
            btnPlay.IsEnabled = false;
            btnPause.IsEnabled = true;
            _timerDiaporama.Start();
        }

        private void btnMain_Click(object sender, RoutedEventArgs e)
        {
            StopDiaporama();
            DisplayAlbumsList(_tripCurrent.Albums.IndexOf(_albumCurrent), ZOOM_SLOW);
        }

        private async void mainList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if ((sender as ListView).SelectedItem is Image)
            {
                await DisplayPicturesList_FullScreen((int)((sender as ListView).SelectedItem as Image).Tag);

                _appBar.Hide(AppBarPage.SIDE.BOTH);
                if ((sender as ListView).Items.Count > 1)
                {
                    if (_app.AppSettings.LearnInProgress("TOPIC_SWIPE_PLAY") && (_displayOnceFullScreenMode) && (!_displayOnceDiaporamaMode))
                    {
                        Toast.DisplayTwoLines(_res.GetString("Diaporama"), _res.GetString("DiaporamaStart"), "Icons/toastImageAndText.png");
                        _displayOnceDiaporamaMode = true;
                    }
                    if (_app.AppSettings.LearnInProgress("TOPIC_NAVIGATION_LR") && (!_displayOnceFullScreenMode))
                    {
                        Toast.DisplayTwoLines(_res.GetString("FullScreenMode"), _res.GetString("NavigateArrows"), "Icons/toastImageAndText.png");
                        _displayOnceFullScreenMode = true;
                    }
                }
            }
        }

        private void ViewModeUpdate(VIEW_MODE _mode)
        {
            _viewMode = _mode;

            switch (_mode)
            {
                case VIEW_MODE.WORLD:
                    {
                        btnMain.Visibility = Visibility.Collapsed;
                        btnSub.Visibility = Visibility.Collapsed;
                        ImageViewModeActive = false;
                        mainList.Visibility = Visibility.Visible;
                        sliderMap.Visibility = Visibility.Collapsed;
                        imageFullScreen.Visibility = Visibility.Collapsed;
                        _currentIndexFullScreen = -1;
                        break;
                    }
                case VIEW_MODE.ALBUMS:
                    {
                        ClearAllCountriesLayer();
                        btnMain.Visibility = Visibility.Visible;
                        btnSub.Visibility = Visibility.Collapsed;
                        ImageViewModeActive = false;
                        mainList.Visibility = Visibility.Visible;
                        sliderMap.Visibility = Visibility.Visible;
                        imageFullScreen.Visibility = Visibility.Collapsed;
                        mainList.Focus(FocusState.Programmatic);
                        _currentIndexFullScreen = -1;
                        break;
                    }
                case VIEW_MODE.PICTURES:
                    {
                        ClearAllCountriesLayer();
                        btnMain.Visibility = Visibility.Visible;
                        btnSub.Visibility = Visibility.Visible;
                        ImageViewModeActive = false;
                        mainList.Visibility = Visibility.Visible;
                        sliderMap.Visibility = Visibility.Visible;
                        imageFullScreen.Visibility = Visibility.Collapsed;
                        mainList.Focus(FocusState.Programmatic);
                        _currentIndexFullScreen = -1;
                        break;
                    }
                case VIEW_MODE.PICTURE_FULLSCREEN:
                    {
                        ClearAllCountriesLayer();
                        btnMain.Visibility = Visibility.Visible;
                        btnSub.Visibility = Visibility.Visible;
                        ImageViewModeActive = true;
                        mainList.Visibility = Visibility.Collapsed;
                        sliderMap.Visibility = Visibility.Collapsed;
                        imageFullScreen.Visibility = Visibility.Visible;
                        mainList.Focus(FocusState.Programmatic);
                        break;
                    }
            }
        }

        private void mapMain_Tapped(object sender, TappedRoutedEventArgs e)
        {
            StopDiaporama();
            if (_viewMode == VIEW_MODE.PICTURE_FULLSCREEN)
                _viewMode = VIEW_MODE.PICTURES;
            ViewModeUpdate(_viewMode);
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            _app.AppSettings.LearnDone("TOPIC_SWIPE_PLAY");
            _appBar.Hide(AppBarPage.SIDE.BOTH);

            Play();
        }

        private void Play()
        {
            if (_viewMode == VIEW_MODE.PICTURES)
                _diaporamaBase = VIEW_MODE.PICTURE_FULLSCREEN;
            else
                _diaporamaBase = _viewMode;

            if (_viewMode == VIEW_MODE.WORLD)
                _diaporamaState = VIEW_MODE.ALBUMS;
            else if ((_viewMode == VIEW_MODE.ALBUMS) || (_viewMode == VIEW_MODE.PICTURES))
                _diaporamaState = VIEW_MODE.PICTURE_FULLSCREEN;
            
            StartDiaporama();
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            _app.AppSettings.LearnDone("TOPIC_SWIPE_PLAY");
            _appBar.Hide(AppBarPage.SIDE.BOTH);

            StopDiaporama();
        }

        private async void pageRoot_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            e.Handled = true;

            if (MutexBusy())
                return;

            if (e.Key == VirtualKey.Down || e.Key == VirtualKey.Up ||
                e.Key == VirtualKey.Left || e.Key == VirtualKey.Right ||
                e.Key == VirtualKey.Escape || e.Key == VirtualKey.Space ||
                e.Key == VirtualKey.Back || e.Key == VirtualKey.Enter)
            {
                ItemCollection _obj = mainList.Items;
                if ((mainList.SelectedIndex == -1) && (mainList.Items.Count>0))
                    mainList.SelectedIndex = 0;

                if ((_viewMode == VIEW_MODE.ALBUMS) || (_viewMode == VIEW_MODE.PICTURES))
                {
                    int _index = mainList.SelectedIndex;

                    if (((e.Key == VirtualKey.Down) || (e.Key == VirtualKey.Right)) && (mainList.SelectedIndex + 1 < _obj.Count))
                        UpdateSliderValue(_index + 1);
                    else if (((e.Key == VirtualKey.Up) || (e.Key == VirtualKey.Left))  && (mainList.SelectedIndex - 1 >= 0))
                        UpdateSliderValue(_index - 1);
                    else if (_viewMode == VIEW_MODE.ALBUMS)
                    {
                        if (e.Key == VirtualKey.Enter)
                        {
                            if (mainList.SelectedItem is AlbumDescUserControl)
                                await DisplayPicturesList((mainList.SelectedItem as AlbumDescUserControl).DefBlockTrip.Id, 0);
                        }
                        else if (e.Key == VirtualKey.Back)
                            await DisplayWorldLevel(-1, null);
                    }
                    else if (_viewMode == VIEW_MODE.PICTURES)
                    {
                        if (e.Key == VirtualKey.Enter)
                        {
                            if (mainList.SelectedItem is Image)
                                await DisplayPicturesList_FullScreen((int)((sender as ListView).SelectedItem as Image).Tag);
                        }
                        else if (e.Key == VirtualKey.Back)
                            if ((_tripCurrent != null) && (_albumCurrent != null))
                                DisplayAlbumsList(-1, ZOOM_NULL);
                    }
                }
                else if (_viewMode == VIEW_MODE.PICTURE_FULLSCREEN)
                {
                    if (e.Key == VirtualKey.Up)
                        _timerDiaporama.Interval += TimeSpan.FromMilliseconds(1000);
                    else if (e.Key == VirtualKey.Down)
                        _timerDiaporama.Interval -= TimeSpan.FromMilliseconds(1000);
                    else if (e.Key == VirtualKey.Left)
                    {
                        _app.AppSettings.LearnDone("TOPIC_NAVIGATION_LR");
                        StopDiaporama();
                        await DisplayPicturesList_FullScreen(_currentIndexFullScreen - 1);
                    }
                    else if (e.Key == VirtualKey.Right)
                    {
                        _app.AppSettings.LearnDone("TOPIC_NAVIGATION_LR");
                        StopDiaporama();
                        await DisplayPicturesList_FullScreen(_currentIndexFullScreen + 1);
                    }
                    //else if ((e.Key == VirtualKey.Back) || (e.Key == VirtualKey.Escape))
                    //{
                    //    StopDiaporama();
                    //    await DisplayPicturesLevel(_albumCurrent.Id, -1);
                    //}
                    else if (e.Key == VirtualKey.Space)
                    {
                        if (_timerDiaporama.IsEnabled)
                            StopDiaporama();
                        else
                            StartDiaporama();
                    }
                }
                else if (_viewMode == VIEW_MODE.WORLD)
                {
                    if (e.Key == VirtualKey.Back) 
                        this.Frame.Navigate(typeof(ViewHome));
                }
            }
        }

        private async void btnSub_Click(object sender, RoutedEventArgs e)
        {
            StopDiaporama();
            if (_albumCurrent != null)
                await DisplayPicturesList(_albumCurrent.Id, -1);
        }

        private async void imageFullScreen_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (e.Position.X - _movementOrigin < 0)
                await DisplayPicturesList_FullScreen(_currentIndexFullScreen + 1);
            else
                await DisplayPicturesList_FullScreen(_currentIndexFullScreen - 1);
        }

        private void imageFullScreen_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            _movementOrigin = e.Position.X;
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            _app.AppSettings.LearnDone("TOPIC_SWIPE_PLAY");

            _timerDiaporama.Interval += TimeSpan.FromMilliseconds(1000);
            txtDelay.Text = (Convert.ToDecimal(txtDelay.Text) + 1).ToString();
        }

        private void btnRem_Click(object sender, RoutedEventArgs e)
        {
            _app.AppSettings.LearnDone("TOPIC_SWIPE_PLAY");

            int _value = (int)Convert.ToDecimal(txtDelay.Text) - 1;
            if (_value > 0)
            {
                _timerDiaporama.Interval -= TimeSpan.FromMilliseconds(1000);
                txtDelay.Text = _value.ToString();
            }
        }

        private async void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            await SearchAndDisplay(txtSearch.Text);
        }

        private async void txtSearch_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                await SearchAndDisplay(txtSearch.Text);
        }

        private async Task SearchAndDisplay(String _search)
        {
            if (_search.Equals("") || _search.Equals(_res.GetString("SearchLocation")))
                return;

            txtSearch.IsEnabled = false;

            GeocodeRequestOptions requestOptions = new GeocodeRequestOptions(_search, true, false, 1);

            // Make the geocode request 
            SearchManager searchManager = mapMain.SearchManager;

            LocationDataResponse response = await searchManager.GeocodeAsync(requestOptions);
            if (response.LocationData.Count > 0)
            {
                GeocodeLocation _geocodelocation = response.LocationData[0];
                MarkerPosition _marker = new MarkerPosition(this, mapMain.Children, -1, EIcon.IconFlag, new GpsLocation(_geocodelocation.Location), true, 30);
                _marker.Activate();
                mapMain.SetView(_geocodelocation.Location, 4, new TimeSpan(0, 0, ZOOM_SLOW));
            }
            txtSearch.IsEnabled = true;
        }

        private void txtSearch_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (txtSearch.Text.Equals(_res.GetString("SearchLocation")))
            {
                txtSearch.Text = "";
            }
        }

        private async void backWorld_Click(object sender, RoutedEventArgs e)
        {
            StopDiaporama();
            await DisplayWorldLevel(-1, null);
        }

        private void imageTrash_DragEnter(object sender, DragEventArgs e)
        {
            if ((_viewMode == VIEW_MODE.PICTURES) || (_viewMode == VIEW_MODE.ALBUMS))
            {
                imageTrash.Visibility = Visibility.Visible;
                imageTrash.Source = new BitmapImage(new Uri(this.BaseUri, "Icons/trash_full.png"));
            }
        }

        private void imageTrash_DragLeave(object sender, DragEventArgs e)
        {
            imageTrash.Source = new BitmapImage(new Uri(this.BaseUri, "Icons/trash_empty.png"));
        }

        private async void imageTrash_Drop(object sender, DragEventArgs e)
        {
            int _index = 0;
            imageTrash.Visibility = Visibility.Collapsed;
            DataPackageView _package = e.Data.GetView();
            if (_package.Contains("itemIndex"))
                _index = (int)Convert.ToDecimal(await _package.GetTextAsync("itemIndex"));
            else
                return;

            if (_tripCurrent.Sample)
            {
                Toast.DisplayTwoLines(_res.GetString("SampleMode"), _res.GetString("DragDropEnabled"), "Icons/toastImageAndText.png");
                return;
            }

            if ((_viewMode == VIEW_MODE.PICTURES) && (_albumCurrent != null))
            {
                if (_albumCurrent.Pictures.Count < _index)
                    return;

                _listPictures.RemoveAt(_index);
                _albumCurrent.Pictures.RemoveAt(_index);
                if (_albumCurrent.Pictures.Count == 0)
                    _tripCurrent.Albums.Remove(_albumCurrent);
            }
            else if (_viewMode == VIEW_MODE.ALBUMS)
            {
                _listAlbumDesc.RemoveAt(_index);
                _tripCurrent.Albums.RemoveAt(_index);
            }

            if (_tripCurrent.Albums.Count == 0)
            {
                await Trip.DeleteFiles(_tripCurrent.Summary.Id, _listSummary, true);
                
                if (_listSummary.Count > 0)
                    await DisplayWorldLevel(-1, null);
                else
                    this.Frame.Navigate(typeof(ViewHome));
            }
            else
                await Serialization.SerializeToXmlFile<Trip>(_tripCurrent.Id + ".trip", _tripCurrent);
        }

        private void mapMain_DragLeave(object sender, DragEventArgs e)
        {
            imageTrash.Visibility = Visibility.Collapsed;
        }

        private void mapMain_DragEnter(object sender, DragEventArgs e)
        {
            if ((_viewMode == VIEW_MODE.PICTURES) || (_viewMode == VIEW_MODE.ALBUMS))
                imageTrash.Visibility = Visibility.Visible;
        }

        private void mainList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if ((mainList.SelectedItems.Count > 0) && ((_viewMode == VIEW_MODE.PICTURES) || (_viewMode == VIEW_MODE.ALBUMS)))
                imageTrash.Visibility = Visibility.Visible;
            else
                imageTrash.Visibility = Visibility.Collapsed;
        }

        private Boolean InternetConnectionPresent()
        {
            ConnectionProfile profile = NetworkInformation.GetInternetConnectionProfile();
            return ((profile != null) && (profile.NetworkAdapter != null));
        }

        private void AppBarOpened_callback(object sender, object e)
        {
        }

        internal async void CountrySelected(Country country, MarkerPosition markerPosition)
        {
            markerPosition.SetLegendColor(_app.AppSettings.ToggleCountryActive(country.Code));
            await UpdateCountriesLayer();
        }

        private async void mainList_Drop(object sender, DragEventArgs e)
        {
            int _index = 0;
            DataPackageView _package = e.Data.GetView();
            if (_package.Contains("itemIndex"))
                _index = (int)Convert.ToDecimal(await _package.GetTextAsync("itemIndex"));
            else
                return;

            if (_viewMode == VIEW_MODE.WORLD)
            {
                TripDescUserControl _tripDescSrc = null;
                TripDescUserControl _tripDescDest = null;

                if (e.OriginalSource is FrameworkElement)
                {
                    FrameworkElement _temp = e.OriginalSource as FrameworkElement;
                    String _id = _temp.Tag as String;
                    foreach (TripDescUserControl _tripDesc in _listTripDesc)
                    {
                        if (_id == _tripDesc.TripSummary.Id)
                            _tripDescDest = _tripDesc;
                    }
                    if (_listTripDesc.Count >= _index)
                        _tripDescSrc = _listTripDesc[_index];
                }

                if ((_tripDescDest != null) && (_tripDescSrc != null))
                {
                    if (await TripSummary.Merge(_tripDescSrc, _tripDescDest, this))
                    {
                        _listTripDesc.Remove(_tripDescSrc);
                        await Trip.DeleteFiles(_tripDescSrc.TripSummary.Id, _listSummary, true);
                    }
                }
            }
            else if (_viewMode == VIEW_MODE.ALBUMS)
            {
                AlbumDescUserControl _albumDescSrc = null;
                AlbumDescUserControl _albumDescDest = null;

                if (e.OriginalSource is FrameworkElement)
                {
                    FrameworkElement _temp = e.OriginalSource as FrameworkElement;
                    String _id = _temp.Tag as String;
                    foreach (AlbumDescUserControl _albumDesc in _listAlbumDesc)
                    {
                        if (_id == _albumDesc.Summary.Id)
                            _albumDescDest = _albumDesc;
                    }
                    if (_listAlbumDesc.Count >= _index)
                        _albumDescSrc = _listAlbumDesc[_index];
                }

                if ((_albumDescDest != null) && (_albumDescSrc != null))
                {
                    _albumDescSrc.SetActive();
                    _albumDescDest.SetActive();

                    Album _albumSrc = null;
                    Album _albumDest = null;

                    foreach (Album _album in _tripCurrent.Albums)
                        if (_album.Summary.Id == _albumDescSrc.Summary.Id)
                            _albumSrc = _album;

                    foreach (Album _album in _tripCurrent.Albums)
                        if (_album.Summary.Id == _albumDescDest.Summary.Id)
                            _albumDest = _album;

                    if ((_albumDest != null) && (_albumSrc != null))
                    {
                        if (_albumSrc.Summary.Sample || _albumDest.Summary.Sample)
                        {
                            Toast.DisplaySingleLine(_res.GetString("MergeSamples"));
                            return;
                        }
                        // no previous import date exists, ask and import everything
                        Boolean _requestMerge = false;
                        MessageDialog messageDialog;
                        messageDialog = new MessageDialog(_res.GetString("MergeAlbums"));
                        messageDialog.Commands.Add(new UICommand(_res.GetString("No"), (command) => { }));
                        messageDialog.Commands.Add(new UICommand(_res.GetString("Yes"), (command) => { _requestMerge = true; }));
                        messageDialog.CancelCommandIndex = 0;
                        messageDialog.DefaultCommandIndex = 1;
                        await messageDialog.ShowAsync();

                        if (_requestMerge)
                        {
                            //local variables for callback
                            _albumLocal = _albumDest;
                            _albumDesc_Cb = _albumDescSrc;
                            _tripLocal = _tripCurrent;

                            _tripCurrent.RequestMerge(_albumDest, _albumSrc);
                            await _tripCurrent.Update(false, true, CallbackPosition, null, null, this);
                            _albumDescDest.SetInactive();
                            _listAlbumDesc.Remove(_albumDescSrc);
                        }
                        else
                        {
                            _albumDescSrc.SetInactive();
                            _albumDescDest.SetInactive();
                        }
                    }
                }
            }
        }

        public async Task SwitchSynchro(SynchroHandle _syncObj, TripDescUserControl _tripDesc)
        {
            _syncObj.Requested = !_syncObj.Requested;
            _syncObj.Finished = false;

            if (_syncObj.Compression == SynchroManager.ComprLevel.Undefined)
            {
                MessageDialog messageDialog;
                messageDialog = new MessageDialog(_res.GetString("CompressionChooseLevel"),
                    _res.GetString("SynchroWith") + " " + _app.SynchroManager.CloudServerFromName(_syncObj.Server).DisplayName);
                messageDialog.Commands.Add(new UICommand(_res.GetString("CompressionHigh"), (command) => { _syncObj.Compression = SynchroManager.ComprLevel.High; }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("CompressionMedium"), (command) => { _syncObj.Compression = SynchroManager.ComprLevel.Medium; }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("CompressionNo"), (command) => { _syncObj.Compression = SynchroManager.ComprLevel.Original; }));
                messageDialog.CancelCommandIndex = 0;
                messageDialog.DefaultCommandIndex = 2;
                await messageDialog.ShowAsync();
            }

            await SaveSummary();

            _app.SynchroManager.Update(_syncObj);
        }

        public async Task SaveSummary()
        {
            await TripSummary.Save(_listSummary);
        }

        internal void UpdateProgress(string _description, int _progress)
        {
            // update UI
        }

        internal void NavigateToConf(TripSummary _summary)
        {
            ConfObj _obj = new ConfObj(this, _summary, null);
            this.Frame.Navigate(typeof(ViewConf), _obj);
        }

        internal void NavigateToAlbumConf(AlbumSummary _summary)
        {
            ConfObj _obj = new ConfObj(this, _tripCurrent.Summary, _summary);
            this.Frame.Navigate(typeof(ViewConf), _obj);
        }
    }
  
}
