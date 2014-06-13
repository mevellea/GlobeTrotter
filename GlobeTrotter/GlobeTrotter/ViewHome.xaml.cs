using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections;
using System.Globalization;
using System.Collections.ObjectModel;
using Windows.System;
using Windows.Globalization;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;
using Windows.System.UserProfile;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ApplicationSettings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Notifications;
using GlobeTrotter.Common;
using Bing.Maps;
using Bing.Maps.Search;
using NotificationsExtensions;
using NotificationsExtensions.TileContent;
using Windows.Devices.Enumeration;

namespace GlobeTrotter
{
    public sealed partial class ViewHome : Page
    {
        // private
        ObservableCollection<TripDescUserControl> _listTripDesc = new ObservableCollection<TripDescUserControl>();
        ObservableCollection<Image> _listFlags = new ObservableCollection<Image>();
        ObservableDictionary _defaultViewModel = new ObservableDictionary();

        List<TripSummary> _listSummary;
        List<Country> _listCountriesStandard;
        List<String> _listCountries;
        Trip _tripImport;
        uint _distanceCurrent;
        NavigationHelper navigationHelper;
        AppBarPage _appBar;
        ResourceLoader _res;
        int _itemIndexDelete;
        Boolean _importInProgress;
        int _tripNumberUser;
        Flyout _flyoutCountry;
        TextBlock _txtCountry;
        StackPanel _panelCountry;
        Button _btnCountry;
        uint Distance;
        Boolean _connected;
        Boolean _displayOnceWelcome;
        SettingsPane _settingPane;
        ConfigurationPanel _configurationPanel;
        DeviceSelector _selector;
        App _app;

        public ViewHome()
        {
            this.InitializeComponent();

            navigationHelper = new NavigationHelper(this);
            navigationHelper.LoadState += navigationHelper_LoadState;
            navigationHelper.SaveState += navigationHelper_SaveState;

            _res = ResourceLoader.GetForCurrentView();

            btnAddTrip.Label = _res.GetString("AddTrip");
            btnClearTrip.Label = _res.GetString("Reinitialize");
            btnMessage.Label = _res.GetString("Message");
            btnSettings.Label = _res.GetString("Settings");
            btnCamera.Label = _res.GetString("ImportCamera");
            pageTitle.Text = _res.GetString("AppName");
            imageTrash.Source = new BitmapImage(new Uri(this.BaseUri, "Icons/trash_empty.png"));
            imageTrash.Visibility = Visibility.Collapsed;
                        
            gridDisp.ItemsSource = _listTripDesc;

            gridFlag.ItemsSource = _listFlags;
            _listCountries = new List<string>();

            _selector = new DeviceSelector(_res);
            _flyoutCountry = new Flyout();
            _panelCountry = new StackPanel();
            _txtCountry = new TextBlock();
            _btnCountry = new Button();
            _btnCountry.Content = _res.GetString("ViewMap");
            _btnCountry.Click += _btnCountry_Click;
            _panelCountry.Children.Add(_txtCountry);
            _panelCountry.Children.Add(_btnCountry);
            _flyoutCountry.Content = _panelCountry;

            //GooglePlusServer.CreateXml();
        }

        private async void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            // import pictures from camera when entry point is OnActivated
            if ((_app.Device != null) && (!_app.Device.Equals("")))
                await ImportFromCamera(_app.Device);
            else if ((_app.FileActivationFiles != null) && (_app.FileActivationFiles.Count() > 0))
            {
                if (await getImportAuthorization() == false)
                    return;

                IStorageItem _item = _app.FileActivationFiles[0];
                if (_item is StorageFile)
                {
                    StorageFolder _parent = await (_item as StorageFile).GetParentAsync();
                    if (_parent != null)
                        await importLocalAskReorganise(_parent);
                    else
                    {
                        MessageDialog messageDialog = new MessageDialog(_res.GetString("ImportForbidden"));
                        messageDialog.Commands.Add(new UICommand(_res.GetString("Ok"), (command) => { }));
                        messageDialog.CancelCommandIndex = 0;
                        messageDialog.DefaultCommandIndex = 0;
                        await messageDialog.ShowAsync();
                    }
                }
                else if (_item is StorageFolder)
                    await importLocalAskReorganise(_item as StorageFolder);
            }
        }

        private void navigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // get reference to base App
            if (e.Parameter is App)
            {
                _app = (App)e.Parameter;
                _app.SynchroManager.Parent = this;
            }

