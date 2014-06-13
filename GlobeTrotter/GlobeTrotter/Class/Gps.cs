using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using Bing.Maps;
using System.Diagnostics;

namespace GlobeTrotter
{
    public class GpsUnitDec
    {
        public enum ECoordType
        {
            Latitude,
            Longitude
        }

        private String _ref;
        private double _deg;
        private double _min;
        private double _sec;

        public double Deg
        {
            get { return _deg; }
            set
            {
                if ((value > 0) &&
                    (((_ref == "N") || (_ref == "S")) && (value < Gps.LATITUDE_DEG_MAX)) ||
                    (((_ref == "E") || (_ref == "W")) && (value < Gps.LONGITUDE_DEG_MAX)))
                    _deg = value;
                else
#if DEBUG
                    throw new IndexOutOfRangeException();
#else
                    _deg = Gps.LONGITUDE_DEG_MAX;
#endif
            }
        }    

        public double Min
        {
            get { return _min; }
            set
            {
                if ((value > 0) &&
                    (((_ref == "N") || (_ref == "S")) && (value < Gps.LATITUDE_MIN_MAX)) ||
                    (((_ref == "E") || (_ref == "W")) && (value < Gps.LONGITUDE_MIN_MAX)))
                        _min = value;
                else
#if DEBUG
                    throw new IndexOutOfRangeException();
#else
                    _min = Gps.LONGITUDE_MIN_MAX;
#endif
            }
        }

        public double Sec
        {
            get { return _sec; }
            set
            {
                if ((value > 0) &&
                    (((_ref == "N") || (_ref == "S")) && (value < Gps.LATITUDE_SEC_MAX)) ||
                    (((_ref == "E") || (_ref == "W")) && (value < Gps.LONGITUDE_SEC_MAX)))
                    _sec = value;
                else
#if DEBUG
                    throw new IndexOutOfRangeException();
#else
                    _sec = Gps.LONGITUDE_SEC_MAX;
#endif
            }
        }

        public string Ref
        {
            get { return _ref;}
            set
            {
                if ((value == "N") || (value == "S") || (value == "E") || (value == "W"))
                    _ref = value;
                else
#if DEBUG
                    throw new IndexOutOfRangeException();
#else
                    _ref = "N";
#endif
            }
        }

        public GpsUnitDec()
        {
            Ref = "N";
            Deg = 0;
            Min = 0;
            Sec = 0;
        }

        public GpsUnitDec(Double coord, ECoordType _type)
        {
            if (_type == ECoordType.Latitude)
                Ref = (coord >= 0) ? "N" : "S";
            else
                Ref = (coord >= 0) ? "E" : "W";

            Deg = Math.Floor(Math.Abs(coord));
            Min = Math.Floor((Math.Abs(coord) - Deg) * 60);
            Sec = ((Math.Abs(coord) - Deg - Min / 60) * 3600);
        }
    }

    public class GpsLocationDec
    {
        public GpsUnitDec Latitude;
        public GpsUnitDec Longitude;

        public GpsLocationDec(GpsLocation _location)
        {
            Latitude = new GpsUnitDec(_location.Latitude, GpsUnitDec.ECoordType.Latitude);
            Longitude = new GpsUnitDec(_location.Longitude, GpsUnitDec.ECoordType.Longitude);
        }
    }

    public class GpsLocation
    {
        private double _latitude;
        private double _longitude;

        public double Latitude
        {
            get { return _latitude; }
            set
            {
                if (value < Gps.LATITUDE_MIN)
                    _latitude = Gps.LATITUDE_MIN;
                else if (value > Gps.LATITUDE_MAX)
                    _latitude = Gps.LATITUDE_MAX;
                else
                    _latitude = value;
            }
        }

        public double Longitude
        {
            get { return _longitude; }
            set
            {
                if (value < Gps.LONGITUDE_MIN)
                    _longitude = Gps.LONGITUDE_MIN;
                else if (value > Gps.LONGITUDE_MAX)
                    _longitude = Gps.LONGITUDE_MAX;
                else
                    _longitude = value;
            }
        }

        public GpsLocation()
        {
            Latitude = 0;
            Longitude = 0;
        }

        public GpsLocation(GpsLocation _location)
        {
            Latitude = _location.Latitude;
            Longitude = _location.Longitude;
        }

        public GpsLocation(Double _latitude, Double _longitude)
        {
            Latitude = _latitude;
            Longitude = _longitude;
        }

        public GpsLocation(Location _location)
        {
            Latitude = _location.Latitude;
            Longitude = _location.Longitude;
        }

        public Boolean Equals(GpsLocation _locCompare)
        {
            return ((Latitude == _locCompare.Latitude) && (Longitude == _locCompare.Longitude));
        }

        public static GpsLocation operator +(GpsLocation c1, GpsLocation c2)
        {
            return new GpsLocation(c1.Latitude + c2.Latitude, c1.Longitude + c2.Longitude);
        }

        public static GpsLocation operator -(GpsLocation c1, GpsLocation c2)
        {
            return new GpsLocation(c1.Latitude - c2.Latitude, c1.Longitude - c2.Longitude);
        }

        public static GpsLocation operator /(GpsLocation c1, int _val)
        {
            return new GpsLocation(c1.Latitude / _val, c1.Longitude / _val);
        }

        public Boolean Undefined()
        {
            return ((Latitude == 0) && (Longitude == 0));
        }

