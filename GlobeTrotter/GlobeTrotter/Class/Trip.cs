using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.Storage.FileProperties;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Bing.Maps;
using Bing.Maps.Directions;
using Windows.ApplicationModel.Resources;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using GlobeTrotter;

namespace GlobeTrotter
{
    [DataContract]
    public class Trip
    {
        public delegate void Callback();

        // global variables
        public String Id;
        public String PathRoot;
        public String DisplayName;

        public List<Album> Albums;
        public List<Itinerary> Itineraries;
        public TripSummary Summary;
        public Boolean PositionPresent;
        public Boolean Sample;
        public Boolean Reorganize;
        public GpsRect Rect;
        public String Hash;

        // merge variables
        Album _albumMergeForceSrc;
        Album _albumMergeForceDest;

        Random _random;
        ResourceLoader _res;
        Page _parent;

        public Trip()
        {
            Id = RandomNum(0).ToString();
            Albums = new List<Album>();
            Itineraries = new List<Itinerary>();
            Summary = new TripSummary();

            _res = ResourceLoader.GetForCurrentView();
        }

        public async void Import(StorageFolder _rootFolder, Boolean _reorganize, Page _parentLoc, 
            Trip.Callback _callbackEnd)
        {
            _parent = _parentLoc;
            Reorganize = _reorganize;

            ProgressUpdate(_res.GetString("SearchPictures"), 0);

            // create picture list
            List<Picture> _picList = await CreatePicList(_rootFolder);

            if (_picList.Count == 0)
            {
                ProgressFinished(_res.GetString("NoPicture"));
                return;
            }

            ProgressUpdate(_res.GetString("ImportStart"), 0);

            // update picture list locations
            _picList = await UpdatePicListLocation(_picList);

            // create albums
            Albums = CreateAlbums(_picList);

            // update albums data
            await Update(true, true, null, null, _callbackEnd, _parentLoc);
        }

        public void RequestMerge(Album _albumDest, Album _albumSrc)
        {
            _albumMergeForceDest = _albumDest;
            _albumMergeForceSrc = _albumSrc;
        }