            if (this.Frame.BackStack.Count > 0)
            {
                PageStackEntry _appRefEntry = this.Frame.BackStack.First<PageStackEntry>(); ;
                if (_appRefEntry.Parameter is App)
                {
                    _app = (App)_appRefEntry.Parameter;
                    _app.SynchroManager.Parent = this;
                }
            }

            _appBar = new AppBarPage(this, AppBarPage.SIDE.BOTH, AppBarOpened_Callback);

            _configurationPanel = new ConfigurationPanel(_app);
            _configurationPanel.AddSwitchCallback(ConfigSwitchChanged_callback);
            _configurationPanel.AddComboBoxCallback(ConfigThemeChanged_callback);

            _settingPane = SettingsPane.GetForCurrentView();
            _settingPane.CommandsRequested += _configurationPanel.OnCommandsRequested;

            AccountsSettingsPane.GetForCurrentView().AccountCommandsRequested += _configurationPanel.AccountCommandsRequested;

            DataTransferManager.GetForCurrentView().DataRequested += OnDataRequested;
            DataTransferManager.GetForCurrentView().TargetApplicationChosen += dataTransferManager_TargetApplicationChosen;

            this.pageRoot.Unloaded += pageRoot_Unloaded;

            UpdateTheme(_app.AppSettings.ThemeColors);

