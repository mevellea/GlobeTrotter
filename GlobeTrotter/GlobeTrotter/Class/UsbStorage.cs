using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.System;
using Windows.Devices;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace GlobeTrotter
{
    public class UsbServer : StorageServer
    {
        DeviceSelector _selector;
        SynchroHandle _syncHandleSav;

        public UsbServer(Settings _settings)
        {
            LocalSettings = _settings;

            Id = "usb";
            DisplayName = "Usb";
            Name = GlobeTrotter.SynchroManager.ServerName.Usb;

            ClientId = "";
            CallbackUrl = "";
            State = "";

            HasSecret = false;
            HasExpiryDate = false;

            LOCAL_OAUTH_TOKEN = "USB_OAUTH_TOKEN";
            LOCAL_USER_NAME = "USB_USER_NAME";
            LOCAL_OAUTH_TOKEN_REFRESH = "USB_OAUTH_TOKEN_REFRESH";

            Provider = new WebAccountProvider(Id, DisplayName, new Uri("ms-appx:///icons/USB-Drive-icon.png"));

            _selector = new DeviceSelector(Res);

            InitializeServerAccount();
        }
        
        public override async Task Login()
        {
            //if ((String)LocalSettings.LoadStorageValue<String>(LOCAL_OAUTH_TOKEN) == null)
            await _selector.ShowPopupDevice(LoginCallback);
        }

        async Task LoginCallback(String _id)
        {
            StorageFolder _storage = await getDeviceGTFolder(_id);

            if (_storage != null)
            {
                LoggedIn = WebAccountState.Connected;
                UserName = _storage.Path;

                LocalSettings.SaveStorageValue<String>(LOCAL_OAUTH_TOKEN, _id);
                LocalSettings.SaveStorageValue<String>(LOCAL_USER_NAME, UserName);
                LocalSettings.SaveStorageValue<String>(LOCAL_OAUTH_TOKEN_REFRESH, _storage.Path);

                if (_syncHandleSav != null)
                    await Synchronize(_syncHandleSav);
            }
            else if (_syncHandleSav != null)
                _syncHandleSav.SetSynchroStatus(SynchroManager.Status.ErrorOrNotConnected);
        }

        async Task<StorageFolder> getDeviceGTFolder(String _id)
        {
            try
            {
                StorageFolder _storage = StorageDevice.FromId(_id);
                return await _storage.CreateFolderAsync("GlobeTrotter", CreationCollisionOption.OpenIfExists);
            }
            catch
            {
                return null;
            }
        }

        public override Task Disconnect()
        {
            return default(Task);
        }
        
        public override Task GoWebsite()
        {
            return default(Task);
        }

        async Task DeleteRecursive(StorageFolder _folderTop)
        {
            IReadOnlyList<IStorageItem> _items = await _folderTop.GetItemsAsync();
            foreach (IStorageItem _item in _items)
            {
                if (_item.IsOfType(StorageItemTypes.Folder))
                    await DeleteRecursive(_item as StorageFolder);
                else
                    await (_item as StorageFile).DeleteAsync(StorageDeleteOption.PermanentDelete);
            }

            if ((await _folderTop.GetItemsAsync()).Count() == 0)
                await _folderTop.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }

        public async override Task<Boolean> DeleteContent(TripSummary _summary)
        {
            try
            {
                String _path = LocalSettings.LoadStorageValue<String>(LOCAL_OAUTH_TOKEN_REFRESH);
                StorageFolder _rootGT, _rootTrip;
                try
                {
                    _rootGT = await StorageFolder.GetFolderFromPathAsync(_path);
                }
                catch
                {
                    // device not available, return error
                    return false;
                }
                try
                {
                    _rootTrip = await _rootGT.GetFolderAsync(_summary.FolderTopName);
                }
                catch
                {
                    // device available, folder already deleted
                    return true;
                }
                await DeleteRecursive(_rootTrip);
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }
        }

        public async override Task<SynchroManager.Status> Synchronize(SynchroHandle _syncHandle)
        {
            Boolean _status = true;
            SynchroManager.Status _syncStatus = SynchroManager.Status.NoRequest;

            if (LoggedIn != WebAccountState.Connected)
            {
                _syncHandleSav = _syncHandle;
                _status = await SyncMngr.Login(SynchroManager.ServerName.Usb);
            }
            if (_status)
            {
                Trip _trip = await Trip.LoadFromSummary(_syncHandle.TripSummary);
                _status &= await SynchronizeTrip(_trip);
                ProgressFinished("");

                // check error status
                if (CancelInProgress())
                    _syncStatus = SynchroManager.Status.NoRequest;
                else if (!_status)
                    _syncStatus = SynchroManager.Status.ErrorOrNotConnected;
                else
                    _syncStatus = SynchroManager.Status.Synchronized;
            }
            else
                _syncStatus = SynchroManager.Status.ErrorOrNotConnected;

            _syncHandle.SetSynchroStatus(_syncStatus);
            return _syncStatus;
        }

        public async Task<Boolean> SynchronizeTrip(Trip _trip)
        {
            Boolean _status = true;

            String _id = LocalSettings.LoadStorageValue<String>(LOCAL_OAUTH_TOKEN);
            StorageFolder _rootGT = await getDeviceGTFolder(_id);
            if (_rootGT == null)
                return false;

            ProgressUpdate(Res.GetString("SynchroTrip") + " " + _trip.Summary.FolderTopName, 0);

            IReadOnlyList<StorageFolder> _folders = await _rootGT.GetFoldersAsync();
            StorageFolder _rootTrip = null;
            Boolean _found = false;
            String _tripName = _trip.Summary.FolderTopName;
            foreach (StorageFolder _item in _folders)
            {
                if (_item.Name.Equals(_tripName))
                {
                    _rootTrip = _item as StorageFolder;
                    _found = true;
                    break;
                }
            }

            if (!_found)
                _rootTrip = await _rootGT.CreateFolderAsync(_tripName, CreationCollisionOption.OpenIfExists);

            _folders = await _rootTrip.GetFoldersAsync();

            //check if all dropbox folders are synchronized with local folders
            foreach (Album _album in _trip.Albums)
            {
                _found = false;
                StorageFolder _folderAlbum = null;
                foreach (StorageFolder _item in _folders)
                {
                    if ((_rootTrip.Path + "\\" + _album.DisplayName == _item.Path))
                    {
                        _folderAlbum = _item;
                        _found = true;
                        break;
                    }
                }
                if (!_found)
                    _folderAlbum = await _rootTrip.CreateFolderAsync(_album.DisplayName, CreationCollisionOption.OpenIfExists);

                _status &= await synchronizeAlbum(_album, _folderAlbum, _trip.Summary);
                    
                if (!_status || CancelInProgress())
                    return false;
            }

            return true;
        }

        private async Task<Boolean> synchronizeAlbum(Album _album, StorageFolder _folderAlbum, TripSummary _summary)
        {
            Boolean _found = false;
            Boolean _status = true;

            IReadOnlyList<IStorageItem> _items = await _folderAlbum.GetItemsAsync();
            
            foreach (Picture _picture in _album.Pictures)
            {
                ProgressUpdate(Res.GetString("Upload") + " \"" + _picture.Name + _picture.Extension + "\"", 100 * _album.Pictures.IndexOf(_picture) / _album.Pictures.Count);

                foreach (StorageFile _item in _items)
                {
                    if (_item.Name.Equals(_picture.Name + _picture.Extension))
                    {
                        _found = true;
                        break;
                    }
                }
                if (!_found)
                    _status &= await synchronizePicture(_picture, _folderAlbum, _album.Pictures.IndexOf(_picture), _summary);

                if (!_status || CancelInProgress())
                    return false;
            }
            return true;
        }

        private async Task<Boolean> synchronizePicture(Picture _picture, StorageFolder _folderAlbum, int _index, TripSummary _summary)
        {
            StorageFile _file = null;
            try
            {
                if (_summary.Sample)
                    _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/" + _summary.PathThumb + "/" + _summary.PicturesThumb[_index % 4]));
                else
                    _file = await StorageFile.GetFileFromPathAsync(_picture.GetPath());

                // compress to jpeg: 0 (no compression) -> 3 (high)
                if (Compression == SynchroManager.ComprLevel.Medium)
                    _file = await Picture.CompressAndSaveFileAsync(_file.Path, ApplicationData.Current.LocalFolder.Path, _file.Name, 1920, false);
                else if (Compression == SynchroManager.ComprLevel.High)
                    _file = await Picture.CompressAndSaveFileAsync(_file.Path, ApplicationData.Current.LocalFolder.Path, _file.Name, 1024, false);

                StorageFile _fileCopy = await _file.CopyAsync(_folderAlbum, _file.Name, NameCollisionOption.ReplaceExisting);

                if ((_fileCopy != null) && (Compression == SynchroManager.ComprLevel.Medium) || (Compression == SynchroManager.ComprLevel.High))
                    await _file.DeleteAsync();

            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }

            return true;
        }
    }
}
