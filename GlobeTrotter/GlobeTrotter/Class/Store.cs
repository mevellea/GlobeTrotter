using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.UI.Popups;
using Windows.ApplicationModel.Store;

namespace GlobeTrotter
{
    class Store
    {
        public static async Task<Boolean> Purchase()
        {
            try
            {
#if DEBUG
                await CurrentAppSimulator.RequestAppPurchaseAsync(false);
#else
                await CurrentApp.RequestAppPurchaseAsync(false);
#endif
                if (!IsTrial())
                {
                    ResourceLoader _res = ResourceLoader.GetForCurrentView();
                    MessageDialog messageDialog = new MessageDialog(_res.GetString("VersionFull"), _res.GetString("ThanksPurchase"));
                    messageDialog.Commands.Add(new UICommand("Ok", (command) => { }));
                    messageDialog.CancelCommandIndex = 0;
                    messageDialog.DefaultCommandIndex = 0;
                    await messageDialog.ShowAsync();
                    return true;
                }
                else
                    return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool IsTrial()
        {
#if DEBUG
            LicenseInformation licenseInformation = CurrentAppSimulator.LicenseInformation;
#else
            LicenseInformation licenseInformation = CurrentApp.LicenseInformation;
#endif
            //return (licenseInformation.IsActive && licenseInformation.IsTrial);
            return false;
        }

        public static String AppUri()
        {
#if DEBUG
            return CurrentAppSimulator.LinkUri.AbsoluteUri;
#else
            return CurrentApp.LinkUri.AbsoluteUri;
#endif
        }
    }
}