            Boolean _requestUpgrade = false;
            if (Store.IsTrial() && _app.AppSettings.LearnInProgress("TOPIC_VERSION"))
            {
                MessageDialog messageDialog = new MessageDialog(_res.GetString("VersionBasic"), _res.GetString("Thanks"));
                messageDialog.Commands.Add(new UICommand(_res.GetString("NotNow"), (command) => { _app.AppSettings.LearnDone("TOPIC_VERSION"); }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Upgrade"), (command) => { _requestUpgrade = true; }));
                messageDialog.CancelCommandIndex = 0;
                messageDialog.DefaultCommandIndex = 1;
                await messageDialog.ShowAsync();
            }

            if (_requestUpgrade)
                if (await Store.Purchase())
                    _app.AppSettings.LearnDone("TOPIC_VERSION");

            CheckConnectionStatus();

            await loadUserTrip();

            if ((_tripNumberUser == 0) && _app.AppSettings.LearnInProgress("TOPIC_SWIPE_ADD") && (!_displayOnceWelcome))
            {
                Toast.DisplayTwoLines(_res.GetString("Welcome") + " " + _app.AppSettings.FirstName + "!", _res.GetString("ImportSwipe"), "Icons/toastImageAndText.png");
                _displayOnceWelcome = true;
            }

            navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedFrom(e);

            SettingsPane.GetForCurrentView().CommandsRequested -= _configurationPanel.OnCommandsRequested;
            DataTransferManager.GetForCurrentView().DataRequested -= OnDataRequested;
        }

        private void dataTransferManager_TargetApplicationChosen(DataTransferManager sender, TargetApplicationChosenEventArgs args)
        {
            DataTransferManager.GetForCurrentView().DataRequested -= OnDataRequested;
        }

        private void pageRoot_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void _btnCountry_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(ViewMapTrip), (sender as Button).Tag);
        }

        private void loadCountries()
        {
            List<String> _listCountriesRef = new List<string>();
            foreach (String _country in _listCountries)
                _listCountriesRef.Add(_country);

            if (_listCountries == null)
                _listCountries = new List<string>();
            else
                _listCountries.Clear();
            
            foreach (Country _country in _listCountriesStandard)
                if (_app.AppSettings.GetCountryActive(_country.Code))
                    _listCountries.Add(_country.Name);

            foreach (TripDescUserControl _tripDesc in _listTripDesc)
                foreach (String _country in _tripDesc.TripSummary.Countries)
                    if (!_listCountries.Contains(_country))
                        _listCountries.Add(_country);

            foreach (String _country in _listCountriesRef)
                if (!_listCountries.Contains(_country))
                    foreach (Image _flag in _listFlags)
                        if ((_flag.Tag != null) && (_flag.Tag.ToString() == _country))
                        {
                            _listFlags.Remove(_flag);
                            break;
                        }

            foreach (String _country in _listCountries)
                if (!_listCountriesRef.Contains(_country))
                    _listFlags.Insert(0, GetFlag(_country));
        }
        
        private Image GetFlag(String _name)
        {
            Image _image = new Image();
            for (int _idx = 0; _idx < Country.CountryNames.Count(); _idx++)
            {
                if (Country.CountryNames[_idx].Equals(_name.ToUpper()))
                {
                    _image.Source = new BitmapImage(new Uri(this.BaseUri, "Flags/" + Country.CountryCodes[_idx] + ".png"));
                    _image.Tag = Country.CountryNames[_idx];
                    break;
                }
            }
            _image.Width = 30;
            _image.Height = 30;
            _image.Tapped += _flag_Tapped;
            return _image;
        }

        private void _flag_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_importInProgress)
                return;

            if ((sender as Image).Tag != null)
            {
                _txtCountry.Text = _res.GetString("Country") + ": " + (sender as Image).Tag.ToString();
                _txtCountry.Height = 30;
                _btnCountry.Tag = (sender as Image).Tag;
                _flyoutCountry.ShowAt(sender as Image);
            }
            else
                this.Frame.Navigate(typeof(ViewMapTrip), (sender as Image).Tag);
        }

        private void gridDisp_DragStarted(object sender, DragItemsStartingEventArgs e)
        {
            TripDescUserControl _desc = e.Items[0] as TripDescUserControl;
            _itemIndexDelete = _listTripDesc.IndexOf(_desc);

            imageTrash.Source = new BitmapImage(new Uri(this.BaseUri, "Icons/trash_empty.png"));
            imageTrash.Visibility = Visibility.Visible;
        }

        private async void imageTrash_Drop(object sender, DragEventArgs e)
        {
            if (_listTripDesc.Count >= _itemIndexDelete)
            {
                TripDescUserControl _tripDesc = _listTripDesc[_itemIndexDelete];
                await DeleteTrip(_listTripDesc[_itemIndexDelete], true);

                if ((_tripDesc.TripSummary.Sample) && (_tripDesc.TripSummary.SampleId == 1))
                    _app.AppSettings.LearnDone("TOPIC_SAMPLE_A");
                else if ((_tripDesc.TripSummary.Sample) && (_tripDesc.TripSummary.SampleId == 2))
                    _app.AppSettings.LearnDone("TOPIC_SAMPLE_B");
                rewards();
            }
            imageTrash.Visibility = Visibility.Collapsed;
            _app.AppSettings.LearnDone("TOPIC_DRAG_DELETE");
        }

        private async Task loadUserTrip()
        {
            _listSummary = await TripSummary.Load();
            _tripNumberUser = _listSummary.Count;

            StorageFile _fileCountries = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/CountryDefinition.desc"));
            _listCountriesStandard = Serialization.DeserializeFromXmlFile<List<Country>>(_fileCountries.Path);

            if (_tripNumberUser == 0)
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

            if (_listSummary.Count == 0)
                _appBar.Show(AppBarPage.SIDE.BOTH);
            else if (_listSummary.Count > 0)
            {
                List<TripSummary> _tripSummOrd = _listSummary.OrderByDescending<TripSummary, DateTime>(o => o.DateArrival).ToList<TripSummary>();

                foreach (TripSummary _summary in _tripSummOrd)
                {
                    // displayed trip description block
                    TripDescUserControl blk = new TripDescUserControl(this, _app.AppSettings.GetConfig("CONFIG_MILES"), _app.AppSettings.ThemeColors);
                    blk.DefBlockTrip = _summary;
                    _listTripDesc.Add(blk);
                    Distance += _summary.Distance;
                }
            }
            StorageFile _imageAccount = UserInformation.GetAccountPicture(AccountPictureKind.SmallImage) as StorageFile;
#if DEBUG
            _imageAccount = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///assets/user.png"));
            _app.AppSettings.DisplayName = _res.GetString("Guest");
#endif
            IRandomAccessStream _stream = await _imageAccount.OpenReadAsync();
            BitmapImage _bmp = new BitmapImage();

            _bmp.SetSource(_stream);
            profilePicture.Source = _bmp;
            profilePicture.Visibility = Visibility.Visible;

            profileName.Text = _app.AppSettings.DisplayName + " >";

            rewards();

            foreach (TripDescUserControl _tripDesc in _listTripDesc)
                _app.SynchroManager.UpdateAll(_tripDesc.DefBlockTrip);
        }

        private void rewards()
        {
            loadCountries();

            if (_listSummary.Count > 0)
                TileHomePage.UpdateDisplay(
                    _res.GetString("AppName"),
                    "ms-appx:///Square310x310Logo.scale-100.png",
                    _listSummary.Last<TripSummary>().Sample,
                    _listSummary.Last<TripSummary>().PathThumb,
                    _listSummary.Last<TripSummary>().PicturesThumb,
                    _listCountries);
            else
                TileHomePage.UpdateDisplay(
                    _res.GetString("AppName"),
                    "ms-appx:///Square310x310Logo.scale-100.png",
                    false, null, null, null);

            if (_listCountries.Count == 0)
                txtCountriesNumber.Visibility = Visibility.Collapsed;
            else
            {
                txtCountriesNumber.Visibility = Visibility.Visible;
                txtCountriesNumber.Text = _listCountries.Count + " " + _res.GetString("CountriesVisited");
            }

            if (Distance == 0)
                txtDistance.Visibility = Visibility.Collapsed;
            else
            {
                txtDistance.Text = Gps.GetDistanceFormat(Distance, _app.AppSettings.GetConfig("CONFIG_MILES"));
                txtDistance.Visibility = Visibility.Visible;
            }
        }

        private async Task<Boolean> getImportAuthorization()
        {
            Boolean _requestUpgrade = false;
            Boolean _requestExit = false;

            if (_importInProgress)
                return false;

            CheckConnectionStatus();
#if !DEBUG
            if (!_connected)
            {
                MessageDialog messageDialog = new MessageDialog(_res.GetString("ConnectionInformation"), _res.GetString("NoConnection"));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Ok"), (command) => { _requestExit = true; }));
                messageDialog.CancelCommandIndex = 0;
                messageDialog.DefaultCommandIndex = 0;
                await messageDialog.ShowAsync();
            }
            if (_requestExit)
                return false;
#endif
            if ((Store.IsTrial() && (_tripNumberUser >= 2)))
            {
                MessageDialog messageDialog = new MessageDialog(_res.GetString("TrialDescription"), _res.GetString("TrialVersion"));
                messageDialog.Commands.Add(new UICommand(_res.GetString("NotNow"), (command) => { _requestExit = true; }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Upgrade"), (command) => { _requestUpgrade = true; }));
                messageDialog.CancelCommandIndex = 0;
                messageDialog.DefaultCommandIndex = 1;
                await messageDialog.ShowAsync();
            }
            
            if ((_requestExit) || (_requestUpgrade && !(await Store.Purchase())))
                return false;

            if (_connected && _app.AppSettings.GetConfig("CONFIG_WARNING") && (Connection.MeteredOrLimited()))
            {
                MessageDialog messageDialog = new MessageDialog(_res.GetString("LimitedConnection"), _res.GetString("Warning"));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Cancel"), (command) => { _requestExit = true; }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Continue"), (command) => { }));
                messageDialog.CancelCommandIndex = 0;
                messageDialog.DefaultCommandIndex = 0;
                await messageDialog.ShowAsync();
            }

            if (_requestExit)
                return false;
            else
                return true;
        }

        private async Task importLocalAskReorganise(StorageFolder _folderBase)
        {
            int _reorganize = -1;

            if (_app.AppSettings.Reorganize == -1)
                _reorganize = 0;
            else if (_app.AppSettings.Reorganize == 1)
                _reorganize = 1;
            else
            {
                MessageDialog messageDialog = new MessageDialog(_res.GetString("RenameQuestion"), _res.GetString("AutomaticRename"));
                messageDialog.Commands.Add(new UICommand(_res.GetString("No"), (command) => { _reorganize = 0; }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Yes"), (command) => { _reorganize = 1; }));
                messageDialog.CancelCommandIndex = 0;
                messageDialog.DefaultCommandIndex = 1;
                await messageDialog.ShowAsync();
            }

            _importInProgress = true;
            importFromFolder(_folderBase, (_reorganize == 1));
        }

        private async void btnImport_Click(object sender, RoutedEventArgs e)
        {
            if (await getImportAuthorization() == false)
                return;

            FolderPicker _folderPicker = new FolderPicker();
            _folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            _folderPicker.ViewMode = PickerViewMode.Thumbnail;
            _folderPicker.SettingsIdentifier = "pickerInit";
            _folderPicker.CommitButtonText = _res.GetString("SelectFolder");
            foreach (String _ext in Picture.Extensions)
                _folderPicker.FileTypeFilter.Add(_ext);

            StorageFolder _folderBase = await _folderPicker.PickSingleFolderAsync();

            if (_folderBase != null)
                await importLocalAskReorganise(_folderBase);
        }

        private void importFromFolder(StorageFolder _folderBase, Boolean _reorganize)
        {
            _tripImport = new Trip();
            _tripImport.Import(_folderBase, _reorganize, this, ImportStepEnd);
        }

        public void ProgressUpdate(String _text, int _percent)
        {
            ringImport.IsActive = true;
            progressBar.Visibility = Visibility.Visible;
            txtImport.Visibility = Visibility.Visible;
            progressBar.Value = _percent;
            txtImport.Text = _text;
            UpdateLayout();
        }

        public void ProgressFinished(String _text)
        {
            ringImport.IsActive = false;
            progressBar.Visibility = Visibility.Collapsed;
            if ((_text != null) && (_text != ""))
                txtImport.Text = _text;
            else
                txtImport.Visibility = Visibility.Collapsed;
        }

        private async void ImportStepEnd()
        {
            StopImport();

            _tripNumberUser++;
            _distanceCurrent = Distance;

            foreach (String _country in _tripImport.GetCountriesList())
                _tripImport.Summary.AddCountry(_country);

            if (_app.AppSettings.LearnInProgress("TOPIC_SAMPLE_A"))
            {
                _app.AppSettings.LearnDone("TOPIC_SAMPLE_A");
                foreach (TripDescUserControl _tripdesc in _listTripDesc)
                {
                    if (_tripdesc.DefBlockTrip.SampleId == 1)
                    {
                        await DeleteTrip(_tripdesc, false);
                        break;
                    }
                }
            }

            if (_app.AppSettings.LearnInProgress("TOPIC_SAMPLE_B"))
            {
                _app.AppSettings.LearnDone("TOPIC_SAMPLE_B");
                Toast.DisplayTwoLines(_res.GetString("FirstImport"), _res.GetString("SampleRemoved"), "Icons/toastImageAndText.png");
                foreach (TripDescUserControl _tripdesc in _listTripDesc)
                {
                    if (_tripdesc.DefBlockTrip.SampleId == 2)
                    {
                        await DeleteTrip(_tripdesc, true);
                        break;
                    }
                }
            }
            _app.AppSettings.LearnDone("TOPIC_SWIPE_ADD");

            _listSummary.Add(_tripImport.Summary);

            // save trip and summary
            await SaveSummary();

            // displayed trip description block
            TripDescUserControl blk = new TripDescUserControl(this, _app.AppSettings.GetConfig("CONFIG_MILES"), _app.AppSettings.ThemeColors);
            blk.DefBlockTrip = _tripImport.Summary;
            _listTripDesc.Insert(0, blk);
                
            txtDistance.Text = Gps.GetDistanceFormat(Distance, _app.AppSettings.GetConfig("CONFIG_MILES"));
            rewards();
        }

        public void StopImport()
        {
            _importInProgress = false;
        }

        public void TripDescUserControlSelected(TripDescUserControl _tripDesc)
        {
            if (_importInProgress)
            {
                _tripDesc.SetInactive();
                return;
            }
            imageTrash.Visibility = Visibility.Collapsed; 
            ringImport.IsActive = true;

            this.Frame.Navigate(typeof(ViewMapTrip), _tripDesc);
        }

        private void gridDisp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gridDisp.SelectedItems.Count > 0)
                imageTrash.Visibility = Visibility.Visible;
            else
                imageTrash.Visibility = Visibility.Collapsed;
        }

        private void gridDisp_PointerPressed(object sender, HoldingRoutedEventArgs e)
        {
            imageTrash.Visibility = Visibility.Visible;
        }

        private async Task<Boolean> DeleteTrip(TripDescUserControl _tripDesc, Boolean _save)
        {
            if (_tripDesc == null)
                return false;

            if (!(_tripDesc.TripSummary.Sample) && _tripDesc.TripSummary.SyncRequested())
            {
                Boolean _requestDeleteCloud = false;
                Boolean _requestCancel = false;
                Boolean _status;

                MessageDialog messageDialog = new MessageDialog(_res.GetString("DeleteCloud"), _tripDesc.TripSummary.FolderTopName);
                messageDialog.Commands.Add(new UICommand(_res.GetString("Cancel"), (command) => { _requestCancel = true; }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("No"), (command) => { }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Yes"), (command) => { _requestDeleteCloud = true; }));
                messageDialog.CancelCommandIndex = 0;
                messageDialog.DefaultCommandIndex = 2;
                await messageDialog.ShowAsync();

                if (_requestCancel)
                    return false;

                if (_requestDeleteCloud)
                {
                    _status = await _app.SynchroManager.DeleteAll(_tripDesc);
                    if (!_status)
                    {
                        messageDialog = new MessageDialog(_res.GetString("DeleteCloudFailed"));
                        messageDialog.Commands.Add(new UICommand(_res.GetString("Ok"), (command) => { }));
                        await messageDialog.ShowAsync();
                    }
                }
            }

            _listTripDesc.Remove(_tripDesc);

            Distance -= _tripDesc.TripSummary.Distance;

            if (!_tripDesc.DefBlockTrip.Sample)
            {
                _tripNumberUser--;
                await Trip.DeleteFiles(_tripDesc.TripSummary.Id, _listSummary, _save);
            }
            rewards();

            return true;
        }

        private async void imageTrash_Tapped(object sender, RoutedEventArgs e)
        {
            await DeleteTrip(gridDisp.SelectedItem as TripDescUserControl, true);
            imageTrash.Visibility = Visibility.Collapsed; 
            rewards();
        }

        private async void pageRoot_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            e.Handled = true;
            if (e.Key == VirtualKey.Delete)
            {
                await DeleteTrip(gridDisp.SelectedItem as TripDescUserControl, true);
                rewards();
            }
            else if (e.Key == VirtualKey.Enter)
                TripDescUserControlSelected((sender as ListView).SelectedItem as TripDescUserControl);
        }

        private void profileSelect(object sender, TappedRoutedEventArgs e)
        {
        }

        private void pageRoot_KeyDown2(object sender, KeyRoutedEventArgs e)
        {
            pageRoot_KeyDown(sender, e);
        }

        private async void btnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_importInProgress)
                return;

            _appBar.Hide(AppBarPage.SIDE.BOTH);

            Boolean _requestReset = true;

            if (_listTripDesc.Count > 0)
            {
                var messageDialog = new MessageDialog(_res.GetString("WarningDeleteMsg"), _res.GetString("Warning"));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Cancel"), (command) => { _requestReset = false; }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("DeleteYes"), (command) => { }));
                messageDialog.CancelCommandIndex = 0;
                messageDialog.DefaultCommandIndex = 1;
                await messageDialog.ShowAsync();
            }

            if (!_requestReset)
                return;
            
            int _count = _listTripDesc.Count;
            for (int _idx = 0; _idx < _count; _idx++)
            {
                Boolean _status = await DeleteTrip(_listTripDesc.Last<TripDescUserControl>(), false);
                if (!_status)
                    return;
            }
            _app.AppSettings.Clear();

            await SaveSummary();
            
            _app.AppSettings.LearnDone("TOPIC_VERSION");
            _app.AppSettings.LearnDone("TOPIC_SWIPE_ADD");
            UpdateTheme(_app.AppSettings.ThemeColors);

            await loadUserTrip();
        }

        private void gridDisp_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (gridDisp.SelectedItems.Count > 0)
                imageTrash.Visibility = Visibility.Visible;
            else
                imageTrash.Visibility = Visibility.Collapsed;
        }

        private void btnMessage_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs e)
        {
            GetShareContent(e.Request);
        }

        private bool GetShareContent(DataRequest request)
        {
            bool succeeded = false;

            string dataPackageText = _res.GetString("Feedback") + " <" + Website.Email + ">";
            if (!String.IsNullOrEmpty(dataPackageText))
            {
                DataPackage requestData = request.Data;
                requestData.Properties.Title = _res.GetString("FeedbackTitle");
                requestData.Properties.ContentSourceApplicationLink = ApplicationLink;
                requestData.SetText(dataPackageText);
                succeeded = true;
            }
            return succeeded;
        }

        private Uri ApplicationLink
        {
            get
            {
                return GetApplicationLink(GetType().Name);
            }
        }

        public static Uri GetApplicationLink(string sharePageName)
        {
            return new Uri("ms-sdk-sharesourcecs:navigate?page=" + sharePageName);
        }

        private void ToggleAppBarButton(bool showPinButton)
        {
            if (btnPin != null)
            {
                if (showPinButton)
                {
                    btnPin.Style = (this.Resources["PinAppBarButtonStyle"] as Style);
                    btnPin.Label = "epingler";
                }
                else
                {
                    btnPin.Style = (this.Resources["UnpinAppBarButtonStyle"] as Style);
                    btnPin.Label = "supprimer";
                }
            }
        }

        private async void btnPin_Click(object sender, RoutedEventArgs e)
        {
            if (!TileHomePage.SecondaryTileExist())
            {
                Boolean _pinned = await TileHomePage.PinSecondaryTile((FrameworkElement)sender);
                ToggleAppBarButton(!_pinned);
            }
            else
            {
                Boolean _unpinned = await TileHomePage.UnpinSecondaryTile((FrameworkElement)sender);
                ToggleAppBarButton(_unpinned);
            }
        }

        private void imageTrash_DragEnter(object sender, DragEventArgs e)
        {
            imageTrash.Source = new BitmapImage(new Uri(this.BaseUri, "Icons/trash_full.png"));
        }

        private void imageTrash_DragLeave(object sender, DragEventArgs e)
        {
            imageTrash.Source = new BitmapImage(new Uri(this.BaseUri, "Icons/trash_empty.png"));
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            _appBar.Hide(AppBarPage.SIDE.BOTH);
             _configurationPanel.DisplayGeneralPanel();
        }

        private void ConfigThemeChanged_callback(object sender, SelectionChangedEventArgs args)
        {
            ComboBox _input = sender as ComboBox;
            Theme.EName _theme = (Theme.EName)_input.SelectedIndex;

            UpdateTheme(_theme);
        }

        private void ConfigSwitchChanged_callback(object sender, RoutedEventArgs args)
        {
            ToggleSwitch _input = sender as ToggleSwitch;

            if (_input.Name.Equals("CONFIG_MILES"))
            {
                foreach (TripDescUserControl _userControl in _listTripDesc)
                    _userControl.UpdateDistance(_input.IsOn);

                txtDistance.Text = Gps.GetDistanceFormat(Distance, _input.IsOn);
            }
        }
        
        private void ConfigComboChanged_callback(object sender, SelectionChangedEventArgs args)
        {
            loadCountries();
        }

        private void AppBarOpened_Callback(object sender, object e)
        {
            CheckConnectionStatus();
            _configurationPanel.Hide();
            ToggleAppBarButton(!TileHomePage.SecondaryTileExist());
        }

        private void btnRate_Click(object sender, RoutedEventArgs e)
        {
            //Store.AppUri();
        }
                
        private async void btnCamera_Click(object sender, RoutedEventArgs e)
        {
            _app.AppSettings.LearnDone("TOPIC_SWIPE_ADD");

            if (await getImportAuthorization() == false)
                return;
            
            await _selector.ShowPopupDevice(ImportFromCamera);
        }

        private async Task ImportFromCamera(String _id)
        {
            Boolean _requestExit = false;
            int _newPicturesNumber = 0;

            _appBar.Hide(AppBarPage.SIDE.BOTH);

            ProgressUpdate(_res.GetString("SearchPictures"), 0);
            
            IReadOnlyList<StorageFile> _listNewFiles = await _selector.GetImagesFromStorageAsync(_id);
            String _cameraName = _selector.GetNameFromId(_id);

            if (_listNewFiles == null)
            {
                ProgressFinished(_res.GetString("NoNewImage"));
                return;
            }
            else
                ProgressFinished("");

            DateTime _dateFirst = _listNewFiles.First<StorageFile>().DateCreated.DateTime;
            DateTime _dateLast = _listNewFiles.Last<StorageFile>().DateCreated.DateTime;
            DateTime _dateLastImport = _app.AppSettings.GetDateLastImportCamera(_cameraName);
            DateTime _dateRef = DateTime.Now;

            if (_dateLastImport.Ticks != 0) 
            {
                // previous import date exists
                if ((_dateLastImport > _dateFirst) && (_dateFirst < _dateLast))
                {
                    foreach (StorageFile _file in _listNewFiles)
                        if (_file.DateCreated > _dateLastImport)
                            _newPicturesNumber++;

                    ProgressUpdate(_newPicturesNumber + " " + _res.GetString("ImagesFound"), 0);

                    if (_newPicturesNumber > 0)
                    {
                        // new images found
                        MessageDialog messageDialog;
                        messageDialog = new MessageDialog(_res.GetString("ImportSince") + " " + _dateLastImport.ToString("yyyy-MM-dd") + "?", _newPicturesNumber + " " + _res.GetString("ImagesFound"));
                        messageDialog.Commands.Add(new UICommand(_res.GetString("Cancel"), (command) => { _requestExit = true; }));
                        messageDialog.Commands.Add(new UICommand(_res.GetString("Ok"), (command) => { _dateRef = _dateLastImport; }));
                        messageDialog.CancelCommandIndex = 0;
                        messageDialog.DefaultCommandIndex = 1;
                        await messageDialog.ShowAsync();
                    }
                    else
                        _requestExit = true;
                }
                else
                    ProgressFinished(_res.GetString("NoNewImage"));
            }
            else
            {
                _newPicturesNumber = _listNewFiles.Count;

                if (_newPicturesNumber == 0)
                {
                    ProgressFinished(_res.GetString("NoNewImage"));
                    return;
                }
                else
                    ProgressUpdate(_newPicturesNumber + " " + _res.GetString("ImagesFound"), 100);

                // no previous import date exists, ask and import everything
                MessageDialog messageDialog;
                messageDialog = new MessageDialog(_res.GetString("ImportFirst"), _newPicturesNumber + " " + _res.GetString("ImagesFound"));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Cancel"), (command) => { _requestExit = true; }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Ok"), (command) => { _dateRef = _dateFirst; }));
                messageDialog.CancelCommandIndex = 0;
                messageDialog.DefaultCommandIndex = 1;
                await messageDialog.ShowAsync();
            }

            if (_requestExit)
            {
                _importInProgress = false;
                ProgressFinished("");
                return;
            }

            int _index = 0;

            StorageFolder _folderOut = await KnownFolders.PicturesLibrary.CreateFolderAsync(DateTime.Now.ToString("yyyy-MM-dd") + " - Import from " + _cameraName, CreationCollisionOption.GenerateUniqueName);

            foreach (StorageFile _file in _listNewFiles)
            {
                if (_file.DateCreated > _dateRef)
                {
                    ProgressUpdate(_res.GetString("ImportPicture") + " " + _index + " " + _res.GetString("of") + " " + _newPicturesNumber,
                        100 * ++_index / _newPicturesNumber);
                    await _file.CopyAsync(_folderOut, _file.Name, NameCollisionOption.ReplaceExisting);
                }
            }
            // save curent DateTime so it is ready for next import
            _app.AppSettings.SetDateLastImportCamera(_cameraName);
             
            ProgressFinished(_res.GetString("ImportFinished"));

            // copy to local storage finished, start standard import procedure
            if (await getImportAuthorization() == false)
                return;

            _importInProgress = true;
            importFromFolder(_folderOut, true);
        }
        
        private async void gridDisp_Drop(object sender, DragEventArgs e)
        {
            TripDescUserControl _tripDescSrc;
            TripDescUserControl _tripDescDest;

            if (e.OriginalSource is FrameworkElement)
            {
                FrameworkElement _temp = e.OriginalSource as FrameworkElement;
                if (_temp.DataContext is TripDescUserControl)
                    _tripDescDest = _temp.DataContext as TripDescUserControl;
                else
                    return;
            }
            else
                return;

            if (_listTripDesc.Count >= _itemIndexDelete)
                _tripDescSrc = _listTripDesc[_itemIndexDelete];
            else
                return;

            _importInProgress = true;
            imageTrash.Visibility = Visibility.Collapsed;
            ringImport.IsActive = true;
            _app.AppSettings.LearnDone("TOPIC_DRAG_DELETE");

            if (await TripSummary.Merge(_tripDescSrc, _tripDescDest, this))
                await DeleteTrip(_tripDescSrc, true);

            ProgressFinished("");
            _importInProgress = false;
        }

        private void imageTrash_Tapped(object sender, TappedRoutedEventArgs e)
        {
            imageTrash_Drop(null, null);
        }

        public ObservableDictionary DefaultViewModel
        {
            get { return this._defaultViewModel; }
        }

        private void CheckConnectionStatus()
        {
            if (Connection.InternetAccess())
            {
                _connected = true;
                imageHorsLigne.Visibility = Visibility.Collapsed;
            }
            else
            {
                _connected = false;
                imageHorsLigne.Visibility = Visibility.Visible;
                if (CultureInfo.CurrentCulture.Parent.Name == "fr")
                    imageHorsLigne.Source = new BitmapImage(new Uri(this.BaseUri, "Assets/hors-ligne.png"));
                else
                    imageHorsLigne.Source = new BitmapImage(new Uri(this.BaseUri, "Assets/not-connected.png"));
            }
        }

        private void UpdateTheme(Theme.EName _name)
        {
//          bgViewHome.Background = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.MainBg));
            bgViewHome.Background = Theme.GetPictureFromTheme(_name);
            bgHomePanel.Background = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.MainFront));
            pageTitle.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.MainFront));
            bgList.Color = Theme.GetColorFromTheme(_name, Theme.EColorPalet.MainBgDegr);

            profileName.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.Text1));
            txtDistance.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.Text5));
            txtCountriesNumber.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.Text5));

            txtImport.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.MainBgDegr));
            progressBar.Background = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.MainBgDegr));
            progressBar.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.Text2));
            ringImport.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.MainBgDegr));

            foreach (TripDescUserControl _uc in _listTripDesc)
                _uc.UpdateTheme(_name);
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

        private void imageHorsLigne_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CheckConnectionStatus();
        }

        public void NavigateToConf(TripSummary _summary)
        {
            ConfObj _obj = new ConfObj(this, _summary, null);
            this.Frame.Navigate(typeof(ViewConf), _obj);
        }
    }
}
