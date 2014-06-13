using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.AccessCache;
using Windows.Foundation.Collections;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Bing.Maps;
using Bing.Maps.Search;
using System.Collections;
using GoogleService.Common.JSON;
using System.Text.RegularExpressions;
namespace GlobeTrotter
{
    public class Picture : IComparable
    {
        public String Name;
        public String PathFolder;
        public String Extension;

        public String PreviousName;
        public String PreviousPathFolder;

        public GpsLocation Position;
        public Boolean PositionPresent;
        public PlaceInfos PictureInfos;
        public DateTime Date;
        public MarkerPosition MarkerPicture;

        private StorageFile _fileHandler;

        public static String[] Extensions = 
        {
            ".jpg",
            ".bmp",
            ".png"
        };

        public Picture()
        {
        }

        public Picture(StorageFolder _rootFolder, StorageFolder _currentFolder, StorageFile _file)
        {
            PictureInfos = new PlaceInfos();
            _fileHandler = _file;
            PathFolder = _currentFolder.Path;

            Name = _file.DisplayName;
            Extension = _file.FileType.ToLower();
        }

        public async Task<Boolean> RenameAndMoveAsync(String _pathRoot, String _newNameFile, String _newNameFolder, int _index)
        {
            PreviousName = Name;
            PreviousPathFolder = PathFolder;

            StorageFile _fileInit;
            StorageFolder _folderDest, _folderParent;
            String _specifier = "D3";

            try
            {
                StorageFolder _folderInit = await StorageFolder.GetFolderFromPathAsync(PathFolder);

                // don't rename if folders are identical and files different but _renameFile not set
                if (_pathRoot + "\\" + _newNameFolder == PathFolder)
                    if (_newNameFile + " - " + _index.ToString(_specifier) == Name)
                        return false;

                _fileInit = await StorageFile.GetFileFromPathAsync(GetPath());
                _folderParent = await StorageFolder.GetFolderFromPathAsync(_pathRoot);
                _folderDest = await _folderParent.CreateFolderAsync(_newNameFolder, CreationCollisionOption.OpenIfExists);

                _newNameFile = await MoveUniqueNameAsync(_newNameFile, Extension, _fileInit, _folderDest, _index);

                if ((await _folderInit.GetItemsAsync()).Count == 0)
                    await _folderInit.DeleteAsync(StorageDeleteOption.Default);

                Name = _newNameFile;
                PathFolder = _pathRoot + "\\" + _newNameFolder;
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine("File not found");
                return false;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        public String GetPath()
        {
            return PathFolder + "\\" + Name + Extension;
        }

        public async Task<Boolean> UpdateMetadata(GpsLocation _position)
        {
            Position = _position;
            PositionPresent = true;

            GpsLocationDec _locationDec = new GpsLocationDec(_position);
            StorageFile _file;

            try
            {
                _file = await StorageFile.GetFileFromPathAsync(GetPath());
            }
            catch (FileNotFoundException)
            {
                PositionPresent = false;
                Position.Latitude = 0;
                Position.Longitude = 0;
                Date = _fileHandler.DateCreated.DateTime;
                return false;
            }

            ImageProperties m_imageProperties = await _file.Properties.GetImagePropertiesAsync();

            PropertySet propertiesToSave = new PropertySet();

            // The Latitude and Longitude properties are read-only. Instead,
            // write to System.GPS.LatitudeNumerator, LatitudeDenominator, etc.
            // These are length 3 arrays of integers. For simplicity, the
            // seconds data is rounded to the nearest 10000th.
            uint[] latitudeNumerator = 
            {
                (uint) _locationDec.Latitude.Deg,
                (uint) _locationDec.Latitude.Min,
                (uint) (_locationDec.Latitude.Sec * 10000)
            };

            uint[] longitudeNumerator = 
            {
                (uint) _locationDec.Longitude.Deg,
                (uint) _locationDec.Longitude.Min,
                (uint) (_locationDec.Longitude.Sec * 10000)
            };

            // LatitudeDenominator and LongitudeDenominator share the same values.
            uint[] denominator = 
            {
                1,
                1,
                10000
            };

            propertiesToSave.Add("System.GPS.LatitudeRef", _locationDec.Latitude.Ref);
            propertiesToSave.Add("System.GPS.LongitudeRef", _locationDec.Longitude.Ref);

            propertiesToSave.Add("System.GPS.LatitudeNumerator", latitudeNumerator);
            propertiesToSave.Add("System.GPS.LatitudeDenominator", denominator);
            propertiesToSave.Add("System.GPS.LongitudeNumerator", longitudeNumerator);
            propertiesToSave.Add("System.GPS.LongitudeDenominator", denominator);

            try
            {
                await m_imageProperties.SavePropertiesAsync(propertiesToSave);
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.Message);
                return false;
            }
            return true;
        }

        public async Task<Boolean> GetMetadata()
        {
            ImageProperties m_imageProperties;
            try
            {
                m_imageProperties = await _fileHandler.Properties.GetImagePropertiesAsync();
            }
            catch (FileNotFoundException)
            {
                return false;
            }

            if (m_imageProperties != null)
            {
                Position = new GpsLocation();

                if (m_imageProperties.Latitude != null)
                    Position.Latitude = Math.Min((double)m_imageProperties.Latitude, Gps.LATITUDE_DEG_MAX);

                if (m_imageProperties.Longitude != null)
                    Position.Longitude = Math.Min((double)m_imageProperties.Longitude, Gps.LONGITUDE_DEG_MAX);

                if ((m_imageProperties.DateTaken.Year > 1995) && (m_imageProperties.DateTaken.Year < 2030))
                    Date = m_imageProperties.DateTaken.DateTime;
                else
                    Date = _fileHandler.DateCreated.DateTime;

                if (Position.Latitude != 0)
                    PositionPresent = true;
            }
            else
            {
                PositionPresent = false;
                Position.Latitude = 0;
                Position.Longitude = 0;
                Date = _fileHandler.DateCreated.DateTime;
            }
            return true;
        }

        public async Task<Boolean> Download()
        {
            String _specifier = "#.##";
            String _pointsRequest = "https://maps.googleapis.com/maps/api/place/nearbysearch/json?location=";
            HttpResponseMessage response;

            _pointsRequest += Position.Latitude.ToString(_specifier).Replace(",", ".") + "," +
                Position.Longitude.ToString(_specifier).Replace(",", ".");

            _pointsRequest += "&radius=5000&sensor=false&key=" + GooglePlusServer.Key;

            //Create the Request URL for the routing service
            Uri routeRequest = new Uri(_pointsRequest);

            //Make a request and get the response
            HttpClient client = new HttpClient();

            try   
            {
                response = await client.GetAsync(routeRequest);
            }
            catch (HttpRequestException)
            {
                return false;
            }

            if (response.StatusCode != HttpStatusCode.OK)
                return false;

            Stream inputStream = await response.Content.ReadAsStreamAsync();
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(ResponsePlace));
            inputStream.Seek(0, SeekOrigin.Begin);

            ResponsePlace r0 = new ResponsePlace();
            r0 = ser.ReadObject(inputStream) as ResponsePlace;

            if (r0 != null &&
                r0.results != null &&
                r0.results.Count() > 0 &&
                r0.results[0].reference != null &&
                r0.results[0].reference.Length > 0)
            {
                //second request
                String _addressRequest = "https://maps.googleapis.com/maps/api/place/details/json?reference=";
                _addressRequest += r0.results[0].reference;
                _addressRequest += "&sensor=true&key=" + GooglePlusServer.Key;
                routeRequest = new Uri(_addressRequest);
                client = new HttpClient();

                try
                {
                    response = await client.GetAsync(routeRequest);
                }
                catch (HttpRequestException)
                {
                    return false;
                }

                Stream inputStreamAddress = await response.Content.ReadAsStreamAsync();
                ser = new DataContractJsonSerializer(typeof(ResponseAddress));
                inputStreamAddress.Seek(0, SeekOrigin.Begin);

                ResponseAddress r1 = new ResponseAddress();
                r1 = ser.ReadObject(inputStreamAddress) as ResponseAddress;

                if (r1 != null &&
                    r1.result != null &&
                    r1.result.address_components != null &&
                    r1.result.address_components.Count() > 0)
                {
                    foreach (AddressComp _comp in r1.result.address_components)
                    {
                        if (_comp.types[0].Equals("country"))
                        {
                            PictureInfos.Country = _comp.long_name;
                            PictureInfos.Code = _comp.short_name.ToLower();
                            PictureInfos.InfoPresent = true;
                        }
                        else if (_comp.types[0].Equals("administrative_area_level_1"))
                        {
                            PictureInfos.Region = _comp.short_name;
                        }
                        else if (_comp.types[0].Equals("locality"))
                        {
                            PictureInfos.City = _comp.long_name;
                        }
                        else if (_comp.types[0].Equals("administrative_area_level_2") && (PictureInfos.City == null))
                        {
                            PictureInfos.City = _comp.long_name;
                        }
                        else if (_comp.types[0].Equals("establishment"))
                        {
                            PictureInfos.Establishment = _comp.short_name;
                        }
                    }
                }
            }
            return PictureInfos.InfoPresent;
        }

