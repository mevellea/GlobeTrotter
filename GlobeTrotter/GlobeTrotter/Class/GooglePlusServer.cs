using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Security.Authentication.Web;
using System.Diagnostics;
using Windows.Data.Json;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using GoogleService.Common.JSON;
using System.Runtime.Serialization;

namespace GlobeTrotter
{
    public class GooglePlusServer : StorageServer
    {
        public static String Key = "AIzaSyAb5ZXOr_ZJvpqNl61CrxVfcQxQ_8eEEYo";

        public static String MAIN = "https://picasaweb.google.com/data/feed/api/user/default";

        public GooglePlusServer(Settings _settings)
        {
            LocalSettings = _settings;

            Id = "google.com";
            DisplayName = "Google+";
            Name = GlobeTrotter.SynchroManager.ServerName.GooglePlus;

            ClientId = "206947143106-bv9gp9dobditnb0kc4ll8ns3tdat2tc3.apps.googleusercontent.com";
            Secret = "edWCwEa_fvPv3buF6xHe0eCY";
            CallbackUrl = "urn:ietf:wg:oauth:2.0:oob";
            State = "dkjnsdckjhbsfejhbsf";

            HasSecret = true;
            HasExpiryDate = true;
            HasTokenRefresh = true;

            LOCAL_OAUTH_TOKEN = "GOOGLE_PLUS_OAUTH_TOKEN";
            LOCAL_OAUTH_TOKEN_SECRET = "GOOGLE_PLUS_OAUTH_TOKEN_SECRET";
            LOCAL_OAUTH_TOKEN_REFRESH = "GOOGLE_PLUS_OAUTH_TOKEN_REFRESH";
            LOCAL_USER_NAME = "GOOGLE_PLUS_USER_NAME";
            LOCAL_OAUTH_EXPIRY_DATE = "GOOGLE_PLUS_OAUTH_EXPIRY_DATE";

            Provider = new WebAccountProvider(Id, DisplayName, new Uri("ms-appx:///icons/Google.png"));

            InitializeServerAccount();
        }

