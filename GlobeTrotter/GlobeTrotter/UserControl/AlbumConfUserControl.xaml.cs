using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.FileProperties;
// Pour en savoir plus sur le modèle d'élément Contrôle utilisateur, consultez la page http://go.microsoft.com/fwlink/?LinkId=234236

namespace GlobeTrotter
{
    public sealed partial class AlbumConfUserControl : UserControl
    {
        public Album AlbumRef;
        public TextBox NameTopRef;
        public DatePicker DateTopRef;
        public CheckBox CheckBoxRef;

        Func<Boolean> _handler;

        public AlbumConfUserControl(Album _album, Func<Boolean> _func)
        {
            this.InitializeComponent();
            AlbumRef = _album;
            NameTopRef = NameTop;
            DateTopRef = DateTop;
            CheckBoxRef = CkhLocation;

            Display(AlbumRef.Summary.PathThumb, AlbumRef.Summary.PicturesThumb);

            NameTop.Text = AlbumRef.DisplayName;
            DateTop.Date = AlbumRef.DateArrival;
            CkhLocation.IsChecked = AlbumRef.PositionPresent;
            _handler = _func;

            ResourceLoader _res = ResourceLoader.GetForCurrentView();
            CkhLocation.Content = _res.GetString("LocationDefined");
        }

        private async void Display(string _path, List<String> p)
        {
            List<ImageSource> pic = new List<ImageSource>();
            Boolean _defaultPictures = false;
            StorageItemThumbnail fileThumbnail;
            StorageFile _file;
            IRandomAccessStream _stream;
            BitmapImage bm;
            try
            {
                if (p.Count() < 4)
                {
                    if (AlbumRef.Summary.Sample)
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
                        if (AlbumRef.Summary.Sample)
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
            catch
            {
                _defaultPictures = true;
            }

            if (_defaultPictures)
            {
                pic.Clear();
                for (int idx = 0; idx < 4; idx++)
                {
                    _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/reward_" + idx.ToString() + ".png"));
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
        }

        private void NameTop_TextChanged(object sender, TextChangedEventArgs e)
        {
            _handler();
        }

        private void CkhLocation_Checked(object sender, RoutedEventArgs e)
        {
            if (_handler != null)
            _handler();
        }

        private void DateTop_DateChanged(object sender, DatePickerValueChangedEventArgs e)
        {
            _handler();
        }

        public void Lock()
        {
            CkhLocation.IsEnabled = false;
            NameTop.BorderThickness = new Thickness(0);
            NameTop.IsEnabled = false;
            DateTop.IsEnabled = false;
        }

        public void Unlock()
        {
            CkhLocation.IsEnabled = true;
            NameTop.BorderThickness = new Thickness(2);
            NameTop.IsEnabled = true;
            DateTop.IsEnabled = true;
        }
    }
}