        public async Task Update(Boolean _reorganizeAlt, Boolean _downloadAlt, Trip.Callback _callbackPosition, Trip.Callback _callbackItinerary, 
            Trip.Callback _callbackEnd, Page _parentLoc)
        {
            int _count = 1;
            _parent = _parentLoc;
            try
            {
                if (_downloadAlt)
                {
                    // set albums positions and download
                    foreach (Album _album in Albums)
                    {
                        await _album.UpdatePosition();
                        ProgressUpdate(_res.GetString("DownloadPosition") + " " + _count + " " + _res.GetString("Of") + " " + Albums.Count + "...",
                            100 * _count / Albums.Count);
                        _count++;
                    }

                    if (_callbackPosition != null)
                        _callbackPosition();
                }

                // merge albums
                for (int _idx = Albums.Count - 2; _idx >= 0; _idx--)
                    if (Albums[_idx].Merge(Albums[_idx + 1], false))
                        Albums.RemoveAt(_idx + 1);

                // merge force
                if ((_albumMergeForceDest != null) && (_albumMergeForceSrc != null) && (_albumMergeForceDest.Merge(_albumMergeForceSrc, true)))
                {
                    foreach (Album _albumDelete in Albums)
                    {
                        if (_albumDelete.Id == _albumMergeForceSrc.Id)
                        {
                            Albums.Remove(_albumDelete);
                            _albumMergeForceDest = null;
                            _albumMergeForceSrc = null;
                            break;
                        }
                    }
                }

                if (_downloadAlt)
                {
                    // create itineraries
                    ItinerariesCreate();

                    // create itineraries
                    await ItinerariesDownload();

                    if (_callbackItinerary != null)
                        _callbackItinerary();
                }

                // reorganize folders
                _count = 1;
                if (Reorganize && _reorganizeAlt)
                {
                    foreach (Album _album in Albums)
                    {
                        await _album.ReorganizeAlbums(GetPath());
                        ProgressUpdate(_res.GetString("Reorganize") + " " + _count + " " + _res.GetString("Of") + " " + Albums.Count + "...", 
                            100 * _count / Albums.Count);
                        _count++;
                    }
                }

                if (_downloadAlt)
                {
                    // update trip infos
                    UpdateData();
                }
           
                // rename top folders and sub-paths
                if (Reorganize && _reorganizeAlt)
                { 
                    if (Summary.SyncDropbox.InProgress)
                        Summary.SyncDropbox.PreviousName = GetPath();
                    await RenameTopFolder();
                }

                // CreateThumbnails
                await CreateThumbnails();

                // Force resynchronize
                Summary.SyncDropbox.Finished = false;

                // save to file
                await Serialization.SerializeToXmlFile<Trip>(Id + ".trip", this);

                ProgressFinished("");

                if (_callbackEnd != null)
                    _callbackEnd();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private async Task Archive()
        {
            try
            {
                StorageFolder _folderTop = await StorageFolder.GetFolderFromPathAsync(GetPath());
                StorageFolder _folderGT = await _folderTop.CreateFolderAsync(".GlobeTrotter", CreationCollisionOption.OpenIfExists);
                // add hidden property
                StorageFolder _tripNow = await _folderGT.CreateFolderAsync(DateTime.Now.Ticks.ToString(), CreationCollisionOption.FailIfExists);
                StorageFolder _folderTripNow = await _tripNow.CreateFolderAsync(Summary.PathThumb, CreationCollisionOption.FailIfExists);

                StorageFolder _folderTrip = await ApplicationData.Current.LocalFolder.GetFolderAsync(Summary.PathThumb);
                StorageFile _fileTrip = await ApplicationData.Current.LocalFolder.GetFileAsync(Summary.PathThumb + ".trip");
                await _fileTrip.CopyAsync(_tripNow, _fileTrip.Name, NameCollisionOption.FailIfExists);

                IReadOnlyList<StorageFile> _items = await _folderTrip.GetFilesAsync();
                foreach (StorageFile _pic in _items)
                    if (_pic.FileType.Equals(".jpg"))
                        await _pic.CopyAsync(_folderTripNow, _pic.Name, NameCollisionOption.FailIfExists);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        public String GetPath()
        {
            return PathRoot + "\\" + DisplayName;
        }

        private void ProgressUpdate(String _text, int _percent)
        {
            if (_parent != null)
            {
                if (_parent is ViewHome)
                    (_parent as ViewHome).ProgressUpdate(_text, _percent);
                else if (_parent is ViewConf)
                    (_parent as ViewConf).ProgressUpdate(_text, _percent);
            }
        }

        private void ProgressFinished(String _text)
        {
            if (_parent != null)
            {
                if (_parent is ViewHome)
                {
                    (_parent as ViewHome).ProgressFinished(_text);
                    (_parent as ViewHome).StopImport();
                }
                else if (_parent is ViewConf)
                {
                    (_parent as ViewConf).ProgressFinished(_text);
                    (_parent as ViewConf).StopImport();
                }
            }
        }

        private async Task RenameTopFolder()
        {
            StorageFolder _topFolder = await StorageFolder.GetFolderFromPathAsync(GetPath());

            // check first if main location has been found before
            if ((Summary.LocationMain != null) && (Summary.LocationMain != ""))
            {
                //folder does not exist already
                if ((_topFolder.DisplayName != Summary.FolderTopName) && !(await Picture.DoesFolderExistAsync(PathRoot + "\\" + Summary.FolderTopName)))
                {
                    foreach (Album _album in Albums)
                        _album.RenameTopFolder(PathRoot + "\\" + Summary.FolderTopName, GetPath());
                    await _topFolder.RenameAsync(Summary.FolderTopName, NameCollisionOption.FailIfExists);
                    DisplayName = Summary.FolderTopName;
                }
            }
        }

        public async Task<List<Picture>> CreatePicList(StorageFolder _rootfolder)
        {
            List<Picture> _picList = new List<Picture>();
            List<StorageFile> _fileListDoublon = new List<StorageFile>();

            StorageFolder _rootFolderParent = await _rootfolder.GetParentAsync();

            if (_rootFolderParent == null)
                return _picList;

            DisplayName = _rootfolder.DisplayName;
            PathRoot = _rootFolderParent.Path;

            QueryOptions _fileQueryOptions = new QueryOptions();
            _fileQueryOptions.FolderDepth = FolderDepth.Deep;

            StorageFolder _folderOut = ApplicationData.Current.LocalFolder;
            await _folderOut.CreateFolderAsync(this.Id);

            // list all jpg files in selected sub-folders
            StorageFileQueryResult _queryFile = _rootfolder.CreateFileQueryWithOptions(_fileQueryOptions);
            IReadOnlyList<StorageFile> _filesReadOnly = await _queryFile.GetFilesAsync();

            // remove picture if already exist with same name in list

            foreach (StorageFile _file in _filesReadOnly)
            {
                Boolean _valid = false, _lost = false, _found = false;

                // step 1: check extension
                foreach (string _ext in Picture.Extensions)
                {
                    if (_file.FileType.ToLower().Equals(_ext))
                    {
                        _valid = true;
                        break;
                    }
                }
                                    
                if (_valid)
                {
                    StorageFolder _folderParent = await _file.GetParentAsync();

                    if (_folderParent == null)
                        return _picList;

                    // step 2: check if not in lost+found or [%name%]
                    if ((_folderParent.DisplayName.Contains("lost+found")) || 
                        (_folderParent.DisplayName.Contains("[") && _folderParent.DisplayName.Contains("[")))
                        _lost = true;

                    if (!_lost)
                    {
                        // step 3: check if already found
                        foreach (Picture _pic in _picList)
                        {
                            if (_pic.Name == _file.DisplayName)
                            {
                                _found = true;
                                break;
                            }
                        }

                        if (_found)
                            _fileListDoublon.Add(_file);
                        else
                            _picList.Add(new Picture(_rootFolderParent, _folderParent, _file));
                    }
                }
                ProgressUpdate(_picList.Count + " " + _res.GetString("ImagesFound"), 0);
            }
                
            if (_fileListDoublon.Count > 0)
            {
                try
                {
                    StorageFolder _root = await StorageFolder.GetFolderFromPathAsync(_rootfolder.Path);
                    StorageFolder _folderDoublons = await _root.CreateFolderAsync("lost+found", CreationCollisionOption.OpenIfExists);
                    foreach (StorageFile _file in _fileListDoublon)
                        await _file.MoveAsync(_folderDoublons, _file.Name, NameCollisionOption.GenerateUniqueName);
                }
                catch (FileNotFoundException)
                {
                    Debug.WriteLine("File not found");
                    return _picList;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    return _picList;
                }
            }

            return _picList;
        }

        public async Task<List<Picture>> UpdatePicListLocation(List<Picture> _picList)
        {
            int _count = 1;
            foreach (Picture _picture in _picList)
            {
                await _picture.GetMetadata();
                ProgressUpdate(_res.GetString("ImportPicture") + " " + _count + " " + _res.GetString("Of") + " " + _picList.Count,
                    100 * _count / _picList.Count);
                _count++;
            }

            return _picList = _picList.OrderBy<Picture, DateTime>(o => o.Date).ToList<Picture>();
        }

        public List<Album> CreateAlbums(List<Picture> _picList)
        {
            String _specifier = "D2";

            List<Album> _albums = new List<Album>();

            Album _albumImport = new Album(RandomNum(0), _picList[0].PathFolder, GetPath());
            int _day = _picList[0].Date.DayOfYear;
            _albumImport.DisplayName = _picList[0].Date.Day.ToString(_specifier) + "-" + _picList[0].Date.Month.ToString(_specifier) + "-" + _picList[0].Date.Year.ToString();

            // create albums by date
            foreach (Picture _picture in _picList)
            {
                if (_day != _picture.Date.DayOfYear)
                {
                    _albums.Add(_albumImport);
                    _albumImport = new Album(RandomNum(0), _picture.PathFolder, GetPath());
                    _day = _picture.Date.DayOfYear;
                    _albumImport.DisplayName = _picture.Date.Day.ToString(_specifier) + "-" + _picture.Date.Month.ToString(_specifier) + "-" + _picture.Date.Year.ToString();
                }
                _albumImport.Pictures.Add(_picture);
            }
            _albums.Add(_albumImport);

            return _albums.OrderBy<Album, DateTime>(o => o.DateArrival).ToList<Album>();
        }

        public void UpdateData()
        {
            Summary.Id = Id;
            Summary.PathThumb = Id;
            Summary.DateArrival = Albums.First<Album>().DateArrival;
            Summary.DateDeparture = Albums.Last<Album>().DateDeparture;

            List<String> _countriesList = GetCountriesList();
            if (_countriesList.Count > 1)
            {
                List<String> MainCountries = GetMainCountries(_countriesList);
                if ((MainCountries[0].Length + MainCountries[1].Length) < 20)
                    Summary.LocationMain = MainCountries[0] + ", " + MainCountries[1];
                else
                    Summary.LocationMain = MainCountries[0];

                Summary.FolderTopName = DateFormat.DateDisplayLocalization(Summary.DateArrival, DateFormat.EMode.Month) + " - " + Summary.LocationMain;
            }
            else if (_countriesList.Count == 1)
            {
                Summary.LocationMain = _countriesList[0];
                Summary.FolderTopName = DateFormat.DateDisplayLocalization(Summary.DateArrival, DateFormat.EMode.Month) + " - " + Summary.LocationMain;
            }
            else
            {
                //no country found or no data received
                if (DisplayName.Length > 19)
                {
                    Summary.LocationMain = DisplayName.Remove(19);
                    Summary.LocationMain += "...";
                }
                else
                    Summary.LocationMain = DisplayName;
                Summary.FolderTopName = DisplayName;
            }

            List<String> _cityList = GetCityList();
            if (_cityList.Count > 1)
                Summary.LocationFromTo = _cityList.First<String>() + " to " + _cityList.Last<String>();
            else if (_cityList.Count == 1)
                Summary.LocationFromTo = _cityList.First<String>();
            else
                Summary.LocationFromTo = "";

            // update rect of albums and trip
            UpdateRect();
        }

        public void ItinerariesCreate()
        {
            for (int _idx = Itineraries.Count; _idx < Albums.Count - 1; _idx++)
                Itineraries.Add(new Itinerary());

            for (int _idx = 0; _idx < Albums.Count - 1; _idx++)
            {
                Itineraries[_idx].UpdatePoints(Albums[_idx].Position, 0);
                Itineraries[_idx].UpdatePoints(Albums[_idx + 1].Position, 1);
            }
        }

        public async Task<Boolean> ItinerariesDownload()
        {
            Boolean _updated = false;
            int _count = 1;

            foreach (Itinerary _itinerary in Itineraries)
            {
                if (_itinerary.RequestDownload && (await _itinerary.Download()))
                {
                    _updated = true;
                    Summary.Distance += _itinerary.Distance;
                }

                ProgressUpdate(_res.GetString("DownloadingItinerary") + " " + _count + " " + _res.GetString("Of") + " " + Itineraries.Count,
                    100 * _count / Itineraries.Count);
                _count++;
            }
            return _updated;
        }

        public void ItinerariesUpdatePoints()
        {
            for (int _idx=0; _idx<Itineraries.Count; _idx++)
            {
                Itineraries[_idx].UpdatePoints(Albums[_idx].Position, 0);
                Itineraries[_idx].UpdatePoints(Albums[_idx + 1].Position, 1);
            }
        }

        private async Task CreateThumbnails()
        {
            Boolean _requestThumbnailUpdate = false;
            int _count = 1;

            foreach (Album _album in Albums)
            {
                if (await _album.CreateThumbnails(Id, this))
                    _requestThumbnailUpdate = true;

                ProgressUpdate(_res.GetString("CreatingThumb") + " " + _count + " " + _res.GetString("Of") + " " + Albums.Count + "...", 
                    100 * _count / Albums.Count);
                _count++;
            }

            if (_requestThumbnailUpdate)
            {
                List<String> _pictureThumbList = new List<string>();
                foreach (Album _album in Albums)
                    foreach (String _path in _album.Summary.PicturesThumb)
                        _pictureThumbList.Add(_path);

                Summary.PicturesThumb = TripSummary.GetRandomList(_pictureThumbList);
            }
        }

        public List<String> GetCountriesList()
        {
            List<String> _countries = new List<String>();
            Boolean foundPrevious;

            foreach (Album _album in Albums)
            {
                if ((_album.Country != "") && (_album.Country != null))
                {
                    foundPrevious = false;
                    String countryTemp = _album.Country;
                    foreach (String previousCountries in _countries)
                        if (countryTemp == previousCountries)
                            foundPrevious = true;
                    if (!foundPrevious)
                        _countries.Add(countryTemp);
                }
            }
            return _countries;
        }

        private List<String> GetRegionList()
        {
            List<String> _regions = new List<String>();
            Boolean foundPrevious;

            foreach (Album album in Albums)
            {
                if ((album.AlbumInfos.Region != "") && (album.AlbumInfos.Region != null))
                {
                    foundPrevious = false;
                    String _regionTemp = album.AlbumInfos.Region;
                    foreach (String _prevRegions in _regions)
                        if (_regionTemp == _prevRegions)
                            foundPrevious = true;
                    if (!foundPrevious)
                        _regions.Add(_regionTemp);
                }
            }
            return _regions;
        }

        private List<String> GetCityList()
        {
            List<String> _cities = new List<String>();
            Boolean foundPrevious;

            foreach (Album album in Albums)
            {
                if ((album.AlbumInfos.City != "") && (album.AlbumInfos.City != null))
                {
                    foundPrevious = false;
                    String _cityTemp = album.AlbumInfos.City;
                    foreach (String _prevCity in _cities)
                        if (_cityTemp == _prevCity)
                            foundPrevious = true;
                    if (!foundPrevious)
                        _cities.Add(_cityTemp);
                }
            }
            return _cities;
        }

        private List<String> GetMainCountries(List<String> _countriesList)
        {
            List<String> retCountry = new List<String>();

            if (_countriesList.Count != 0)
            {
                int[] countCountries = new int[_countriesList.Count];

                for (int idxCountries = 0; idxCountries < _countriesList.Count; idxCountries++)
                    for (int idxAlbum = 0; idxAlbum < Albums.Count; idxAlbum++)
                        if (Albums[idxAlbum].Country == _countriesList[idxCountries])
                            countCountries[idxCountries]++;

                int CountMax = 0;
                retCountry.Add("");
                retCountry.Add("");

                for (int idxCountries = 0; idxCountries < _countriesList.Count; idxCountries++)
                {
                    if (countCountries[idxCountries] > CountMax)
                    {
                        CountMax = countCountries[idxCountries];
                        retCountry[1] = retCountry[0];
                        retCountry[0] = _countriesList[idxCountries];
                    }
                    else
                        if (retCountry[1].Equals(""))
                            retCountry[1] = _countriesList[idxCountries];
                }
            }
            return retCountry;
        }

        public void DisplayItineraries(MapShapeLayer _routeLayer, Boolean _lowPerfo)
        {
            RemoveItineraries(_routeLayer);
            foreach (Itinerary _itinerary in Itineraries)
                _itinerary.Display(_routeLayer, _lowPerfo);
        }

        public void RemoveItineraries(MapShapeLayer _routeLayer)
        {
            foreach (Itinerary _itinerary in Itineraries)
                _itinerary.Remove(_routeLayer);
        }

        public void DisplayMarkers(ViewMapTrip _view, MapUIElementCollection _element)
        {
            foreach (Album _album in Albums)
            {
                if (_album.PositionPresent)
                {
                    _album.RemoveMarkerPictures();
                    _album.CreateMarker(_view, _element);
                }
            }
        }

        public void RemoveMarkers()
        {
            foreach (Album _album in Albums)
            {
                if (_album.PositionPresent)
                {
                    _album.RemoveMarkerPictures();
                    _album.MarkerAlbum.Delete();
                }
            }
        }

        public void UpdateRect()
        {
            foreach (Album _album in Albums)
                _album.ExtractRect();

            Boolean _first = true;
            PositionPresent = false;

            foreach (Album _album in Albums)
            {
                if (_album.PositionPresent)
                {
                    PositionPresent = true;
                    if (_first)
                    {
                        Rect = new GpsRect(_album.Rect);
                        _first = false;
                    }
                    Rect.UpdateRangeFromRect(_album.Rect);
                }
            }

            double _degreesOffset = 1.5;

            if (Rect == null)
                Rect = new GpsRect();

            Rect.UpdateDisp(_degreesOffset);
        }

        public void Activate()
        {
            foreach (Album album in Albums)
                album.Activate();
        }

        public void DeActivate()
        {
            foreach (Album album in Albums)
                album.DeActivate();
        }

        public int RandomNum(int _max)
        {
            if (_random == null)
            {
                DateTime _saveNow = DateTime.Now;
                _random = new Random((int)_saveNow.Ticks);
            }
            if (_max == 0)
                return _random.Next();
            else
                return _random.Next(0, _max);
        }

        public void Merge(Trip _tripSrc, String _pathDest)
        {
            for (int _idxSrc = 0; _idxSrc < _tripSrc.Albums.Count; _idxSrc++)
            {
                _tripSrc.Albums[_idxSrc].Summary.PathThumb = _pathDest;
                Albums.Add(_tripSrc.Albums[_idxSrc]);
            }
            Albums = Albums.OrderBy<Album, DateTime>(o => o.DateArrival).ToList<Album>();

            foreach (String _country in _tripSrc.Summary.Countries)
                if (!Summary.Countries.Contains(_country))
                    Summary.Countries.Add(_country);

            Summary.Distance += _tripSrc.Summary.Distance;
        }
                
        public static async Task<Trip> Load(TripDescUserControl _tripDesc)
        {
            return await LoadFromSummary(_tripDesc.TripSummary);
        }
                
        public static async Task<Trip> LoadFromSummary(TripSummary _tripSummary)
        {
            StorageFile _file;
            if (_tripSummary.Sample)
                _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/" + _tripSummary.PathThumb + ".trip"));
            else
                _file = await ApplicationData.Current.LocalFolder.GetFileAsync(_tripSummary.Id + ".trip");

            Trip _trip = Serialization.DeserializeFromXmlFile<Trip>(_file.Path);

            if (_trip.Summary.DataCoherent())
                return _trip;
            else
#if DEBUG
                throw new FormatException();
#else
                return new Trip();
#endif
        }

        public static async Task<Boolean> DeleteFiles(String _tripName, List<TripSummary> _listSummary, Boolean _save)
        {
            if (_listSummary == null)
                _listSummary = await TripSummary.Load();

            try
            {
                foreach (TripSummary _summary in _listSummary)
                {
                    if (_summary.Id.Equals(_tripName))
                    {
                        _listSummary.Remove(_summary);
                        break;
                    }
                }

                if (_save)
                    await TripSummary.Save(_listSummary);

                StorageFile fileDelete = await ApplicationData.Current.LocalFolder.GetFileAsync(_tripName + ".trip");
                await fileDelete.DeleteAsync(StorageDeleteOption.PermanentDelete);
                StorageFolder folderDelete = await ApplicationData.Current.LocalFolder.GetFolderAsync(_tripName);
                await folderDelete.DeleteAsync(StorageDeleteOption.PermanentDelete);
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }
        }
    }
}