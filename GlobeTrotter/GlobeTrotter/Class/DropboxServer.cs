using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using DropboxsService.Common.JSON;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.Security.Credentials;
using Windows.Security.Authentication.Web;
using Windows.Data.Json;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.System;

namespace GlobeTrotter
{
    public class DropboxServer : StorageServer
    {
        SynchroHandle _handle;

        public DropboxServer(Settings _settings)
        {
            LocalSettings = _settings;

            Id = "dropbox.com";
            DisplayName = "Dropbox";
            Name = GlobeTrotter.SynchroManager.ServerName.Dropbox;

            ClientId = "duoxxdmgcsmjmwh";
            Secret = "o8zcoptn4t1cyog";
            CallbackUrl = "https://www.google.com";
            State = "dkjnsdckjhbsfejhbsf";

            HasSecret = false;
            HasExpiryDate = false;
            HasTokenRefresh = false;

            LOCAL_OAUTH_TOKEN = "DROPBOX_OAUTH_TOKEN";
            LOCAL_USER_NAME = "DROPBOX_USER_NAME";
            LOCAL_OAUTH_EXPIRY_DATE = "DROPBOX_OAUTH_EXPIRY_DATE";

            Provider = new WebAccountProvider(Id, DisplayName, new Uri("ms-appx:///icons/Dropbox.png"));

            InitializeServerAccount();
        }

        public async override Task Login()
        {
            String _dropboxURL = DROPBOX_SITE_BASE + "oauth2/authorize?client_id=" + Uri.EscapeDataString(ClientId) +
                //"&redirect_uri=" + Uri.EscapeDataString(CallbackUrl) +
                "&response_type=code" +
                "&state=" + Uri.EscapeDataString(State);

            Popup _popup = new Popup();
            _popup.Closed += (senderPopup, argsPopup) => { _popup = null; };
            _popup.HorizontalOffset = Window.Current.Bounds.Width / 2 - 220;
            _popup.VerticalOffset = Window.Current.Bounds.Height / 2 - 120;

            _popup.Child = new PopupDropboxAuth(new Uri(_dropboxURL), Login_callback, Res);
            _popup.IsOpen = true;

            // bypass error message, leave this
            if (_dropboxURL == "")
                await GoWebsite();
        }
                
        public async Task Login_callback(String _code)
        {
            String _dropboxURL = "https://api.dropbox.com/1/oauth2/token?code=" + Uri.EscapeDataString(_code) + 
                "&grant_type=authorization_code";

            HttpResponseMessage _response = null;
            HttpClient httpClient = new HttpClient();

            HttpContent content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", Secret)
            });

            try
            {
                _response = await httpClient.PostAsync(new Uri(_dropboxURL), content);
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine(e.Message);
            }
            if ((_response != null) && (_response.StatusCode == HttpStatusCode.OK))
            {
                ResponseToken _t = await Serialization.DeserializeHttpToJson<ResponseToken>(_response.Content) as ResponseToken;

                Token = _t.access_token;
                LocalSettings.SaveStorageValue<String>(LOCAL_OAUTH_TOKEN, Token);

                //Get UserName
                String _respJson = await httpClient.GetStringAsync(new Uri("https://api.dropbox.com/1/account/info?access_token=" + Token));
                if (_response != null)
                {
                    var value = JsonValue.Parse(_respJson).GetObject();
                    UserName = value.GetNamedString("display_name");
                    LocalSettings.SaveStorageValue<String>(LOCAL_USER_NAME, UserName);

                    //end of auth process
                    LoggedIn = WebAccountState.Connected;

                    // force synchronization
                    if (_handle != null)
                    {
                        _handle.SetSynchroStatus(SynchroManager.Status.InProgress);
                        await Synchronize(_handle);
                    }
                }
            }

