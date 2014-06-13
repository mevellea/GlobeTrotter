using GlobeTrotter.Common;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.Resources;
using Windows.Storage;

// Pour en savoir plus sur le modèle d'élément Page de base, consultez la page http://go.microsoft.com/fwlink/?LinkId=234237

namespace GlobeTrotter
{
    /// <summary>
    /// Page de base qui inclut des caractéristiques communes à la plupart des applications.
    /// </summary>
    public sealed partial class ViewConf : Page
    {
        TripSummary TripSummaryRef;
        AlbumSummary AlbumSummaryRef;
        Trip TripRef;
        Page PageRef;

        Boolean _changeInProgress;
        Boolean _initFinished;
        Boolean _listLoaded;
        Boolean _importInProgress;

        NavigationHelper navigationHelper;
        ObservableDictionary defaultViewModel = new ObservableDictionary();
        ObservableCollection<AlbumConfUserControl> listAlbums = new ObservableCollection<AlbumConfUserControl>();
        ResourceLoader _res;

        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        public ViewConf()
        {
            this.InitializeComponent();
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += navigationHelper_LoadState;
            this.navigationHelper.SaveState += navigationHelper_SaveState;
      
            _res = ResourceLoader.GetForCurrentView();

            ProgressUpdate(_res.GetString("Loading"), 0);

            ComboDropbox.Items.Add(_res.GetString("CompressionHigh"));
            ComboDropbox.Items.Add(_res.GetString("CompressionMedium"));
            ComboDropbox.Items.Add(_res.GetString("CompressionNo"));

            ComboUsb.Items.Add(_res.GetString("CompressionHigh"));
            ComboUsb.Items.Add(_res.GetString("CompressionMedium"));
            ComboUsb.Items.Add(_res.GetString("CompressionNo"));

            lblPath.Text = _res.GetString("ConfLblPath");
            lblAlbNum.Text = _res.GetString("ConfLblAlbNum");
            lblPicNum.Text = _res.GetString("ConfLblPicNum");
            lblName.Text = _res.GetString("ConfLblName");
            lblLocation.Text = _res.GetString("ConfLblLocation");
            pageTitle.Text = _res.GetString("ConfTitle");

            ChkRename.Content = _res.GetString("ConfAskRename");
            ChkDropbox.Content = _res.GetString("ConfAskDropbox");
            ChkUsb.Content = _res.GetString("ConfAskUsb");
            btnUpdate.Content = _res.GetString("Save");
        }

        private void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
        }

