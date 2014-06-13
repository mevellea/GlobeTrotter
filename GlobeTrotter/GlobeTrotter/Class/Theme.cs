using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace GlobeTrotter
{
    public class Theme
    {
        // colors default
        public static byte[] Yellow = { 255, 205, 197, 44 };
        public static byte[] YellowPale = { 255, 226, 210, 103 };
        public static byte[] BlueNight = { 255, 46, 33, 96 };
        public static byte[] Orange = { 255, 233, 179, 69 };
        public static byte[] Orange1 = { 255, 205, 108, 44 };
        public static byte[] Orange2 = { 255, 205, 149, 44 };
        public static byte[] Red = { 255, 205, 44, 44 };
        public static byte[] White = { 255, 255, 255, 255 };

        // colors desert
        public static byte[] Black = { 255, 0, 0, 0 };
        public static byte[] GreyDark = { 255, 51, 51, 51 };
        public static byte[] Maroon = { 255, 73, 37, 16 };
        public static byte[] Grey = { 255, 119, 112, 97 };

        // colors rain forrest
        public static byte[] GreenDark = { 255, 49, 62, 47 };
        public static byte[] Green = { 255, 35, 142, 13 };
        public static byte[] GreenPale = { 255, 196, 202, 183 };
        public static byte[] GreenPale2 = { 255, 173, 187, 139 };
        public static byte[] Maroon2 = { 255, 100, 75, 29 };
        public static byte[] Maroon3 = { 255, 145, 106, 51 };


        public static byte[] OrangeWasp = { 255, 255, 127, 39 };
        public static byte[] YellowWasp = { 255, 255, 201, 14 };

        public static byte[] GreenSpring = { 255, 200, 251, 104 };
        public static byte[] BlueSpring = { 255, 0, 162, 232 };
        public static byte[] YellowSpring = { 255, 255, 242, 0 };

        public static byte[][] ThemeDefault = { Yellow, YellowPale, BlueNight, Red, Orange1, Orange2, White, Orange1 };
        public static byte[][] ThemeBlack = { Black, GreyDark, BlueNight, Red, Orange1, Orange2, White, Orange1 };
        public static byte[][] ThemeDesert = { YellowPale, Black, Maroon, Orange, Yellow, YellowPale, White, Yellow };
        public static byte[][] ThemeSquares = { GreenDark, White, Green, Maroon, Maroon2, Maroon3, White, Yellow };
        public static byte[][] ThemeWasp = { OrangeWasp, YellowWasp, Black, Red, Maroon2, Maroon3, White, Yellow };
        public static byte[][] ThemeSpring = { GreenSpring, YellowSpring, BlueSpring, Maroon, Maroon2, Maroon3, Red, Maroon };

        static byte[][][] table = 
        { 
            ThemeDefault, 
            ThemeBlack,
            ThemeDesert,
            ThemeSquares,
            ThemeWasp,
            ThemeSpring
        };

        public enum EColorPalet
        {
            MainBg = 0,
            MainBgDegr,
            MainFront,
            Text1,
            Text2,
            Text3,
            Text4,
            Text5
        }

        public enum EName
        {
            EThemeDefault = 0,
            EThemeBlack,
            EThemeDesert,
            EThemeSquares,
            EThemeWasp,
            EThemeSpring
        };

        public static Color GetColorFromTheme(EName _theme, EColorPalet _colorName)
        {
            Color _color = new Color();
            _color.A = table[(int)_theme][(int)_colorName][0];
            _color.R = table[(int)_theme][(int)_colorName][1];
            _color.G = table[(int)_theme][(int)_colorName][2];
            _color.B = table[(int)_theme][(int)_colorName][3];
            return _color;
        }

        public static ImageBrush GetPictureFromTheme(EName _theme)
        {
            ImageBrush _imageBrush = new ImageBrush();
            String _path = "ms-appx:///Assets/";
            switch (_theme)
            {
                default:
                case EName.EThemeWasp:
                case EName.EThemeDefault:
                    _path += "Wallpaper_yellow.png";
                    break;
                case EName.EThemeSquares:
                    _path += "Wallpaper_bluesquares.png";
                    break;
                case EName.EThemeDesert:
                    _path += "Wallpaper_sand.png";
                    break;
                case EName.EThemeSpring:
                    _path += "Wallpaper_green.png";
                    break;
                case EName.EThemeBlack:
                    _path += "Wallpaper_black.png";
                    break;
            }
            _imageBrush.ImageSource = new BitmapImage() { UriSource = new Uri(_path)};
            _imageBrush.Stretch = Stretch.UniformToFill;
            return _imageBrush;
        }
    }
}
