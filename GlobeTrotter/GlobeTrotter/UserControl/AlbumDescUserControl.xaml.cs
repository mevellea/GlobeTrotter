using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
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


// Pour en savoir plus sur le modèle d'élément Contrôle utilisateur, consultez la page http://go.microsoft.com/fwlink/?LinkId=234236

namespace GlobeTrotter
{
    public sealed partial class AlbumDescUserControl : UserControl
    {
        public AlbumDescUserControl(Theme.EName _theme, ViewMapTrip _p)
        {
            this.InitializeComponent();
            UpdateTheme(_theme);
            _parent = _p;
        }

        public AlbumSummary Summary;
        
        Brush _brushRef;
        ViewMapTrip _parent;

        public void UpdateTheme(Theme.EName _name)
        {
            StackPanelMain.Background = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.MainFront));
            NameDisp.Foreground = new SolidColorBrush(Theme.GetColorFromTheme(_name, Theme.EColorPalet.Text5));
        }

        // dependency property
        public static DependencyProperty DefBlockTripProperty = DependencyProperty.Register(
            "DefBlockTrip",
            typeof(AlbumSummary),
            typeof(AlbumDescUserControl),
            new PropertyMetadata(0));

        public AlbumSummary DefBlockTrip
        {
            get
            {
                return (AlbumSummary)GetValue(DefBlockTripProperty);
            }
            set
            {
                this.Visibility = Visibility.Collapsed;
                loadRing.IsActive = true;

                SetValue(DefBlockTripProperty, value);
                Summary = value;

                UpdateName(value.StrLocation);
                UpdatePicturesDisplay(value.PathThumb, value.PicturesThumb);

                loadRing.IsActive = false;
                this.Visibility = Visibility.Visible;

                String _index = value.Id;
                StackPanelMain.Tag = _index;
                pic0.Tag = _index;
                pic1.Tag = _index;
                pic2.Tag = _index;
                pic3.Tag = _index;
                pic_single.Tag = _index;
                loadRing.Tag = _index;
                NameDisp.Tag = _index;
            }
        }

        public void UpdateName(String _name)
        {
            NameDisp.Text = _name;
        }

        private async void UpdatePicturesDisplay(string _path, List<String> p)
        {
            await Display(_path, p);
        }

        private async Task Display(string _path, List<String> p)
        {        
            List<ImageSource> pic = new List<ImageSource>();

            Boolean _defaultPictures = false;
            StorageItemThumbnail fileThumbnail;
            StorageFile _file;
            IRandomAccessStream _stream;
            BitmapImage bm;

            try
            {
                loadRing.IsActive = true;

                if (p.Count() < 4)
                {
                    if (Summary.Sample)
                        _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/" + _path + "/" + p[0]));
                    else
                    {
                        StorageFolder folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(_path);
                        _file = await folder.GetFileAsync(p[0]);
                    }
                    _stream = await _file.OpenAsync(FileAccessMode.Read);
                    bm = new BitmapImage();
                    await bm.SetSourceAsync(_stream);
                    pic.Add(bm);
                }
                else
                {
                    for (int idx = 0; idx < 4; idx++)
                    {
                        if (Summary.Sample)
                            _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/" + _path + "/" + p[idx]));
                        else
                        {
                            StorageFolder folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(_path);
                            _file = await folder.GetFileAsync(p[idx]);
                        }
                        _stream = await _file.OpenReadAsync();
                        bm = new BitmapImage();
                        await bm.SetSourceAsync(_stream);
                        pic.Add(bm);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                _defaultPictures = true;
                loadRing.IsActive = false;
            }

            if (_defaultPictures)
            {
                pic.Clear();
                for (int idx = 0; idx < 4; idx++)
                {
                    _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/reward_"+idx.ToString()+".png"));
                    fileThumbnail = await _file.GetThumbnailAsync(ThumbnailMode.SingleItem, 120, ThumbnailOptions.ResizeThumbnail);
                    bm = new BitmapImage();
                    await bm.SetSourceAsync(fileThumbnail);
                    pic.Add(bm);
                }
            }
            if (p.Count<string>() < 4)
            {
                pic_single.Source = pic[0];
                pic_single.Visibility = Visibility.Visible;
                pic0.Visibility = Visibility.Collapsed;
                pic1.Visibility = Visibility.Collapsed;
                pic2.Visibility = Visibility.Collapsed;
                pic3.Visibility = Visibility.Collapsed;
            }
            else
            {
                pic0.Source = pic[0];
                pic1.Source = pic[1];
                pic2.Source = pic[2];
                pic3.Source = pic[3];
                pic_single.Visibility = Visibility.Collapsed;
                pic0.Visibility = Visibility.Visible;
                pic1.Visibility = Visibility.Visible;
                pic2.Visibility = Visibility.Visible;
                pic3.Visibility = Visibility.Visible;
            }
            loadRing.IsActive = false;
        }

        internal void PositionWarning()
        {
            //StorageFile _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/position_warning.png"));
            //IRandomAccessStream _stream = await _file.OpenAsync(FileAccessMode.Read);
            //BitmapImage bm = new BitmapImage();
            //await bm.SetSourceAsync(_stream);
            //pic_loc.Source = bm;
            //loadRing.IsActive = false;

            pic_loc.Visibility = Visibility.Collapsed;

            _brushRef = StackPanelMain.Background;
            StackPanelMain.Background = new SolidColorBrush(Colors.White);
        }

        internal async void PositionOk()
        {
            StorageFile _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/position_ok.png"));
            IRandomAccessStream _stream = await _file.OpenAsync(FileAccessMode.Read);
            BitmapImage bm = new BitmapImage();
            await bm.SetSourceAsync(_stream);
            pic_loc.Source = bm;
            loadRing.IsActive = false;

            if (_brushRef != null)
                StackPanelMain.Background = _brushRef;
        }

        public void SetActive()
        {
            pic_loc.Visibility = Visibility.Collapsed;
            loadRing.IsActive = true;
        }

        public void SetInactive()
        {
            loadRing.IsActive = false;
            pic_loc.Visibility = Visibility.Visible;
        }

        private void confTapped(object sender, TappedRoutedEventArgs e)
        {
            _parent.NavigateToAlbumConf(Summary);            
        }
    }
}