        private DateTime ExtractDate(String dateRaw)
        {
            string pattern = "yyyy:MM:dd HH:mm:ss";
            DateTime parsedDate;
            DateTime.TryParseExact(dateRaw, pattern, null, DateTimeStyles.None, out parsedDate);
            return parsedDate;
        }

        public static async Task<StorageFile> CompressAndSaveFileAsync(String _pathIn, String _pathOut, String _nameOut, uint _sizePic, Boolean _square)
        {
            try
            {
                bool _inverted = false;

                StorageFolder folder_out = await StorageFolder.GetFolderFromPathAsync(_pathOut);
                StorageFile file_in = await StorageFile.GetFileFromPathAsync(_pathIn);
                StorageFile file_out = await folder_out.CreateFileAsync(_nameOut, CreationCollisionOption.ReplaceExisting);

                IRandomAccessStream sourceStream = await file_in.OpenAsync(FileAccessMode.Read);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);
                Double _ratio = (Double)decoder.OrientedPixelWidth / (Double)decoder.OrientedPixelHeight;
                BitmapTransform transform = new BitmapTransform();

                if (decoder.OrientedPixelWidth != decoder.PixelWidth)
                    _inverted = true;

                uint HeightOut, WidthOut;

                if (_ratio > 1) 
                {
                    transform.ScaledHeight = _sizePic;
                    transform.ScaledWidth = (uint)(_sizePic * _ratio);

                    HeightOut = _sizePic;
                    WidthOut = _square ? _sizePic : (uint)(_sizePic * _ratio);

                    if (_square && (_ratio < 2))
                        transform.Bounds = new BitmapBounds() { Height = HeightOut, Width = WidthOut, X = (uint)(_sizePic * (_ratio - 1)) };
                    else
                        transform.Bounds = new BitmapBounds() { Height = HeightOut, Width = WidthOut };
                }
                else
                {
                    if (_inverted)
                    {
                        transform.ScaledHeight = _sizePic;
                        transform.ScaledWidth = (uint)(_sizePic / _ratio); ;

                        HeightOut = _square ? _sizePic : (uint)(_sizePic / _ratio);
                        WidthOut =  _sizePic;

                        transform.Bounds = new BitmapBounds() { Height = HeightOut, Width = WidthOut };
                    }
                    else
                    {
                        transform.ScaledHeight = (uint)(_sizePic / _ratio);
                        transform.ScaledWidth = _sizePic;

                        HeightOut = _square ? _sizePic : (uint)(_sizePic / _ratio);
                        WidthOut = _sizePic;

                        transform.Bounds = new BitmapBounds() { Height = HeightOut, Width = WidthOut };
                    }
                }

                PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Rgba8,
                    BitmapAlphaMode.Straight,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                IRandomAccessStream destinationStream = await file_out.OpenAsync(FileAccessMode.ReadWrite);
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, destinationStream);
                encoder.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight, WidthOut, HeightOut, 96, 96, pixelData.DetachPixelData());
                await encoder.FlushAsync();
                destinationStream.Dispose();
                return file_out;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }
        }

        internal void Activate()
        {
            if (PositionPresent)
                MarkerPicture.Activate();
        }

        internal void DeActivate()
        {
            if (PositionPresent)
                MarkerPicture.DeActivate();
        }

        internal void CreateMarker(ViewMapTrip mapPage, MapUIElementCollection _element, int _count)
        {
            if (MarkerPicture != null)
                MarkerPicture.Delete();

            MarkerPicture = new MarkerPosition(mapPage, _element, _count, EIcon.IconFlag, Position, true, 20);
        }

        internal void RemoveMarker()
        {
            if ((PositionPresent) && (MarkerPicture != null))
                MarkerPicture.Delete();
        }

        public static IComparer sortTimeAscending()
        {
            return (IComparer)new sortTimeAscendingHelper();
        }

        private class sortTimeAscendingHelper : IComparer
        {
            int IComparer.Compare(object a, object b)
            {
                Picture c1 = (Picture)a;
                Picture c2 = (Picture)b;

                if (c1.Date > c2.Date)
                    return 1;
                else if (c1.Date < c2.Date)
                    return -1;
                else
                    return 0;
            }
        }

        int IComparable.CompareTo(object obj)
        {
            Picture c = (Picture)obj;
            return String.Compare(this.Date.ToString(), c.Date.ToString());
        }

        public static async Task<String> MoveUniqueNameAsync(string _nameNew, String _extension, StorageFile _srcFile, StorageFolder _destFolder, int _desiredIndex)
        {
            String _specifier = "D3";
            String _nameUpdt = (_desiredIndex == 0) ? _nameNew : _nameNew + " - " + _desiredIndex.ToString(_specifier);
            Boolean _exist = await DoesFileExistAsync(_destFolder.Path + "\\" + _nameUpdt + _extension);

            if (_exist)
            {
                do 
                {
                    _desiredIndex++;
                    _nameUpdt = _nameNew + " - " + _desiredIndex.ToString(_specifier);
                    _exist = await DoesFileExistAsync(_destFolder.Path + "\\" + _nameUpdt + _extension);
                } while (_exist);
            }
            await _srcFile.MoveAsync(_destFolder, _nameUpdt + _extension, NameCollisionOption.FailIfExists);
            return _nameUpdt;
        }

        public static async Task<bool> DoesFileExistAsync(string fileName)
        {
            try
            {
                await StorageFile.GetFileFromPathAsync(fileName);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }
        }

        public static async Task<bool> DoesFolderExistAsync(string _folderName)
        {
            try
            {
                await StorageFolder.GetFolderFromPathAsync(_folderName);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }
        }

        internal void RenameTopFolder(string _pathRoot, string _pathRootPrevious)
        {
            PathFolder = PathFolder.Replace(_pathRootPrevious, _pathRoot);
        }
    }
}
