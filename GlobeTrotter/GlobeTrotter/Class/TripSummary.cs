using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace GlobeTrotter
{
    public class TripSummary
    {
        public String Id;
        public String LocationMain;
        public String FolderTopName;
        public String LocationFromTo;
        public DateTime DateArrival;
        public DateTime DateDeparture;
        public uint Distance;
        public String PathThumb;
        public List<String> PicturesThumb;
        public Boolean Sample;
        public int SampleId;
        public List<String> Countries;
        public String Hash;

        public SynchroHandle SyncGooglePlus;
        public SynchroHandle SyncFacebook;
        public SynchroHandle SyncDropbox;
        public SynchroHandle SyncUsb;

        public TripSummary()
        {
            PicturesThumb = new List<String>();
            Countries = new List<string>();
            Distance = 0;

            SyncGooglePlus = new SynchroHandle(SynchroManager.ServerName.GooglePlus);
            SyncFacebook = new SynchroHandle(SynchroManager.ServerName.Facebook);
            SyncDropbox = new SynchroHandle(SynchroManager.ServerName.Dropbox);
            SyncUsb = new SynchroHandle(SynchroManager.ServerName.Usb);
        }

        public void AddCountry(String _country)
        {
            if (!Countries.Contains(_country))
                Countries.Add(_country.ToUpper());
        }

        public void RemoveCountry(String _country)
        {
            if (Countries.Contains(_country))
                Countries.Remove(_country);
        }

        public static async Task<Boolean> Save(List<TripSummary> _listSummaryLocal)
        {
            List<TripSummary> _listSummarySave = new List<TripSummary>();
            foreach (TripSummary _summary in _listSummaryLocal)
                if (!_summary.Sample)
                    _listSummarySave.Add(_summary);

            return (await Serialization.SerializeToXmlFile<List<TripSummary>>("TripList.desc", _listSummarySave));
        }

        public static async Task<List<TripSummary>> Load()
        {
            try
            {
                StorageFile _file = await ApplicationData.Current.LocalFolder.GetFileAsync("TripList.desc");
                List<TripSummary> _listTemp = Serialization.DeserializeFromXmlFile<List<TripSummary>>(_file.Path);
                if (_listTemp == null)
                    _listTemp = new List<TripSummary>();

                for (int _idx = _listTemp.Count - 1; _idx >= 0; _idx--)
                    if (!_listTemp[_idx].DataCoherent())
                        _listTemp.RemoveAt(_idx);

                return _listTemp;
            }
            catch
            {
                return new List<TripSummary>();
            }
        }

        public static List<String> GetRandomList(List<String> _listIn)
        {
            DateTime _saveNow = DateTime.Now;
            Random _random = new Random((int)_saveNow.Ticks);

            // Creation of albums finished, get 4 random thumbnails from albums
            List<String> _listOut = new List<string>();

            int _compteur = Math.Min(4, _listIn.Count);
            int _count;

            for (int _idx = 0; _idx < _compteur; _idx++)
            {
                _count = _random.Next(_listIn.Count);
                _listOut.Add(_listIn[_count]);
                _listIn.RemoveAt(_count);
            }
            return _listOut;
        }

        public static async Task<Boolean> Merge(TripDescUserControl _tripDescSrc, TripDescUserControl _tripDescDest, Page _parent)
        {
            ResourceLoader _res = ResourceLoader.GetForCurrentView();

            if (_tripDescDest.TripSummary.Sample || _tripDescSrc.TripSummary.Sample)
            {
                Toast.DisplaySingleLine(_res.GetString("MergeSamples"));
                return false;
            }

            // no previous import date exists, ask and import everything
            Boolean _requestMerge = false;
            MessageDialog messageDialog;
            messageDialog = new MessageDialog(_res.GetString("MergeTrips"));
            messageDialog.Commands.Add(new UICommand(_res.GetString("No"), (command) => { }));
            messageDialog.Commands.Add(new UICommand(_res.GetString("Yes"), (command) => { _requestMerge = true; }));
            messageDialog.CancelCommandIndex = 0;
            messageDialog.DefaultCommandIndex = 1;
            await messageDialog.ShowAsync();

            if (!_requestMerge)
                return false;

            if (_parent is ViewHome)
                (_parent as ViewHome).ProgressUpdate(_res.GetString("Merging"), 0);

            Trip _tripSrc = await Trip.Load(_tripDescSrc);
            Trip _tripDest = await Trip.Load(_tripDescDest);

            try
            {
                StorageFolder _folderSrc = await ApplicationData.Current.LocalFolder.GetFolderAsync(_tripDescSrc.TripSummary.PathThumb);
                StorageFolder _folderDest = await ApplicationData.Current.LocalFolder.GetFolderAsync(_tripDescDest.TripSummary.PathThumb);

                var _items = await _folderSrc.GetItemsAsync();
                int _count = 0;
                foreach (object _file in _items)
                {
                    if (_file is StorageFile)
                        await (_file as StorageFile).MoveAsync(_folderDest, (_file as StorageFile).Name, NameCollisionOption.ReplaceExisting);

                    if (_parent is ViewHome)
                        (_parent as ViewHome).ProgressUpdate(_res.GetString("MovePicture") + " " + (_file as StorageFile).Name, 
                            100 * _count++ / _items.Count);
                }
            }

            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }

            List<String> _listPicNames = new List<string>();
            foreach (String _str in _tripSrc.Summary.PicturesThumb)
                _listPicNames.Add(_str);
            foreach (String _str in _tripDest.Summary.PicturesThumb)
                _listPicNames.Add(_str);

            _tripDest.Summary.PicturesThumb = GetRandomList(_listPicNames);

            _tripDest.Merge(_tripSrc, _tripDescDest.TripSummary.PathThumb);

            await _tripDest.Update(true, true, null, null, null, _parent);
            
            // try delete top folder if empty
            try
            {
                StorageFolder folderDelete = await StorageFolder.GetFolderFromPathAsync(_tripSrc.GetPath());
                IReadOnlyList<IStorageItem> _files = await folderDelete.GetItemsAsync();
                if (_files.Count == 0)
                    await folderDelete.DeleteAsync();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        public Boolean DataCoherent()
        {
            if ((Sample) ||
                (Id != null) && (Id is String) && (Id != "") &&
                (LocationMain != null) && (LocationMain is String) && (LocationMain != "") &&
                (DateArrival.Ticks != 0) && (DateDeparture.Ticks != 0) &&
                (PathThumb != null) && (PathThumb is String) && (PathThumb != "") &&
                (PicturesThumb != null) && (PicturesThumb is List<String>) && (PicturesThumb.Count > 0) &&
                (PicturesThumb[0] != null) && (PicturesThumb[0] != ""))
                return true;
            else
                return false;
        }

        public Boolean SyncRequested()
        {
            return SyncDropbox.Requested || SyncFacebook.Requested || SyncGooglePlus.Requested || SyncUsb.Requested;
        }

        public void ClearSyncRequest()
        {
            SyncDropbox.Finished = false;
            SyncFacebook.Finished = false;
            SyncGooglePlus.Finished = false;
            SyncUsb.Finished = false;
        }
    }
}