            return;
        }

        public async override Task<Boolean> DeleteContent(TripSummary _summary)
        {
            return await Delete(_summary.FolderTopName);
        }

        public async override Task Disconnect()
        {
            String _request = API_TOKEN_DISABLE + "?access_token=" + Token;
            await SendRequest(_request);
        }

        public async override Task GoWebsite()
        {
            HttpClient httpClient = new HttpClient();
            try
            {
                string response = await httpClient.GetStringAsync(new Uri(API_ACCOUNT_INFO + "?access_token=" + Token));
                JsonObject value = JsonValue.Parse(response).GetObject();
                await Launcher.LaunchUriAsync(new Uri(value.GetNamedString("referral_link")));
            }
            catch (HttpRequestException)
            {
            }
        }

        public async override Task<SynchroManager.Status> Synchronize(SynchroHandle _syncHandle)
        {
            _handle = _syncHandle;

            SynchroManager.Status _syncStatus = SynchroManager.Status.ErrorOrNotConnected;

            if (!Connection.InternetAccess())
                return _syncStatus;

            ProgressUpdate(Res.GetString("SynchroStart"), 0);

            //list top-folder, login if not logged (reentrant)
            ResponseContainer _folderDesc = await MetadataTopFolder(SyncMngr, true);

            if ((_folderDesc != null) && (_folderDesc.contents != null))
            {
                Boolean _status = true;

                List<String> _listTripNames = new List<string>();
                foreach (ResponseElement _element in _folderDesc.contents)
                    if (_element.is_dir)
                        _listTripNames.Add(_element.path);

                // check if trip folder name match, rename if necessary
                foreach (String _tripName in _listTripNames)
                {
                    // no error check since the folder may not exist
                    if ((_syncHandle.TripSummary.SyncDropbox.PreviousName != null) && (_syncHandle.TripSummary.SyncDropbox.PreviousName != ""))
                        if (_syncHandle.TripSummary.SyncDropbox.PreviousName == _tripName)
                            await RenameFolder(_tripName, _syncHandle.TripSummary.FolderTopName);

                    // check cancellation
                    if (CancelInProgress())
                        return SynchroManager.Status.NoRequest;
                }

                Trip _trip = await Trip.LoadFromSummary(_syncHandle.TripSummary);

                UploadList = new List<UploadOperation>();

                _status &= await SynchronizeTrip(_trip);

                // check error status
                if (CancelInProgress())
                    return SynchroManager.Status.NoRequest;
                else if (!_status)
                    return SynchroManager.Status.ErrorOrNotConnected;
 
                _status &= await UploadAll(UploadList);

                // check error status
                if (CancelInProgress())
                    _syncStatus = SynchroManager.Status.NoRequest;
                else if (!_status)
                    _syncStatus = SynchroManager.Status.ErrorOrNotConnected;
                else
                    _syncStatus = SynchroManager.Status.Synchronized;

                return _syncStatus;
            }
            else
            {
                ProgressFinished("");
                return SynchroManager.Status.ErrorOrNotConnected;
            }
        }

        public async Task<Boolean> SynchronizeTrip(Trip _trip)
        {
            Boolean _status = true;
            ResponseContainer _folderDesc = null;

            ProgressUpdate(Res.GetString("SynchroTrip") + " " + _trip.Summary.FolderTopName, 0);

            String _request = API_METADATA + "/sandbox/" + _trip.Summary.FolderTopName + 
                "?access_token=" + Token + "&hash=" + _trip.Hash;
            HttpResponseMessage _response = await SendRequest(_request);

            if (_response != null)
            {
                if (_response.StatusCode == HttpStatusCode.OK)
                    _folderDesc = await Serialization.DeserializeHttpToJson<ResponseContainer>(_response.Content) as ResponseContainer;
                else if (_response.StatusCode == HttpStatusCode.NotFound)
                    _folderDesc = await CreateFolder(_trip.Summary.FolderTopName);
                else if (_response.StatusCode == HttpStatusCode.NotModified)
                    return true;
            }

            if (_folderDesc.contents != null)
            {
                //check if all dropbox folders are synchronized with local folders
                foreach (ResponseElement _element in _folderDesc.contents)
                {
                    if (_element.is_dir)
                    {
                        Boolean _found = false;
                        foreach (Album _album in _trip.Albums)
                        {
                            if ("/" + _trip.DisplayName + "/" + _album.PathAlbum == _element.path)
                            {
                                _status &= await synchronizeAlbum(_album, _trip.DisplayName, _trip.Summary);
                                _found = true;
                            }
                            if (!_status)
                                return false;
                        }
                        if (!_found)
                            _status &= await Delete(_element.path);
                    }
                    else
                        //not a folder, delete file
                        _status &= await Delete(_element.path);
                }
            }
            //check if all local folders are synchronized with dropbox folders
            foreach (Album _album in _trip.Albums)
            {
                Boolean _found = false;

                if (_folderDesc.contents != null)
                {
                    foreach (ResponseElement _element in _folderDesc.contents)
                    {
                        if (_album.DisplayName == _element.path)
                            //already synchronized
                            _found = true;
                    }
                }
                if (!_found)
                    _status &= await synchronizeAlbum(_album, _trip.DisplayName, _trip.Summary);
                if (!_status || CancelInProgress())
                    return false;
            }

            //get hash code of synchronized folder
            _request = API_METADATA + "/sandbox/" + _trip.Summary.FolderTopName + "?access_token=" + Token;
            _response = await SendRequest(_request);

            if ((_response != null) && (_response.StatusCode == HttpStatusCode.OK))
            {
                _folderDesc = await Serialization.DeserializeHttpToJson<ResponseContainer>(_response.Content) as ResponseContainer;
                _trip.Hash = _folderDesc.hash;
                return true;
            }

            //should never reach this code
            return false;
        }

        private async Task<Boolean> synchronizeAlbum(Album _album, String _pathTrip, TripSummary _summary)
        {
            String _pathAlbum = "/" + _pathTrip + "/" + _album.PathAlbum;
            ResponseContainer _folderDesc = null;

            ProgressUpdate(Res.GetString("SynchroAlbum") + " \"" + _album.Summary.Name + "\"", 0);

            //list top-folder
            String _request = API_METADATA + "/sandbox/"+ _pathAlbum + "?access_token=" + Token + "&hash=" + _album.Hash;
            HttpResponseMessage _response = await SendRequest(_request);

            if (_response != null)
            {
                if (_response.StatusCode == HttpStatusCode.OK)
                    _folderDesc = await Serialization.DeserializeHttpToJson<ResponseContainer>(_response.Content) as ResponseContainer;
                else if (_response.StatusCode == HttpStatusCode.NotFound)
                    _folderDesc = await CreateFolder(_pathTrip + "/" + _album.PathAlbum);
            }

            if (_folderDesc != null)
            {
                Boolean _status = true;
                if ((_folderDesc.contents != null) && (_folderDesc.contents.Count() != 0))
                {
                    //check if all dropbox pictures are synchronized with local pictures, delete if not in local folder
                    foreach (ResponseElement _element in _folderDesc.contents)
                    {
                        if (_element.is_dir)
                            _status &= await Delete(_element.path);
                        else
                        {
                            Boolean _found = false;
                            foreach (Picture _picture in _album.Pictures)
                            {
                                if (_pathAlbum + "/" + _picture.Name + _picture.Extension == _element.path)
                                    _found = true;
                            }
                            if (!_found)
                                _status &= await Delete(_element.path);
                        }
                    }
                    if (!_status || CancelInProgress())
                        return false;
                }

                //check if all local pictures are synchronized with dropbox pictures 
                int _countParts = 0;                           
                foreach (Picture _picture in _album.Pictures)
                {
                    Boolean _found = false;
                    if (_folderDesc.contents != null)
                        foreach (ResponseElement _element in _folderDesc.contents)
                            if (_pathAlbum + "/" + _picture.Name + _picture.Extension == _element.path)
                                _found = true;

                    if (!_found)
                    {
                        UploadOperation _newUpload = await synchronizePicture(_picture, _pathAlbum, _countParts++, _summary);
                        if (_newUpload != null)
                            UploadList.Add(_newUpload);
                    }

                    if (CancelInProgress())
                        return false;
                }
                return true;
            }
            return false;
        }

        private async Task<UploadOperation> synchronizePicture(Picture _picture, String _pathAlbum, int _index, TripSummary _summary)
        {
            StorageFile _file;
            BackgroundUploader _uploader = new BackgroundUploader();
            UploadOperation _upload = null;

            String _request = API_FILES_PUT + "/sandbox/" + _pathAlbum + "/" + _picture.Name + _picture.Extension + 
                "?access_token=" + Token + "&overwrite=true";
            try
            {
                if (_summary.Sample)
                    _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/" + _summary.PathThumb + "/" + _summary.PicturesThumb[_index%4]));
                else
                    _file = await StorageFile.GetFileFromPathAsync(_picture.GetPath());
            }
            catch (FileNotFoundException)
            {
                return null;
            }

            try
            {
                _upload = _uploader.CreateUpload(new Uri(_request), _file);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            return _upload;
        }

        public async Task<Boolean> Delete(String _path)
        {
            String _request = API_FILEOPS_DELETE + "?access_token=" + Token + "&root=sandbox" + "&path=" + _path;
            HttpResponseMessage _response = await SendRequest(_request);
            return ((_response != null) && (_response.StatusCode == HttpStatusCode.OK));
        }

        public async Task<ResponseContainer> CreateFolder(String _path)
        {
            String _request = API_FILEOPS_CREATE + "?access_token=" + Token + "&root=sandbox&" + "path=" + (_path);
            HttpResponseMessage _response = await SendRequest(_request);
            return await Serialization.DeserializeHttpToJson<ResponseContainer>(_response.Content) as ResponseContainer;
        }

        public async Task<Boolean> RenameFolder(String _pathOld, String _pathNew)
        {
            String _request = API_FILEOPS_MOVE + "?access_token=" + Token + "&root=sandbox&" +
                "from_path=" + Uri.EscapeDataString(_pathOld) +
                "to_path=" + Uri.EscapeDataString(_pathNew);
            HttpResponseMessage _response = await SendRequest(_request);
            return (_response.StatusCode == HttpStatusCode.OK);
        }

        private static async Task<HttpResponseMessage> SendRequest(String _request)
        {
            HttpResponseMessage response = null;
            try
            {
                return response = await (new HttpClient()).GetAsync(new Uri(_request));
            }
            catch (HttpRequestException)
            {
                // not connected, or connexion failed for some reason
                return null;
            }
        }
        
        public async Task<ResponseContainer> MetadataTopFolder(SynchroManager _manager, Boolean _reentrant)
        {
            String _request = API_METADATA + "/sandbox?access_token=" + Token;
            HttpResponseMessage _response = await SendRequest(_request);

            if (_response != null)
            {
                if (_response.StatusCode == HttpStatusCode.OK)
                    return await Serialization.DeserializeHttpToJson<ResponseContainer>(_response.Content) as ResponseContainer;
                else if ((_response.StatusCode == HttpStatusCode.Unauthorized) || (_response.StatusCode == HttpStatusCode.NotFound))
                {
                    if (_reentrant)
                        await Login();
                        //return await MetadataTopFolder(_manager, false);

                    return null;
                }
                else
                    ManageError(_response.StatusCode);
            }
            return null;
        }

        private void ManageError(HttpStatusCode _code)
        {
            // list all existing errors
            if (_code == HttpStatusCode.Unauthorized)
            {
                // 401: Bad or expired token
                Login();
            }
            else if (_code == HttpStatusCode.Forbidden)
            {
                // 403: Bad OAuth request
                Toast.DisplayTwoLines("Upload to Dropbox failed", "Bad OAuth request", "Icons/toastImageAndText.png");
            }
            else if (_code == HttpStatusCode.NotFound)
            {
                // 404: File or folder not found at the specified path.
                Toast.DisplayTwoLines("Upload to Dropbox failed", "File or folder not found", "Icons/toastImageAndText.png");
            }
            else if (_code == HttpStatusCode.MethodNotAllowed)
            {
                // 405: Request method not expected.
                Toast.DisplayTwoLines("Upload to Dropbox failed", "Request method not expected", "Icons/toastImageAndText.png");
            }
            else if ((int)_code == 429)
            {
                // 429: Your app is making too many requests and is being rate limited.
                Toast.DisplayTwoLines("Upload to Dropbox failed", "Too many requests", "Icons/toastImageAndText.png");
            }
            else if ((int)_code == 503)
            {
                // 503: transient server error
                Toast.DisplayTwoLines("Upload to Dropbox failed", "Upload will restart at next startup", "Icons/toastImageAndText.png");
            }
            else if ((int)_code == 507)
            {
                // 507: User is over Dropbox storage quota
                Toast.DisplayTwoLines("Upload to Dropbox failed", "Over quota", "Icons/toastImageAndText.png");
            }
            else if (((int)_code)/100 == 5)
            {
                // 5xx: server error
                Toast.DisplayTwoLines("Upload to Dropbox failed", "Server error", "Icons/toastImageAndText.png");
            }
        }

        private static String DROPBOX_SITE_BASE = "https://www.dropbox.com/1/";
        private static String DROPBOX_API_BASE = "https://api.dropbox.com/1/";
        private static String DROPBOX_API_CONTENT_BASE = "https://api-content.dropbox.com/1/";
        //private static String DROPBOX_API_NOTIFY_BASE = "https://api-notify.dropbox.com/1/";

        private static String API_TOKEN_DISABLE = DROPBOX_API_BASE + "disable_access_token";
        private static String API_ACCOUNT_INFO = DROPBOX_API_BASE + "account/info";

        //private static String API_FILES_GET = DROPBOX_API_CONTENT_BASE + "files";
        private static String API_FILES_PUT = DROPBOX_API_CONTENT_BASE + "files_put";
        private static String API_METADATA = DROPBOX_API_BASE + "metadata";
        //private static String API_DELTA = DROPBOX_API_BASE + "delta";
        //private static String API_LONG_DELTA = DROPBOX_API_NOTIFY_BASE + "longpoll_delta";
        //private static String API_REVISIONS = DROPBOX_API_BASE + "revisions";
        //private static String API_RESTORE = DROPBOX_API_BASE + "restore";
        //private static String API_SEARCH = DROPBOX_API_BASE + "search";
        //private static String API_MEDIA = DROPBOX_API_BASE + "media";
        //private static String API_COPY_REF = DROPBOX_API_BASE + "copy_ref";
        //private static String API_CHUNK_UPLOAD = DROPBOX_API_CONTENT_BASE + "chunked_upload";
        //private static String API_THUMBNAILS = DROPBOX_API_CONTENT_BASE + "thumbnails";
        //private static String API_CHUNK_UPLOAD_COMMIT = DROPBOX_API_CONTENT_BASE + "commit_chunked_upload";

        //private static String API_FILEOPS_COPY = DROPBOX_API_BASE + "fileops/copy";
        private static String API_FILEOPS_CREATE = DROPBOX_API_BASE + "fileops/create_folder";
        private static String API_FILEOPS_DELETE = DROPBOX_API_BASE + "fileops/delete";
        private static String API_FILEOPS_MOVE = DROPBOX_API_BASE + "fileops/move";

    }
}