        private void navigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
        }

        #region Inscription de NavigationHelper

        void TextBoxReadOnly(TextBox _textBox, Boolean _readOnly)
        {
            Thickness _th;
            if (_readOnly)
                _th = new Thickness(0);
            else
                _th = new Thickness(2);

            _textBox.BorderThickness = _th;
            _textBox.IsReadOnly = _readOnly;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            _initFinished = false;
            _listLoaded = false;

            if (e.Parameter is ConfObj)
            {
                TripSummaryRef = (e.Parameter as ConfObj).TripSummary;
                AlbumSummaryRef = (e.Parameter as ConfObj).AlbumSummary;
                PageRef = (e.Parameter as ConfObj).Page;
            }
            else
                throw new Exception();

            TripRef = await Trip.LoadFromSummary(TripSummaryRef);

            if ((TripRef.Sample) || !(PageRef is ViewHome))
            {
                TextBoxReadOnly(TripName, true);
                TextBoxReadOnly(TripLocation, true);
            }

            // config from summary
            TripLocation.Text = TripSummaryRef.LocationFromTo;

            if (TripRef.Sample)
            {
                TripName.Text = TripSummaryRef.LocationMain;
                TripPath.Text = "Sample";
                btnUpdate.IsEnabled = false;
            }
            else
            {
                TripName.Text = TripSummaryRef.FolderTopName;
                TripPath.Text = TripRef.PathRoot;
            }

            // config from trip
            AlbumNum.Text = TripRef.Albums.Count().ToString();
            ChkRename.IsChecked = TripRef.Reorganize;

            ChkDropbox.IsChecked = TripSummaryRef.SyncDropbox.Requested;
            UpdateCheckSync(TripSummaryRef.SyncDropbox, ComboDropbox, ChkDropbox);

            ChkUsb.IsChecked = TripSummaryRef.SyncUsb.Requested;
            UpdateCheckSync(TripSummaryRef.SyncUsb, ComboUsb, ChkUsb);

            UpdateLayout();

            // used for selection
            AlbumConfUserControl _itemRef = null;

            int _count = 0;
            foreach (Album _album in TripRef.Albums)
            {
                _count += _album.Pictures.Count;
                AlbumConfUserControl _userControl = new AlbumConfUserControl(_album, NotifyChange);
                listAlbums.Add(_userControl);

                if ((AlbumSummaryRef != null) && (_album.Id == AlbumSummaryRef.Id))
                    _itemRef = _userControl;
            }
            PicNum.Text = _count.ToString();

            tableAlbums.ItemsSource = listAlbums;

            if (_itemRef != null)
            {
                tableAlbums.SelectedItem = _itemRef;
                tableAlbums.ScrollIntoView(_itemRef, ScrollIntoViewAlignment.Leading);
            }

            _initFinished = true;
            ProgressFinished("");

            navigationHelper.OnNavigatedTo(e);
        }

        private async void backButton_Click(object sender, RoutedEventArgs e)
        {
            if (_importInProgress)
                return;

            if (_changeInProgress && !TripRef.Sample)
            {
                bool _requestUpdate = false;
                MessageDialog messageDialog;
                messageDialog = new MessageDialog(_res.GetString("LeaveWithoutUpdate"), _res.GetString("Warning"));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Ignore"), (command) => { }));
                messageDialog.Commands.Add(new UICommand(_res.GetString("Save"), (command) => { _requestUpdate = true; }));
                messageDialog.CancelCommandIndex = 0;
                messageDialog.DefaultCommandIndex = 1;
                await messageDialog.ShowAsync();

                try
                {
                    if (_requestUpdate)
                        await SaveConf();
                }
                catch (Exception error)
                {
                    Debug.WriteLine(error.Message);
                }
            }
            this.Frame.GoBack();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedFrom(e);
        }

        #endregion

        async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SaveConf();
            }
            catch (Exception error)
            {
                Debug.WriteLine(error.Message);
            }
        }

        private async Task SaveConf()
        {
            if (_importInProgress)
                return;

            if (TripRef.Sample)
            {
                Toast.DisplayTwoLines(_res.GetString("SampleMode"), _res.GetString("NoSampleModification"), "Icons/toastImageAndText.png");
                return;
            }

            _changeInProgress = false;

            StartImport();
        
            // modification of summary from ViewHome only
            if (PageRef is ViewHome)
            {
                TripSummaryRef.FolderTopName = TripName.Text;
                TripSummaryRef.LocationMain = TripName.Text;
                TripSummaryRef.LocationFromTo = TripLocation.Text;
                TripSummaryRef.SyncDropbox.Requested = (bool)ChkDropbox.IsChecked;
                TripSummaryRef.SyncDropbox.Compression = SynchroManager.IntToCompr(ComboDropbox.SelectedIndex);
                TripSummaryRef.SyncUsb.Requested = (bool)ChkUsb.IsChecked;
                TripSummaryRef.SyncUsb.Compression = SynchroManager.IntToCompr(ComboUsb.SelectedIndex);

                // save summary
                await (PageRef as ViewHome).SaveSummary();
            }

            TripRef.Summary.FolderTopName = TripName.Text;

            // modification of Trip
            foreach (AlbumConfUserControl _albumConf in listAlbums)
            {
                if (_albumConf.AlbumRef.DisplayName != _albumConf.NameTopRef.Text)
                {
                    // was changed bu user
                    _albumConf.AlbumRef.DisplayName = _albumConf.NameTopRef.Text;
                    _albumConf.AlbumRef.Summary.StrLocation = _albumConf.NameTopRef.Text;
                    _albumConf.AlbumRef.Summary.StrLocationShort = _albumConf.NameTopRef.Text;
                }
                _albumConf.AlbumRef.DateArrival = _albumConf.DateTopRef.Date.Date;
                _albumConf.AlbumRef.PositionPresent = (Boolean)_albumConf.CheckBoxRef.IsChecked;
            }

            await TripRef.Update(true, false, null, null, null, this);
        }

        private void UpdateCheckSync(SynchroHandle _handle, ComboBox _comboBox, CheckBox _checkBox)
        {
            if (_handle.Compression == SynchroManager.ComprLevel.Undefined)
                _handle.Compression = SynchroManager.ComprLevel.Original;

            _comboBox.SelectedIndex = SynchroManager.ComprToInt(TripSummaryRef.SyncUsb.Compression);
            _comboBox.Visibility = (Boolean)_checkBox.IsChecked ? Visibility.Visible : Visibility.Collapsed;

            if ((TripRef.Sample) || !(PageRef is ViewHome))
            {
                _comboBox.IsEnabled = false;
                _checkBox.IsEnabled = false;
            }
        }

        private void ChkDropbox_Checked(object sender, RoutedEventArgs e)
        {
            TripSummaryRef.SyncDropbox.Requested = (Boolean)ChkDropbox.IsChecked;
            UpdateCheckSync(TripSummaryRef.SyncDropbox, ComboDropbox, ChkDropbox);
            NotifyChange();
        }

        private void ChkUsb_Checked(object sender, RoutedEventArgs e)
        {
            TripSummaryRef.SyncUsb.Requested = (Boolean)ChkUsb.IsChecked;
            UpdateCheckSync(TripSummaryRef.SyncUsb, ComboUsb, ChkUsb);
            NotifyChange();
        }

        private void ChkRename_Checked(object sender, RoutedEventArgs e)
        {
            TripRef.Reorganize = (Boolean)ChkRename.IsChecked;
            NotifyChange();
        }

        private void ComboDropbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TripSummaryRef.SyncDropbox.Compression = SynchroManager.IntToCompr((sender as ComboBox).SelectedIndex);
            NotifyChange();
        }

        private void ComboUsb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TripSummaryRef.SyncUsb.Compression = SynchroManager.IntToCompr((sender as ComboBox).SelectedIndex);
            NotifyChange();
        }

        private Boolean NotifyChange()
        {
            if (_initFinished && _listLoaded)
                 _changeInProgress = true;

            return _changeInProgress;
        }

        internal void ProgressUpdate(string _text, int _percent)
        {
            ringImport.IsActive = true;
            progressBar.Visibility = Visibility.Visible;
            txtImport.Visibility = Visibility.Visible;
            progressBar.Value = _percent;
            txtImport.Text = _text;
            UpdateLayout();
        }

        internal void ProgressFinished(string _text)
        {
            ringImport.IsActive = false;
            progressBar.Visibility = Visibility.Collapsed;
            if ((_text != null) && (_text != ""))
                txtImport.Text = _text;
            else
                txtImport.Visibility = Visibility.Collapsed;
        }

        internal void StartImport()
        {
            _importInProgress = true;

            ringImport.IsActive = true;
            TextBoxReadOnly(TripName, true);
            TextBoxReadOnly(TripLocation, true);
            TripName.IsEnabled = false;
            TripLocation.IsEnabled = false;

            progressBar.Visibility = Visibility.Visible;
            txtImport.Visibility = Visibility.Visible;

            ChkDropbox.IsEnabled = false;
            ChkUsb.IsEnabled = false;
            ComboDropbox.IsEnabled = false;
            ComboUsb.IsEnabled = false;

            TripPath.IsEnabled = false;
            PicNum.IsEnabled = false;
            AlbumNum.IsEnabled = false;

            btnUpdate.IsEnabled = false;
            ChkRename.IsEnabled = false;
            foreach (AlbumConfUserControl _albumConf in listAlbums)
                _albumConf.Lock();
        }

        internal void StopImport()
        {
            TextBoxReadOnly(TripName, false);
            TextBoxReadOnly(TripLocation, false);
            TripName.IsEnabled = true;
            TripLocation.IsEnabled = true;

            if ((TripRef.Sample)  || !(PageRef is ViewHome))
            {
                TextBoxReadOnly(TripName, true);
                TextBoxReadOnly(TripLocation, true);
            }

            btnUpdate.IsEnabled = true;
            ChkRename.IsEnabled = true;

            TripPath.IsEnabled = true;
            PicNum.IsEnabled = true;
            AlbumNum.IsEnabled = true;

            foreach (AlbumConfUserControl _albumConf in listAlbums)
                _albumConf.Unlock();

            if ((!TripRef.Sample) && (PageRef is ViewHome))
            {
                ChkDropbox.IsEnabled = true;
                ChkUsb.IsEnabled = true;
                ComboDropbox.IsEnabled = true;
                ComboUsb.IsEnabled = true;
            }

            ringImport.IsActive = false;

            _importInProgress = false;
        }

        private void TripName_TextChanged(object sender, TextChangedEventArgs e)
        {
            NotifyChange();
        }

        private void tableAlbums_Loaded(object sender, RoutedEventArgs e)
        {
            _listLoaded = true;
        }
    }

    public class ConfObj
    { 
        public TripSummary TripSummary;
        public AlbumSummary AlbumSummary;
        public Page Page;

        public ConfObj(Page _pageRef, TripSummary _tripSummary, AlbumSummary _albumSummary)
        {
            TripSummary = _tripSummary;
            AlbumSummary = _albumSummary;
            Page = _pageRef;
        }
    }
}
