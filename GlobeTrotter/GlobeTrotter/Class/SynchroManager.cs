using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web;
using Windows.Security.Credentials;
using Windows.Data.Json;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI.ApplicationSettings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;

namespace GlobeTrotter
{
    public class SynchroHandle
    {
        public Boolean Requested;
        public Boolean Finished;
        public Boolean InProgress;
        public String PreviousName;
        public SynchroManager.ServerName Server;
        public SynchroManager.ComprLevel Compression;

        // UserControl specific
        Image logo;
        Rectangle rectangle;
        TripDescUserControl tripDescUc;

        public SynchroHandle()
        { }

        public SynchroHandle(SynchroManager.ServerName _server)
        {
            Server = _server;
            Compression = SynchroManager.ComprLevel.Undefined;
        }

        public void SetUcSpecific(Image _logo, Rectangle _rectangle, TripDescUserControl _tripDescUc)
        { 
            logo = _logo;
            rectangle = _rectangle;
            tripDescUc = _tripDescUc;
        }

        public void UpdateRectangle(Visibility _visibility, SolidColorBrush _color)
        {
            rectangle.Visibility = _visibility;
            rectangle.Fill = _color;
        }

        public void BlinkRectangle()
        {
            rectangle.Visibility = (rectangle.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
        }

        public TripSummary TripSummary
        {
            get { return tripDescUc.TripSummary; }
        }

        public void SetSynchroStatus(SynchroManager.Status _status)
        {
            tripDescUc.SetSynchroStatus(this, _status);
        }
    }

    public class SynchroManager
    {
        public enum ServerName
        {
            Dropbox,
            Usb,
            GooglePlus,
            Facebook
        }

        public enum ComprLevel
        {
            Undefined,
            Original,
            Medium,
            High
        }

        public enum Status
        {
            NoRequest,             // not synchronization request => rect not visible
            ErrorOrNotConnected,   // synchronization request but not connected => rect visible red
            InProgress,            // synchronization request connected and in progress => rect blinking orange
            Synchronized           // synchronization request connected but finished => rect visible green
        }

        DropboxServer _dropboxServer;
        UsbServer _usbServer;
#if STEP2
        FacebookServer _facebookServer;
        GooglePlusServer _googlePlusServer;
#endif
        List<SynchroHandle> _listSyncHandle;
        Boolean _syncInProgress;

        public Page Parent;

        public SynchroManager(Settings _settings)
        {
            _dropboxServer = new DropboxServer(_settings);
            _usbServer = new UsbServer(_settings);
#if STEP2
            _facebookServer = new FacebookServer(_settings);
            _googlePlusServer = new GooglePlusServer(_settings);
#endif

            _listSyncHandle = new List<SynchroHandle>();
        }

        public static int ComprToInt(ComprLevel _compr)
        {
            switch (_compr)
            {
                case ComprLevel.High:
                    return 0;
                case ComprLevel.Medium:
                    return 1;
                case ComprLevel.Original:
                    return 2;
                default:
                    return -1;
            }
        }

        public static ComprLevel IntToCompr(int _level)
        {
            switch (_level)
            {
                case 0:
                    return ComprLevel.High;
                case 1:
                    return ComprLevel.Medium;
                case 2:
                    return ComprLevel.Original;
                default:
                    return ComprLevel.Undefined;
            }
        }