        public async override Task Login()
        {
            try
            {
                // try refresh token first
                RefreshToken = LocalSettings.LoadStorageValue<String>(LOCAL_OAUTH_TOKEN_REFRESH);
                if ((RefreshToken != null) && (RefreshToken != ""))
                    await UpdateToken();

                // refresh succeedeed, stop and return
                if (LoggedIn == WebAccountState.Connected)
                    return;

                String GooglePlusURL = "https://accounts.google.com/o/oauth2/auth?" +
                    "client_id=" + Uri.EscapeDataString(ClientId) +
                    "&redirect_uri=" + Uri.EscapeDataString(CallbackUrl) + 
                    "&response_type=code" +
                    "&scope=" + Uri.EscapeDataString("http://picasaweb.google.com/data");

                Uri StartUri = new Uri(GooglePlusURL);
                // When using the desktop flow, the success code is displayed in the html title of this end uri
                Uri EndUri = new Uri("https://accounts.google.com/o/oauth2/approval?");

                WebAuthenticationResult WebAuthenticationResult = await WebAuthenticationBroker.AuthenticateAsync(
                                                        WebAuthenticationOptions.UseTitle,
                                                        StartUri,
                                                        EndUri);
                if (WebAuthenticationResult.ResponseStatus == WebAuthenticationStatus.Success)
                {
                    String webAuthResultResponseData = WebAuthenticationResult.ResponseData.ToString();

                    // Get auth code
                    string responseData = webAuthResultResponseData.Substring(webAuthResultResponseData.IndexOf("code"));
                    String[] keyValPairs = responseData.Split('&');
                    string authorization_code = null;
                    for (int i = 0; i < keyValPairs.Length; i++)
                    {
                        String[] splits = keyValPairs[i].Split('=');
                        switch (splits[0])
                        {
                            case "code":
                                authorization_code = splits[1];
                                break;
                        }
                    }

                    // test if first step failed for some reason
                    if (authorization_code == null)
                        return;

                    // request token from auth code
                    GooglePlusURL = "https://accounts.google.com/o/oauth2/token";

                    HttpContent content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("code", authorization_code),
                        new KeyValuePair<string, string>("client_id", ClientId),
                        new KeyValuePair<string, string>("client_secret", Secret),
                        new KeyValuePair<string, string>("redirect_uri", CallbackUrl),
                        new KeyValuePair<string, string>("grant_type", "authorization_code")
                    });

                    HttpResponseMessage _response = null;
                    try
                    {
                        _response = await (new HttpClient()).PostAsync(new Uri(GooglePlusURL), content);
                    }
                    catch (HttpRequestException e)
                    {
                        Debug.WriteLine(e.Message);
                    }
                    if ((_response != null) && (_response.StatusCode == HttpStatusCode.OK))
                    {
                        LoggedIn = WebAccountState.Connected;
                        ResponseToken _responseToken = await Serialization.DeserializeHttpToJson<ResponseToken>(_response.Content) as ResponseToken;

                        Token = _responseToken.access_token;
                        RefreshToken = _responseToken.refresh_token;
                        ExpiryDate = DateTime.Now + new TimeSpan(0, 0, _responseToken.expires_in);

                        LocalSettings.SaveStorageValue<String>(LOCAL_OAUTH_TOKEN, Token);
                        LocalSettings.SaveStorageValue<String>(LOCAL_OAUTH_TOKEN_REFRESH, RefreshToken);
                        LocalSettings.SaveStorageValue<long>(LOCAL_OAUTH_EXPIRY_DATE, ExpiryDate.Ticks);

                        GetGooglePlusUserNameAsync(Token);
                    }
                }
            }
            catch (Exception Error)
            {
                Debug.WriteLine(Error.ToString());
            }
            return;
        }

        private async Task UpdateToken()
        {
            // request token from auth code
            String GooglePlusURL = "https://accounts.google.com/o/oauth2/token";
            String _refreshToken = (String)LocalSettings.LoadStorageValue<String>(LOCAL_OAUTH_TOKEN_SECRET);

            HttpContent content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", Id),
                new KeyValuePair<string, string>("refresh_token", _refreshToken),
                new KeyValuePair<string, string>("client_secret", Secret),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            });

            HttpResponseMessage _response = null;
            try
            {
                _response = await (new HttpClient()).PostAsync(new Uri(GooglePlusURL), content);
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine(e.Message);
            }
            if ((_response != null) && (_response.StatusCode == HttpStatusCode.OK))
            {
                LoggedIn = WebAccountState.Connected;
                ResponseToken _responseToken = await Serialization.DeserializeHttpToJson<ResponseToken>(_response.Content) as ResponseToken;

                Token = _responseToken.access_token;
                ExpiryDate = DateTime.Now + new TimeSpan(0, 0, _responseToken.expires_in);

                LocalSettings.SaveStorageValue<String>(LOCAL_OAUTH_TOKEN, Token);
                LocalSettings.SaveStorageValue<long>(LOCAL_OAUTH_EXPIRY_DATE, ExpiryDate.Ticks);
            }
        }

        private String GetGooglePlusUserNameAsync(string access_token)
        {
            //Request User info.
            //string _response = await (new HttpClient()).GetStringAsync(new Uri("https://www.googleapis.com/plus/v1/people/me?access_token=" + access_token));
            //if (_response != null)
            //{
            //    JsonObject value = JsonValue.Parse(_response).GetObject();
                UserName = "Google user";
                LocalSettings.SaveStorageValue<String>(LOCAL_USER_NAME, UserName);
                return UserName;
            //}
            //else
            //    return "";
        }

        public async override Task<SynchroManager.Status> Synchronize(SynchroHandle _syncHandle)
        {
            SynchroManager.Status _syncStatus = SynchroManager.Status.ErrorOrNotConnected;

            if (LoggedIn != WebAccountState.Connected)
                await SyncMngr.Login(SynchroManager.ServerName.GooglePlus);

            if (!Connection.InternetAccess())
                return _syncStatus;

            ProgressUpdate(Res.GetString("SynchroStart"), 0);
            
            HttpResponseMessage _response = await (new HttpClient()).GetAsync(new Uri(MAIN + "?access_token=" + Token));

            if ((_response != null) && (_response.StatusCode == HttpStatusCode.OK))
            {
                ResponseFeed _feed = await Serialization.DeserializeHttpToXml<ResponseFeed>(_response.Content, "feed", "http://www.w3.org/2005/Atom") as ResponseFeed;
            }

            Trip _trip = await Trip.LoadFromSummary(_syncHandle.TripSummary);

            return SynchroManager.Status.Synchronized;
        }

        public override Task Disconnect()
        {
            return null;
        }

        public override Task GoWebsite()
        {
            return null;
        }

        public override Task<Boolean> DeleteContent(TripSummary _summary)
        {
            return null;
        }



        public static async void CreateAlbumXml()
        {
            //String _dropboxURL = MAIN + "&token=" + Uri.EscapeDataString(Token);

            //HttpResponseMessage _response = null;
            //HttpClient httpClient = new HttpClient();
            StorageFile _template = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/templateCreateAlbum.xml"));
            XmlLoadSettings _settings = new XmlLoadSettings();
            XmlLoadSettings loadSettings = new XmlLoadSettings();
            loadSettings.ProhibitDtd = false;
            loadSettings.ResolveExternals = false;
            XmlDocument doc = await XmlDocument.LoadFromFileAsync(_template);

            var _section = doc.CreateAttribute("test2");

            // set location
            IXmlNode _group = doc.GetElementsByTagName("gphoto:location").Item(0);
            _group.ChildNodes.First<IXmlNode>().NodeValue = "NZ";

            StorageFile file_out = await ApplicationData.Current.LocalFolder.CreateFileAsync("test.xml", CreationCollisionOption.ReplaceExisting);
            await doc.SaveToFileAsync(file_out);
        }
    }
}
