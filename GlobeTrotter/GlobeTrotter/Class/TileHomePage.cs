using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;
using NotificationsExtensions.TileContent;
using Windows.ApplicationModel.Resources;

namespace GlobeTrotter
{
    public class TileHomePage
    {
        private static String _logoSecondaryTileId = "SecondaryTileTripReview.Logo";

        public static void UpdateDisplay(String _appName, String _reward, Boolean _sample, String _picPath, List<String> _picAlbum, List<String> _countries)
        {
            ResourceLoader _res = ResourceLoader.GetForCurrentView();
            String _textDesc = _res.GetString("ReviewDefinition");
            String _uridemo = "ms-appx:///Assets/";
            String _uriStart = _sample ? "ms-appx:///appdata/" : "ms-appdata:///local/";
            String _src1, _src2, _src3, _src4;

            if (_picPath != null)
            {
                _src1 = _uriStart + _picPath + "/" + _picAlbum[0];

                if (_picAlbum.Count < 2)
                    _src2 = _uridemo + "reward_0_blueViolet.png";
                else
                    _src2 = _uriStart + _picPath + "/" + _picAlbum[1];

                if (_picAlbum.Count < 3)
                    _src3 = _uridemo + "reward_1_blueViolet.png";
                else
                    _src3 = _uriStart + _picPath + "/" + _picAlbum[2];

                if (_picAlbum.Count < 4)
                    _src4 = _uridemo + "reward_2_blueViolet.png";
                else
                    _src4 = _uriStart + _picPath + "/" + _picAlbum[3];

                if (_countries.Count > 0)
                {
                    _textDesc += " to ";
                    foreach (String _country in _countries)
                        _textDesc += _country + ", ";
                    _textDesc += "...";
                }
            }
            else
            {
                _src1 = _uridemo + "reward_0.png";
                _src2 = _uridemo + "reward_1.png";
                _src3 = _uridemo + "reward_2.png";
                _src4 = _uridemo + "reward_3.png";
                _reward = _uridemo + "reward_cup_1.png";
            }

            _src1 = "ms-appx:///appdata/1221335346/83915936.jpg";
            _src2 = "ms-appx:///appdata/1221335346/2112996589.jpg";
            _src3 = "ms-appx:///appdata/1221335346/1915394806.jpg";
            _src4 = "ms-appx:///appdata/1221335346/684173310.jpg";

            // Create a notification for the Square310x310 tile using one of the available templates for the size.
            ITileSquare310x310ImageCollectionAndText01 tileContent = TileContentFactory.CreateTileSquare310x310ImageCollectionAndText01();
            tileContent.TextCaptionWrap.Text = _textDesc;
            tileContent.ImageMain.Src = _reward;
            tileContent.ImageSmall1.Src = _src1;
            tileContent.ImageSmall2.Src = _src2;
            tileContent.ImageSmall3.Src = _src3;
            tileContent.ImageSmall4.Src = _src4;

            // Create a notification for the Wide310x150 tile using one of the available templates for the size.
            ITileWide310x150PeekImageCollection05 wide310x150Content = TileContentFactory.CreateTileWide310x150PeekImageCollection05();
            wide310x150Content.TextBodyWrap.Text = _textDesc;
            wide310x150Content.TextHeading.Text = _appName;
            wide310x150Content.ImageSmallColumn1Row1.Src = _src1;
            wide310x150Content.ImageSmallColumn2Row1.Src = _src2;
            wide310x150Content.ImageSmallColumn1Row2.Src = _src3;
            wide310x150Content.ImageSmallColumn2Row2.Src = _src4;
            wide310x150Content.ImageMain.Src = _reward;
            wide310x150Content.ImageSecondary.Src = _reward;

            // Create a notification for the Square150x150 tile using one of the available templates for the size.
            ITileSquare150x150PeekImageAndText04 square150x150Content = TileContentFactory.CreateTileSquare150x150PeekImageAndText04();
            square150x150Content.TextBodyWrap.Text = _textDesc;
            square150x150Content.Image.Src = _reward;

            // Attach the Square150x150 template to the Wide310x150 template.
            wide310x150Content.Square150x150Content = square150x150Content;

            // Attach the Wide310x150 template to the Square310x310 template.
            tileContent.Wide310x150Content = wide310x150Content;

            // Send the notification to the application’s tile.
            TileUpdateManager.CreateTileUpdaterForApplication().Update(tileContent.CreateNotification());
        }

        internal static Boolean SecondaryTileExist()
        {
            return (SecondaryTile.Exists(_logoSecondaryTileId));
        }
        
        internal static async Task<Boolean> UnpinSecondaryTile(FrameworkElement _element)
        {
            // First prepare the tile to be unpinned
            SecondaryTile secondaryTile = new SecondaryTile(_logoSecondaryTileId);

            GeneralTransform buttonTransform = _element.TransformToVisual(null);
            Point point = buttonTransform.TransformPoint(new Point());
            Rect rect = new Rect(point, new Size(_element.ActualWidth, _element.ActualHeight));
            return (await secondaryTile.RequestDeleteForSelectionAsync(rect, Placement.Above));
        }

        internal static async Task<Boolean> PinSecondaryTile(FrameworkElement _element)
        {
            string tileActivationArguments = _logoSecondaryTileId;

            Uri square150x150Logo = new Uri("ms-appx:///Assets/Square150x150Logo.scale-100.png");
            Uri wide310x150Logo = new Uri("ms-appx:///Assets/Wide310x150Logo.scale-100.png");
            Uri square310x310Logo = new Uri("ms-appx:///Assets/Square310x310Logo.scale-100.png");

            ResourceLoader _res = ResourceLoader.GetForCurrentView();

            SecondaryTile secondaryTile = 
                new SecondaryTile(_logoSecondaryTileId,
                    "GlobeTrotter",
                    tileActivationArguments,
                    square150x150Logo,
                    TileSize.Square150x150);

            secondaryTile.VisualElements.Wide310x150Logo = wide310x150Logo;
            secondaryTile.VisualElements.Square310x310Logo = square310x310Logo;

            GeneralTransform buttonTransform = _element.TransformToVisual(null);
            Point point = buttonTransform.TransformPoint(new Point());
            Rect rect = new Rect(point, new Size(_element.ActualWidth, _element.ActualHeight));
            return (await secondaryTile.RequestCreateForSelectionAsync(rect, Placement.Above));
        }
    }
}