        public StorageServer CloudServerFromName(ServerName _provider)
        {
            switch (_provider)
            {
#if STEP2
                case ServerName.Facebook:
                    return _facebookServer;
                case ServerName.GooglePlus:
                    return _googlePlusServer;
#endif
                case ServerName.Dropbox:
                    return _dropboxServer;
                case ServerName.Usb:
                    return _usbServer;
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        public Boolean LoggedIn(ServerName _server)
        {
            return (CloudServerFromName(_server).LoggedIn == WebAccountState.Connected);
        }

        private ServerName ConvertIdToServer(String _id)
        {
            if (_id.Equals(_dropboxServer.Id))
                return ServerName.Dropbox;
            else if (_id.Equals(_usbServer.Id))
                return ServerName.Usb;
#if STEP2
            else if (_id.Equals(_facebookServer.Id))
                return ServerName.Facebook;
            else if (_id.Equals(_googlePlusServer.Id))
                return ServerName.GooglePlus;
#endif
            else
                throw new ArgumentOutOfRangeException();
        }

        public async Task<Boolean> LoginFromId(String _id)
        {
            return await Login(ConvertIdToServer(_id));
        }

        public async Task<Boolean> Login(ServerName _server)
        {
            StorageServer _manager = CloudServerFromName(_server);

            if (_manager.LoggedIn != WebAccountState.Connected)
            {
                await _manager.Login();
                if ((_manager.Token != null) && (_manager.UserName != null) && 
                    (_manager.Token != "") && (_manager.UserName != ""))
                {
                    _manager.LoggedIn = WebAccountState.Connected;
                    return true;
                }
                else
                    return false;
            }
            else
                return true;
        }

        internal async Task Synchronize()
        {
            if (_syncInProgress)
                return;

            _syncInProgress = true;

            while (_listSyncHandle.Count > 0)
            {
                SynchroHandle _syncHandle = _listSyncHandle.First<SynchroHandle>();
                Status _status = SynchroManager.Status.ErrorOrNotConnected;

                StorageServer _server = CloudServerFromName(_syncHandle.Server);
                _server.SyncMngr = this;
                _server.Compression = _syncHandle.Compression;
                if (Parent != null)
                    _server.Parent = Parent;

                // start synchronization, reset Cts before
                _syncHandle.InProgress = true;
                _server.ResetCts();
                _status = await _server.Synchronize(_syncHandle);
                _syncHandle.InProgress = false;

                if (_status == Status.Synchronized)
                    _syncHandle.Finished = true;

                _syncHandle.SetSynchroStatus(_status);

                removeFromQueue(_syncHandle);

                if ((_listSyncHandle.Count == 0) && (Parent != null) && (Parent is ViewHome))
                    await (Parent as ViewHome).SaveSummary();
            }
        
            _syncInProgress = false;
        }

        public void UpdateAll(TripSummary tripSummary)
        {
            Update(tripSummary.SyncDropbox);
            Update(tripSummary.SyncUsb);
#if STEP2
            Update(tripSummary.SyncGooglePlus);
            Update(tripSummary.SyncFacebook);
#endif
        }

        public void Update(SynchroHandle _syncHandle)
        {
            if ((_syncHandle.Requested && _syncHandle.Finished))
                _syncHandle.SetSynchroStatus(Status.Synchronized);
            else if ((_syncHandle.Requested && !_syncHandle.Finished))
            {
                _syncHandle.SetSynchroStatus(Status.InProgress);
                addToQueue(_syncHandle);
            }
            else
            {
                _syncHandle.SetSynchroStatus(Status.NoRequest);
                removeFromQueue(_syncHandle);
            }
        }

        private async void addToQueue(SynchroHandle _syncObj)
        {
            _listSyncHandle.Add(_syncObj);
            await Synchronize();
        }

        private void removeFromQueue(SynchroHandle _syncObj)
        {
            for (int _idx = 0; _idx < _listSyncHandle.Count; _idx++)
            {
                if ((_listSyncHandle[_idx].Server == _syncObj.Server) && 
                    (_listSyncHandle[_idx].TripSummary.Id == _syncObj.TripSummary.Id))
                {
                    _listSyncHandle.RemoveAt(_idx);
                    break;
                }
            }

            if (_syncObj.InProgress)
                Cancel(_syncObj);
        }

        internal void CancelAll()
        {
            _listSyncHandle.Clear();

            _dropboxServer.Cancel();
            _usbServer.Cancel();
#if STEP2
            _googlePlusServer.Cancel();
            _facebookServer.Cancel();
#endif
        }

        public void Cancel(SynchroHandle _syncHandle)
        {
            StorageServer _manager = CloudServerFromName(_syncHandle.Server);
            _manager.Cancel();
        }

        internal async Task<Boolean> DeleteAll(TripDescUserControl _tripDesc)
        {
            Boolean _status = true;
            _status &= await DeleteContent(_tripDesc.TripSummary.SyncDropbox);
            _status &= await DeleteContent(_tripDesc.TripSummary.SyncUsb);
#if STEP2
            _status &= await DeleteContent(_tripDesc.TripSummary.SyncGooglePlus);
            _status &= await DeleteContent(_tripDesc.TripSummary.SyncFacebook);
#endif
            return _status;
        }

        internal async Task<Boolean> DeleteContent(SynchroHandle _syncHandle)
        {
            if (_syncHandle.Requested)
            {
                StorageServer _manager = CloudServerFromName(_syncHandle.Server);
                return await _manager.DeleteContent(_syncHandle.TripSummary);
            }
            else
                return true;
        }

        internal WebAccountProvider GetProvider(ServerName providerName)
        {
            StorageServer _manager = CloudServerFromName(providerName);
            return _manager.Provider;
        }

        public WebAccountCommand GetAccount(ServerName providerName)
        {
            StorageServer _manager = CloudServerFromName(providerName);
            return _manager.GetAccount();
        }
    }
}
