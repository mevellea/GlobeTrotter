using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Security.Authentication.Web;
using Windows.Web.Http;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace GlobeTrotter
{
    public class FacebookServer : StorageServer
    {
        public FacebookServer(Settings _settings)
        {
            LocalSettings = _settings;

            Id = "Facebook.com";
            DisplayName = "Facebook";
            Name = GlobeTrotter.SynchroManager.ServerName.Facebook;

            ClientId = "599268110151945";
            Secret = "7c0dd186f8a326a5b025f53093a71117";
            CallbackUrl = "http://localhost";

            HasSecret = true;
            HasExpiryDate = false;

            LOCAL_OAUTH_TOKEN = "FACEBOOK_OAUTH_TOKEN";
            LOCAL_USER_NAME = "FACEBOOK_USER_NAME";
            Provider = new WebAccountProvider(Id, DisplayName, new Uri("ms-appx:///icons/Facebook.png"));

            InitializeServerAccount();
        }

        public async override Task Login()
        {
            try
            {
                String FacebookURL = "https://www.facebook.com/dialog/oauth?client_id=" + Uri.EscapeDataString(ClientId) +
                    "&redirect_uri=" + Uri.EscapeDataString(CallbackUrl) + "&scope=read_stream&display=popup&response_type=token";

                Uri StartUri = new Uri(FacebookURL);
                Uri EndUri = new Uri(CallbackUrl);

                WebAuthenticationResult WebAuthenticationResult = await WebAuthenticationBroker.AuthenticateAsync(
                                                        WebAuthenticationOptions.None,
                                                        StartUri,
                                                        EndUri);
                if (WebAuthenticationResult.ResponseStatus == WebAuthenticationStatus.Success)
                {
                    Debug.WriteLine(WebAuthenticationResult.ResponseData.ToString());
                    await GetFacebookUserNameAsync(WebAuthenticationResult.ResponseData.ToString());
                    LoggedIn = WebAccountState.Connected;
                }
            }
            catch (Exception Error)
            {
                Debug.WriteLine(Error.ToString());
            }
            return;
        }

        private async Task GetFacebookUserNameAsync(string webAuthResultResponseData)
        {
            //Get Access Token first
            string responseData = webAuthResultResponseData.Substring(webAuthResultResponseData.IndexOf("access_token"));
            String[] keyValPairs = responseData.Split('&');
            string access_token = null;
            string expires_in = null;
            for (int i = 0; i < keyValPairs.Length; i++)
            {
                String[] splits = keyValPairs[i].Split('=');
                switch (splits[0])
                {
                    case "access_token":
                        access_token = splits[1];
                        break;
                    case "expires_in":
                        expires_in = splits[1];
                        break;
                }
            }

            LocalSettings.SaveStorageValue<String>(LOCAL_OAUTH_TOKEN, access_token); //store access token locally for further use.
            Debug.WriteLine("access_token = " + access_token);

            //Request User info.
            HttpClient httpClient = new HttpClient();
            string response = await httpClient.GetStringAsync(new Uri("https://graph.facebook.com/me?access_token=" + access_token));
            JsonObject value = JsonValue.Parse(response).GetObject();

            UserName = value.GetNamedString("name");
            LocalSettings.SaveStorageValue<String>(LOCAL_USER_NAME, UserName); //store user name locally for further use.

        }

        public override Task<Boolean> DeleteContent(TripSummary _summary)
        {
            return null;
        }

        public override Task Disconnect()
        {
            return null;
        }

        public override Task GoWebsite()
        {
            return null;
        }

        public async override Task<SynchroManager.Status> Synchronize(SynchroHandle _syncHandle)
        {
            SynchroManager.Status _syncStatus = SynchroManager.Status.ErrorOrNotConnected;

            if (!Connection.InternetAccess())
                return _syncStatus;

            if (LoggedIn != WebAccountState.Connected)
                await SyncMngr.Login(SynchroManager.ServerName.Facebook);

            ProgressUpdate(Res.GetString("SynchroStart"), 0);

            Trip _trip = await Trip.LoadFromSummary(_syncHandle.TripSummary);

            return SynchroManager.Status.Synchronized;
        }
    }
}
