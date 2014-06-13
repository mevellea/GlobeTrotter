using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Foundation;
using NotificationsExtensions.ToastContent;

namespace GlobeTrotter
{
    class Toast
    {
        public static void DisplaySingleLine(String _text)
        {
            IToastImageAndText01 templateContent = ToastContentFactory.CreateToastImageAndText01();
            templateContent.TextBodyWrap.Text = _text;
            templateContent.Image.Src = "Icons/toastImageAndText.png";
            IToastNotificationContent toastContent = templateContent;

            ToastNotification toast = toastContent.CreateNotification();
            ToastNotificationManager.CreateToastNotifier().Show(toast);      
        }

        public static void DisplayTwoLines(String _text1, String _text2, String _picture)
        {
            IToastImageAndText02 templateContent = ToastContentFactory.CreateToastImageAndText02();
            templateContent.TextHeading.Text = _text1;
            templateContent.TextBodyWrap.Text = _text2;
            templateContent.Image.Src = _picture;

            IToastNotificationContent toastContent = templateContent;

            ToastNotification toast = toastContent.CreateNotification();
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
    }
}
