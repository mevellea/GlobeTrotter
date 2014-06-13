using System;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Windows.Storage;
using Bing.Maps;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Text;

namespace GlobeTrotter
{
    public class Album
    {
        private static int ALBUM_PICTURES_MAX = 200;

        public String Id;
        public String PathRoot;
        public String PathAlbum;
        public String DisplayName;
        public String Hash;

        public Boolean PositionPresent;
        public DateTime DateArrival;
        public DateTime DateDeparture;
        public List<Picture> Pictures;
        public PlaceInfos AlbumInfos;
        public AlbumSummary Summary;
        public Boolean GpsLocationCommon;

        GpsRect _rect;
        Boolean _requestLocationUpdate;
        Boolean _requestThumbnailUpdate;

        public Album(int _id, String _path, String _root)
        {
            Pictures = new List<Picture>();
            AlbumInfos = new PlaceInfos();
            Summary = new AlbumSummary();
            Position = new GpsLocation();

            Id = _id.ToString();
            PathAlbum = _path.Replace(_root + "\\", "");
            PathRoot = _root;

            // download at first import
            _requestLocationUpdate = true;
            _requestThumbnailUpdate = true;
        }

        public String Country
        {
            get { return AlbumInfos.Country; }
        }

        public MarkerPosition MarkerAlbum;

        public GpsLocation Position;

        public GpsRect Rect
        {
            get { return _rect; }
            set { _rect = value; }
        }