        public void Copy(GpsLocation _point)
        {
            Latitude = _point.Latitude;
            Longitude = _point.Longitude;
        }
    }

    public class GpsRect
    {
        public GpsLocation TopLeft;
        public GpsLocation BottomRight;
        public GpsLocation TopLeftDisp;
        public GpsLocation BottomRightDisp;
        public GpsLocation Center;

        public GpsRect()
        {
            TopLeft = new GpsLocation();
            BottomRight = new GpsLocation();
            TopLeftDisp = new GpsLocation();
            BottomRightDisp = new GpsLocation();
            Center = new GpsLocation();
        }

        public GpsRect(GpsLocation _location)
        {
            TopLeft = new GpsLocation(_location);
            BottomRight = new GpsLocation(_location);
            TopLeftDisp = new GpsLocation(_location);
            BottomRightDisp = new GpsLocation(_location);
            Center = new GpsLocation(_location);
        }

        public GpsRect(GpsRect _other)
        {
            TopLeft = new GpsLocation(_other.TopLeft);
            BottomRight = new GpsLocation(_other.BottomRight);
            TopLeftDisp = new GpsLocation(_other.TopLeftDisp);
            BottomRightDisp = new GpsLocation(_other.BottomRightDisp);
            Center = new GpsLocation(_other.Center);
        }

        public void UpdateRangeFromLocation(GpsLocation _location)
        {
            if (_location.Latitude > TopLeft.Latitude)
                TopLeft.Latitude = _location.Latitude;
            else if (_location.Latitude < BottomRight.Latitude)
                BottomRight.Latitude = _location.Latitude;

            if (_location.Longitude < TopLeft.Longitude)
                TopLeft.Longitude = _location.Longitude;
            else if (_location.Longitude > BottomRight.Longitude)
                BottomRight.Longitude = _location.Longitude;
        }

        public void UpdateRangeFromRect(GpsRect _rectNew)
        {
            if (_rectNew.TopLeft.Latitude > TopLeft.Latitude)
                TopLeft.Latitude = _rectNew.TopLeft.Latitude;
            else if (_rectNew.BottomRight.Latitude < BottomRight.Latitude)
                BottomRight.Latitude = _rectNew.BottomRight.Latitude;

            if (_rectNew.TopLeft.Longitude < TopLeft.Longitude)
                TopLeft.Longitude = _rectNew.TopLeft.Longitude;
            else if (_rectNew.BottomRight.Longitude > BottomRight.Longitude)
                BottomRight.Longitude = _rectNew.BottomRight.Longitude;
        }

        public void UpdateDisp(Double _offset)
        {
            TopLeftDisp.Latitude = TopLeft.Latitude + _offset;
            BottomRightDisp.Latitude = BottomRight.Latitude - _offset;
            TopLeftDisp.Longitude = TopLeft.Longitude - _offset;
            BottomRightDisp.Longitude = BottomRight.Longitude + _offset;

            if ((BottomRight.Longitude > TopLeft.Longitude) && 
                (BottomRight.Longitude < Gps.LONGITUDE_DEG_MAX) && (TopLeft.Longitude < Gps.LONGITUDE_DEG_MAX))
                BottomRightDisp.Longitude += (BottomRight.Longitude - TopLeft.Longitude) / 2;

            Center.Copy(BottomRight);
        }

        public void UpdateDispDiff(Double _offset)
        {
            TopLeftDisp.Latitude = TopLeft.Latitude + _offset * (TopLeft.Latitude - BottomRight.Latitude);
            BottomRightDisp.Latitude = BottomRight.Latitude - _offset * (TopLeft.Latitude - BottomRight.Latitude);
            TopLeftDisp.Longitude = TopLeft.Longitude - _offset * (BottomRight.Longitude - TopLeft.Longitude);
            BottomRightDisp.Longitude = BottomRight.Longitude + 2 * _offset * (BottomRight.Longitude - TopLeft.Longitude);

            Center = (BottomRight + TopLeft) / 2;
        }
    };

    public class Gps
    {
        public static int LATITUDE_MAX = +90;
        public static int LATITUDE_MIN = -90;
        public static int LONGITUDE_MAX = +180;
        public static int LONGITUDE_MIN = -180;

        public static int LATITUDE_DEG_MAX = +90;
        public static int LONGITUDE_DEG_MAX = +180;

        public static int LATITUDE_MIN_MAX = 60;
        public static int LONGITUDE_MIN_MAX = 60;

        public static int LATITUDE_SEC_MAX = 60;
        public static int LONGITUDE_SEC_MAX = 60;

        public static Location ConvertGpsToLoc(GpsLocation _location)
        {
            return new Location(_location.Latitude, _location.Longitude);
        }

        public static LocationRect ConvertGpsToRect(GpsRect _rect)
        {
            return new LocationRect(ConvertGpsToLoc(_rect.TopLeftDisp), ConvertGpsToLoc(_rect.BottomRightDisp));
        }

        public static String GetDistanceFormat(Double _distance, Boolean _unitMiles)
        {
            String _unit = " km";

            if (_unitMiles)
            {
                _unit = " miles";
                _distance /= 1.609;
            }

            if (_distance != 0)
                return (_distance).ToString("N0", CultureInfo.InvariantCulture) + _unit;
            else
                return "";
        }
    }
}
