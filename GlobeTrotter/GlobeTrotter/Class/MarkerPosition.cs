using System;
using GlobeTrotter;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Bing.Maps;

namespace GlobeTrotter
{
    public enum EIcon
    {
        IconUndefined,
        IconBus,
        IconCity,
        IconFlag,
        IconTrain,
        IconVan
    }

    public class MarkerPosition
    {
        private GpsLocation _location;
        private FrameworkElement _viewParent;
        private EIcon _albumIcon;
        private Image _image;
        private MapUIElementCollection _elementUI;
        private TextBlock _legend;
        object _icon;

        public MarkerPosition()
        {
        }

        public MarkerPosition(FrameworkElement view, MapUIElementCollection _element, object _tag, object Icon, GpsLocation pos, Boolean _visible, int _size)
        {
            _location = pos;
            _viewParent = view;

            _image = new Image();
            _icon = Icon;

            if (Icon is EIcon)
            {
                _albumIcon = (EIcon)Icon;
                _image.Source = ImageFromRelativePath(view, GetIconInactive(_albumIcon));
            }
            else if (Icon is BitmapImage)
                _image.Source = Icon as BitmapImage;
            else if (Icon is String)
                _image.Source = ImageFromRelativePath(view, Icon as String);

            _elementUI = _element;

            try
            {
                _image.Width = _size;
                _image.Height = _size;
                _image.Tag = _tag;
                _image.Tapped += image_Tapped;

                MapLayer.SetPosition(_image, Gps.ConvertGpsToLoc(_location));

                if (Icon is BitmapImage)
                    MapLayer.SetPositionAnchor(_image, GetOffsetImage(2));
                else
                    MapLayer.SetPositionAnchor(_image, GetOffsetImage(1));

                _elementUI.Add(_image);

#if DEBUG
                if (_tag != null)
                {
                    _legend = new TextBlock(); 
                    
                    if (_tag is Country)
                        _legend.Text = (_tag as Country).Name.ToString();
                    else
                        _legend.Text = _tag.ToString();

                    _legend.Foreground = new SolidColorBrush(Windows.UI.Colors.White);

                    MapLayer.SetPosition(_legend, Gps.ConvertGpsToLoc(_location));
                    MapLayer.SetPositionAnchor(_legend, GetOffsetLegend());
                    _elementUI.Add(_legend);
                }
#else
                _legend = null;
#endif

                if (!_visible)
                    Hide();
                else
                    Show();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private void _image_DragStarted(object sender, DragStartedEventArgs e)
        {
        }

        public Country Country()
        {
            if (_image.Tag is Country)
                return _image.Tag as Country;
            else
                return null;
        }

        private void _image_DragCompleted(object sender, DragCompletedEventArgs e)
        {
        }

        public void Activate()
        {
            if ((_icon != null) && (!(_icon is BitmapImage)))
                _image.Source = ImageFromRelativePath(_viewParent, GetIconActive(_albumIcon));
        }

        public void Hide()
        {
            _image.Visibility = Visibility.Collapsed;

            if (_legend != null)
                _legend.Visibility = Visibility.Collapsed;
        }

        public void Show()
        {
            _image.Visibility = Visibility.Visible;

            if (_legend != null) 
                _legend.Visibility = Visibility.Visible;
        }

        public void Delete()
        {
            if (_elementUI != null)
            {
                _elementUI.Remove(_image);

                if (_legend != null)
                    _elementUI.Remove(_legend);
            }
        }

        public void DeActivate()
        {
            if ((_icon != null) && (!(_icon is BitmapImage)))
                _image.Source = ImageFromRelativePath(_viewParent, GetIconInactive(_albumIcon));
        }

        internal Windows.Foundation.Point GetOffsetImage(int _factor)
        {
            return new Windows.Foundation.Point(_image.Width / 2, _image.Height / _factor);
        }

        internal Windows.Foundation.Point GetOffsetLegend()
        {
            return new Windows.Foundation.Point(5 * _legend.Text.Length / 2, -_image.Height/3);
        }

        private static BitmapImage ImageFromRelativePath(FrameworkElement _parent, string _path)
        {
            var uri = new Uri(_parent.BaseUri, _path);
            BitmapImage result = new BitmapImage();
            result.UriSource = uri;
            return result;
        }

        private async void image_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Image im = sender as Image;
            Type _type = im.Tag.GetType();
            if (_type.Name.Equals("String"))
                await (_viewParent as ViewMapTrip).DisplayPicturesList(im.Tag.ToString(), 0);
            else if (_type.Name.Equals("Int32"))
                (_viewParent as ViewMapTrip).FlagSelected((Int32)im.Tag, this);
            else if (_type.Name.Equals("Country"))
                (_viewParent as ViewMapTrip).CountrySelected((Country)im.Tag, this);        
        }

        public void SetLegendColor(Boolean _value)
        {
            if (_legend != null)
            {
                if (_value)
                    _legend.Foreground = new SolidColorBrush(Colors.Red);
                else
                    _legend.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private String GetIconActive(EIcon icon)
        {
            switch (icon)
            {
                case EIcon.IconBus: return "Icons/bus.png";
                case EIcon.IconCity: return "Icons/bigcity_red.png";
                case EIcon.IconFlag: return "Icons/flag_red.png";
                case EIcon.IconTrain: return "Icons/train.png";
                case EIcon.IconVan: return "Icons/van.png";
                default: return null;
            }
        }

        private String GetIconInactive(EIcon icon)
        {
            switch (icon)
            {
                case EIcon.IconBus: return "Icons/bus_white.png";
                case EIcon.IconCity: return "Icons/bigcity_green.png";
                case EIcon.IconFlag: return "Icons/flag_blue.png";
                case EIcon.IconTrain: return "Icons/train_white.png";
                case EIcon.IconVan: return "Icons/van_white.png";
                default: return null;
            }
        }
    }
}