        public async void GetStar()
        { 
            // extract star block from .picasa.ini file
            try
            {
                String _starFile = PathRoot + "\\" + PathAlbum + "\\.picasa.ini";
                StorageFile _picasIni = await StorageFile.GetFileFromPathAsync(_starFile);
                IRandomAccessStream sourceStream = await _picasIni.OpenAsync(FileAccessMode.Read);
                ITextDocument _doc = null;
                _doc.LoadFromStream(TextSetOptions.None, sourceStream);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        public async Task<Boolean> CreateThumbnails(String _pathTumb, Trip _parent)
        {
            // create thumbnails if they don't exist only or if requested
            if (_requestThumbnailUpdate || (Summary.PicturesThumb.Count == 0) || 
                ((Summary.PicturesThumb.Count < 4) && Pictures.Count >= 4))
            {
                List<Picture> _pictureRandList = new List<Picture>();

                foreach (Picture _picture in Pictures)
                    _pictureRandList.Add(_picture);

                int _compteur = Math.Min(4, _pictureRandList.Count);

                for (int _idx = 0; _idx < _compteur; _idx++)
                {
                    String nameRand = _parent.RandomNum(0).ToString();
                    int _count = _parent.RandomNum(_pictureRandList.Count);
                    Summary.PicturesThumb.Add(nameRand + ".jpg");

                    uint _size = 128;
                    if (_compteur < 4)
                        _size = 256;

                    await Picture.CompressAndSaveFileAsync(
                       _pictureRandList[_count].GetPath(),
                       ApplicationData.Current.LocalFolder.Path + "\\" + _pathTumb, nameRand + ".jpg", _size, true);

                    if (_idx == 0)
                        await Picture.CompressAndSaveFileAsync(
                            _pictureRandList[_count].GetPath(),
                            ApplicationData.Current.LocalFolder.Path + "\\" + _pathTumb, "Small_" + nameRand + ".jpg", _size, true);

                    _pictureRandList.RemoveAt(_count);
                }

                Summary.PathThumb = _pathTumb;
                _requestThumbnailUpdate = false;
                return true;
            }
            else
                return false;
        }

        public void RequestLocationUpdate()
        {
            _requestLocationUpdate = true;
        }

        public async Task<Boolean> UpdatePosition()
        {
            int _connectionFailedStatus = 0;
            int CONNECTION_FAIL_MAX = 3;
            Boolean _first = true;

            DateArrival = Pictures.First<Picture>().Date;
            DateDeparture = Pictures.Last<Picture>().Date;
            
            // download only if requested before
            foreach (Picture _picture in Pictures)
            {
                // stop try downloading if 3 fails before
                if ((_picture.PositionPresent) && (_requestLocationUpdate) && (_connectionFailedStatus < CONNECTION_FAIL_MAX))
                {
                    // update album position from first valid picture only
                    if (_first)
                    {
                        Position = new GpsLocation(_picture.Position);
                        _first = false;
                    }

                    if (await _picture.Download())
                    {
                        if (_picture.PictureInfos.InfoPresent)
                        {
                            if (_picture.PictureInfos.City != null)
                                DisplayName = _picture.PictureInfos.City + ", " + _picture.PictureInfos.Country;
                            else if (_picture.PictureInfos.Region != null)
                                DisplayName = _picture.PictureInfos.Region + ", " + _picture.PictureInfos.Country;
                            else if (_picture.PictureInfos.Establishment != null)
                                DisplayName = _picture.PictureInfos.Establishment;
                            else
                                DisplayName = _picture.PictureInfos.Country;

                            AlbumInfos = _picture.PictureInfos;
                            _requestLocationUpdate = false;
                        }
                        else
                            _connectionFailedStatus++;
                    }
                    else
                        _connectionFailedStatus++;
                }
            }
            if ((DisplayName == null) && (!AlbumInfos.InfoPresent))
                DisplayName = PathAlbum.Remove(0, PathAlbum.LastIndexOf("\\") + 1);

            Summary.Id = Id;
            Summary.Name = DisplayName;

            if (AlbumInfos.InfoPresent)
            {
                Summary.StrLocationShort = AlbumInfos.Region;

                if ((AlbumInfos.City == null) && (AlbumInfos.Region != null))
                {
                    Summary.StrLocationShort = AlbumInfos.Region;
                    Summary.StrLocation = AlbumInfos.Region;
                }
                else if ((AlbumInfos.City != null) && (AlbumInfos.Region == null))
                {
                    Summary.StrLocationShort = AlbumInfos.City;
                    Summary.StrLocation = AlbumInfos.City;
                }
                else if ((AlbumInfos.City != null) && (AlbumInfos.Region != null))
                {
                    Summary.StrLocationShort = AlbumInfos.City;
                    Summary.StrLocation = AlbumInfos.City + ", " + AlbumInfos.Region;
                }
                else
                {
                    Summary.StrLocationShort = DisplayName;
                    Summary.StrLocation = DisplayName;
                }
            }
            else
            {
                Summary.StrLocationShort = DisplayName;
                Summary.StrLocation = DisplayName;
            }

            return true;
        }

        public void CleanOverNumber()
        {
            DateTime _saveNow = DateTime.Now;
            Random _random = new Random((int)_saveNow.Ticks);

            if (Pictures.Count > ALBUM_PICTURES_MAX)
            {
                int _max = Pictures.Count;
                double _diff = _max - ALBUM_PICTURES_MAX;
                do
                {
                    Pictures.RemoveAt(_random.Next(_max--));
                    _diff--;
                } while (_diff > 0);
            }
        }

        public async Task<Boolean> ReorganizeAlbums(String _rootPathTrip)
        {
            String _newNameFile = DateFormat.DateDisplayLocalization(DateArrival, DateFormat.EMode.Day);
            String _newDisplayName = DateFormat.DateDisplayLocalization(DateArrival, DateFormat.EMode.DayDigit);
            String _specifier = "D3";

            if (AlbumInfos.InfoPresent)
            {
                _newNameFile += " - " + Summary.StrLocationShort;
                _newDisplayName += " - " + Summary.StrLocationShort;
            }

            int _index = 1;

            foreach (Picture _picture in Pictures)
            {
                String _newNameComplete = _newNameFile + " - " + _index.ToString(_specifier) + _picture.Extension;
                // check if same root path name, abum folder name, and file name
                if ((_rootPathTrip != PathRoot) || (DisplayName != _newDisplayName) ||
                    ((_picture.Name + _picture.Extension != _newNameComplete)))
                    await _picture.RenameAndMoveAsync(_rootPathTrip, _newNameFile, _newDisplayName, _index);
                _index++;
            }

            return true;
        }

        public void ExtractRect()
        {
            Boolean _first = true;

            foreach (Picture _pic in Pictures)
            {
                if (_pic.PositionPresent)
                {
                    PositionPresent = true;
                    if (_first)
                    {
                        _rect = new GpsRect(_pic.Position);
                        _first = false;
                    }
                    _rect.UpdateRangeFromLocation(_pic.Position);
                }
            }

            if (_rect == null)
                _rect = new GpsRect();

            // distance with edges of original rect
            double _degreesOffset = 0.2;

            _rect.UpdateDisp(_degreesOffset);
            GpsLocationCommon = _rect.TopLeft.Equals(_rect.BottomRight);
        }

        public Album()
        { }

        internal void Activate()
        {
            if (PositionPresent)
                MarkerAlbum.Activate();
        }

        internal void DeActivate()
        {
            if (PositionPresent)
                MarkerAlbum.DeActivate();
        }

        internal async void CreateMarker(ViewMapTrip mapPage, MapUIElementCollection _element)
        {
            if (MarkerAlbum != null)
                MarkerAlbum.Delete();

            StorageFile _file;

            try
            {
                if (Summary.Sample)
                    _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///appdata/" + Summary.PathThumb + "/" + Summary.PicturesThumb[0]));
                else
                {
                    StorageFolder folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(Summary.PathThumb);
                    _file = await folder.GetFileAsync("Small_" + Summary.PicturesThumb[0]);
                }

                Uri uri = new Uri(_file.Path, UriKind.RelativeOrAbsolute);
                BitmapImage bm = new BitmapImage() { UriSource = uri };
                MarkerAlbum = new MarkerPosition(mapPage, _element, Id, bm, Position, true, 20);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return;
            }
        }

        internal void RemoveMarker()
        {
            if (MarkerAlbum != null)
            {
                MarkerAlbum.Delete();
            }
        }

        internal void RemoveMarkerPictures()
        {
            if (MarkerAlbum != null)
            {
                foreach (Picture _picture in Pictures)
                    _picture.RemoveMarker();
            }
        }

        internal async Task ForcePosition(GpsLocation _location)
        {
            await Pictures[0].UpdateMetadata(_location);
            Position = new GpsLocation(_location);
            RequestLocationUpdate();
        }

        internal Boolean Merge(Album _albumNext, Boolean _force)
        {
            // merge if same location and time difference less than 5 days
            TimeSpan _offset = new TimeSpan(5, 0, 0, 0, 0);

            if (_force || ((_albumNext.DisplayName == DisplayName) && (DateArrival - _albumNext.DateArrival < _offset)))
            {
                foreach (Picture _picture in _albumNext.Pictures)
                    Pictures.Add(_picture);
  
                _requestThumbnailUpdate = true;
                return true;
            }
            else
                return false;
        }

        internal void RenameTopFolder(String _pathRoot, String _pathRootPrevious)
        {
            PathRoot = _pathRoot;
            foreach (Picture _picture in Pictures)
                _picture.RenameTopFolder(_pathRoot, _pathRootPrevious);
        }
    }
}
