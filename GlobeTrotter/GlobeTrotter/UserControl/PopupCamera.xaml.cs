using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Pour en savoir plus sur le modèle d'élément Contrôle utilisateur, consultez la page http://go.microsoft.com/fwlink/?LinkId=234236

namespace GlobeTrotter
{
    public sealed partial class PopupDevice : UserControl
    {
        public PopupDevice(List<String> _display, RoutedEventHandler _callback)
        {
            this.InitializeComponent();
            int _max = 0;

            sp.Children.Clear();
            sp.Background = new SolidColorBrush(Color.FromArgb(100, 46, 33, 96));

            foreach (String _str in _display)
            {
                _max = Math.Max(_max, _str.Length);
                sp.Children.Add(CreateButton(_str, _callback));
            }

            this.Width = _max * 15 + 40;
            this.Height = (55 + 10) * _display.Count() + 10;
        }

        // Handles the Click event of the 'Save' button simulating a save and close
        private void btnClicked(object sender, RoutedEventArgs e)
        {
            // in this example we assume the parent of the UserControl is a Popup
            Popup p = this.Parent as Popup;
            p.IsOpen = false; // close the Popup
        }

        private Button CreateButton(String _text, RoutedEventHandler _callback)
        {   
            Button _button = new Button();
            _button.Content = _text;
            _button.Click += btnClicked;
            _button.Click += _callback;
            _button.Width= 280;
            _button.Height= 55;
            _button.Background = new SolidColorBrush(Colors.Black);
            _button.HorizontalAlignment = HorizontalAlignment.Center;
            _button.VerticalAlignment = VerticalAlignment.Top;
            _button.Margin = new Thickness(10,10,10,0);
            return _button;
        }
    }
}
