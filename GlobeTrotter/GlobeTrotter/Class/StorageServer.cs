using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Windows.Security.Credentials;
using Windows.Storage;
using Windows.UI.ApplicationSettings;
using Windows.Networking.BackgroundTransfer;
using Windows.Web;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace GlobeTrotter
{
    public abstract class StorageServer
    {
        // public
        public String Id;
        public String DisplayName;
        public SynchroManager.ServerName Name;

        public String ClientId;
        public String Secret;
        public String CallbackUrl;
        public String State;
        public String UserName;
        public Boolean HasSecret;
        public Boolean HasExpiryDate; 
        public Boolean HasTokenRefresh;

        public String LOCAL_OAUTH_TOKEN;
        public String LOCAL_OAUTH_EXPIRY_DATE;
        public String LOCAL_OAUTH_TOKEN_SECRET;
        public String LOCAL_OAUTH_TOKEN_REFRESH;
        public String LOCAL_USER_NAME;

        public String Token;
        public String RefreshToken;
        public String TokenSecret;
        public DateTime ExpiryDate;
        public WebAccountProvider Provider;
        public Settings LocalSettings;
        public Page Parent;
        public List<UploadOperation> UploadList;
        public ResourceLoader Res;
        public WebAccountState LoggedIn;
        public SynchroManager SyncMngr;
        public SynchroManager.ComprLevel Compression;

        public abstract Task Login();
        public abstract Task Disconnect();
        public abstract Task GoWebsite();
        public abstract Task<Boolean> DeleteContent(TripSummary _summary);
        public abstract Task<SynchroManager.Status> Synchronize(SynchroHandle _syncHandle);

        // private
        WebAccount account;
        Boolean cts;
        
        public StorageServer()
        {
            Res = new ResourceLoader();
		}

        public void InitializeServerAccount()
        {
            //Initialize account if user was already logged in.
            TokenSecret = null;
            Token = (String)LocalSettings.LoadStorageValue<String>(LOCAL_OAUTH_TOKEN);
            if (HasSecret)
                TokenSecret = (String)LocalSettings.LoadStorageValue<String>(LOCAL_OAUTH_TOKEN_SECRET);
            if (HasExpiryDate)
                ExpiryDate = new DateTime((long)LocalSettings.LoadStorageValue<long>(LOCAL_OAUTH_EXPIRY_DATE));

            if ((Token == null) || (HasSecret && (TokenSecret == null)) ||
                (HasExpiryDate && !HasTokenRefresh && ((ExpiryDate == null) || (DateTime.Now > ExpiryDate))))
            {
                LoggedIn = WebAccountState.None;
            }
            else
            {
                Object User = LocalSettings.LoadStorageValue<String>(LOCAL_USER_NAME);
                if (User != null)
                {
                    UserName = User.ToString();
                    LoggedIn = WebAccountState.Connected;

                    account = new WebAccount(Provider, UserName, WebAccountState.Connected);
                }
            }
        }

        public WebAccountCommand GetAccount()
        {
            account = new WebAccount(Provider, UserName, LoggedIn);

            WebAccountCommand _accountCommand = new WebAccountCommand(
                account, WebAccountInvokedHandler,
                SupportedWebAccountActions.Remove |
                SupportedWebAccountActions.Manage |
                SupportedWebAccountActions.Reconnect);
            return _accountCommand;
        }

        private void WebAccountInvokedHandler(WebAccountCommand command, WebAccountInvokedArgs eventArgs)
        {
            switch (eventArgs.Action)
            {
                case WebAccountAction.Remove:
                    Cancel();
                    LocalSettings.RemoveStorageValue(LOCAL_USER_NAME);
                    LocalSettings.RemoveStorageValue(LOCAL_OAUTH_TOKEN);
                    LoggedIn = WebAccountState.None;
                    Disconnect();
                    break;
                case WebAccountAction.Reconnect:
                    Login();
                    break;
                case WebAccountAction.More:
                case WebAccountAction.ViewDetails:
                case WebAccountAction.Manage:
                    GoWebsite();
                    break;
                default:
                    break;
            }
        }

        public async Task<Boolean> UploadAll(List<UploadOperation> _uploder)
        {
            int _count = 0;
            foreach (UploadOperation _upload in _uploder)
            {
                // upload one file at a time
                ProgressUpdate(Res.GetString("Upload") + " \"" + _upload.SourceFile.Name + "\"", 100 * _count++ / _uploder.Count);
                Boolean _status = await UploadPutFile(_upload.RequestedUri, (StorageFile)_upload.SourceFile, Compression);
                if (!_status)
                {
                    ProgressFinished(Res.GetString("SyncFailRestartLater"));
                    return false;
                }
            }
            ProgressFinished("");
            return true;
        }

        private void MarshalProgress(string _description, int _progress)
        {
            var ignore = Parent.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (Parent is ViewHome)
                    (Parent as ViewHome).ProgressUpdate(_description, _progress);
                else if (Parent is ViewMapTrip)
                    (Parent as ViewMapTrip).UpdateProgress(_description, _progress);
            });
        }

        private bool IsExceptionHandled(string title, Exception ex, UploadOperation upload = null)
        {
            WebErrorStatus error = BackgroundTransferError.GetStatus(ex.HResult);
            if (error == WebErrorStatus.Unknown)
                return false;

            if (upload == null)
                MarshalProgress(String.Format(CultureInfo.CurrentCulture, "Error: {0}: {1}", title, error), 0);
            else
                MarshalProgress(String.Format(CultureInfo.CurrentCulture, "Error: {0} - {1}: {2}", upload.Guid, title, error), 0);

            return true;
        }

        // Note that this event is invoked on a background thread, so we cannot access the UI directly.
        private void UploadProgress(UploadOperation upload)
        {
            BackgroundUploadProgress _progress = upload.Progress;

            if (_progress.HasRestarted)
                MarshalProgress(" - Upload restarted", 0);
            else if (_progress.HasResponseChanged)
            {
                // We've received new response headers from the server.
                MarshalProgress(" - Response updated; Header count: " + upload.GetResponseInformation().Headers.Count, 0);

                // If you want to stream the response data this is a good time to start.
                // upload.GetResultStreamAt(0);
            }
            else
            {
                int _percent = 100;
                if (_progress.TotalBytesToSend > 0)
                    _percent = (int)(_progress.BytesSent * 100 / _progress.TotalBytesToSend);
                float _sent = _progress.BytesSent / 1000000;
                float _total = _progress.TotalBytesToSend / 1000000;
                MarshalProgress(String.Format(CultureInfo.CurrentCulture,
                    " Upload {0}: {1}MB of {2}MB", _progress.Status.ToString().ToLower(), _sent.ToString(),
                    _total.ToString()), _percent);
            }
        }

        public void Cancel()
        {
            ProgressFinished("");
            cts = true;
        }

        public static async Task<Boolean> UploadPutFile(Uri _requestUri, StorageFile _file, SynchroManager.ComprLevel _compression)
        {
            // compress to jpeg: 0 (no compression) -> 3 (high)
            if (_compression == SynchroManager.ComprLevel.Medium)
                _file = await Picture.CompressAndSaveFileAsync( _file.Path, ApplicationData.Current.LocalFolder.Path, "Compressed.jpg", 1920, false);
            else if (_compression == SynchroManager.ComprLevel.High)
                _file = await Picture.CompressAndSaveFileAsync( _file.Path, ApplicationData.Current.LocalFolder.Path, "Compressed.jpg", 1024, false);

            if (_file == null)
                return false;

            Stream _stream = await _file.OpenStreamForReadAsync();

            byte[] buffer;
            long originalPosition = 0;

            if (_stream.CanSeek)
            {
                originalPosition = _stream.Position;
                _stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = _stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = _stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
            }
            finally
            {
                if (_stream.CanSeek)
                    _stream.Position = originalPosition;
            }
            
            HttpClient _client = new HttpClient();
            try
            {
                HttpResponseMessage _response = await _client.PutAsync(_requestUri, new ByteArrayContent(buffer));
                return (_response.StatusCode == HttpStatusCode.OK);
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }

        public void ProgressUpdate(String _text, int _percent)
        {
            if ((Parent != null) && (Parent is ViewHome))
                (Parent as ViewHome).ProgressUpdate(_text, _percent);
        }

        public void ProgressFinished(String _text)
        {
            if ((Parent != null) && (Parent is ViewHome))
            {
                (Parent as ViewHome).ProgressFinished(_text);
                (Parent as ViewHome).StopImport();
            }
        }

        public Boolean CancelInProgress()
        {
            return cts;
        }

        public void ResetCts()
        {
            cts = false;
        }
    }
}
