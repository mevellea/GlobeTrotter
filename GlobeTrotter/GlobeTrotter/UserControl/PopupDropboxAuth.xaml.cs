using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.ApplicationModel.Resources;
using System.Threading.Tasks;
using Windows.System;
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
    public sealed partial class PopupDropboxAuth : UserControl
    {
        public delegate Task CodeSelectorHandler(String _id);

        CodeSelectorHandler _handler;
        Uri _uri;
        ResourceLoader _res;

        public PopupDropboxAuth(Uri _u, CodeSelectorHandler _h, ResourceLoader _r)
        {
            this.InitializeComponent();
            _handler = _h;
            _uri = _u;
            _res = _r;
            Width = 440;
            Height = 240;

            CodeAuthField.Text = _res.GetString("CodeAuthField");
            DescField1.Text = _res.GetString("DescField1");
            DescField2.Text = _res.GetString("DescField2");
            RequestText.Content = _res.GetString("RequestText");
            Cancel.Content = _res.GetString("Cancel");
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Validate();
        }

        void Validate()
        {
            _handler(CodeAuthField.Text);

            Popup p = this.Parent as Popup;
            p.IsOpen = false;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Popup p = this.Parent as Popup;
            p.IsOpen = false;
        }

        private async void RequestCode_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(_uri);
        }

        private void CodeAuthField_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (CodeAuthField.Text == _res.GetString("CodeAuthField"))
                CodeAuthField.Text = "";
        }
    }
}
