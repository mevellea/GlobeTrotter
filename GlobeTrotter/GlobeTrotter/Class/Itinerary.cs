using System;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Bing.Maps;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.Search;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using GoogleService.Common.JSON;
using BingMapsService.Common.JSON;
using Windows.Storage.AccessCache;

namespace GlobeTrotter
{
    [DataContract]
    public class Itinerary
    {
        public Boolean DownloadSucceeded;
        public GpsLocation[] Points;
        public uint Distance;
        public List<String> EncodedItineraries;
        public Boolean RequestDownload;

        private static uint DISTANCE_KM_MAX = 3000;

        public Itinerary()
        {
            Points = new GpsLocation[2];
            Points[0] = new GpsLocation();
            Points[1] = new GpsLocation();

            DownloadSucceeded = false;
            RequestDownload = false;
        }

        public void UpdatePoints(GpsLocation _point, int _index)
        {
            if (Points.Length > _index)
            {
                if (!Points[_index].Equals(_point))
                {
                    Points[_index].Copy(_point);

                    if (!(Points[0].Undefined() || Points[1].Undefined()))
                        RequestDownload = true;
                }
            }
        }

        public async Task<Boolean> Download()
        {
            if (RequestDownload)
            {
                DownloadSucceeded = false;

                String _specifier = "#.##";
                String _pointsRequest = "https://maps.googleapis.com/maps/api/directions/json?";

                _pointsRequest += "origin=" +
                    Points[0].Latitude.ToString(_specifier).Replace(",", ".") + "," +
                    Points[0].Longitude.ToString(_specifier).Replace(",", ".") + "&";

                _pointsRequest += "destination=" +
                    Points[1].Latitude.ToString(_specifier).Replace(",", ".") + "," +
                    Points[1].Longitude.ToString(_specifier).Replace(",", ".") + "&";

                _pointsRequest += "sensor=false";

                //Create the Request URL for the routing service
                Uri routeRequest = new Uri(_pointsRequest);

                //Make a request and get the response
                HttpClient client = new HttpClient();

                HttpResponseMessage response = null;

                try
                {
                    response = await client.GetAsync(routeRequest);
                }
                catch (HttpRequestException)
                {
	                return false;
                }

                if ((response != null) && (response.StatusCode == HttpStatusCode.OK))
                {
                    Stream inputStream = await response.Content.ReadAsStreamAsync();
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(ResponseDirection));
                    inputStream.Seek(0, SeekOrigin.Begin);
                    EncodedItineraries = new List<string>();

                    ResponseDirection r = new ResponseDirection();
                    r = ser.ReadObject(inputStream) as ResponseDirection;

                    if (r != null &&
                        r.routes != null &&
                        r.routes.Count() > 0 &&
                        r.routes[0].legs != null &&
                        r.routes[0].legs.Count() > 0 &&
                        r.routes[0].legs[0].steps != null &&
                        r.routes[0].legs[0].steps.Count() > 0)
                    {
                        Distance = 0;
                        foreach (Step step in r.routes[0].legs[0].steps)
                        {
                            uint _distance = (uint)step.distance.value / 1000;
                            Distance += _distance;
                            EncodedItineraries.Add(step.polyline.points);
                        }
                        if ((Distance > 0) && (Distance < DISTANCE_KM_MAX))
                            DownloadSucceeded = true;
                    
                        RequestDownload = false;
                    }
                }
            }
            return DownloadSucceeded;
        }

        public void Remove(MapShapeLayer routeLayer)
        {
            if (routeLayer.Shapes.Count > 0)    
                routeLayer.Shapes.RemoveAt(0);
        }

        public void Display(MapShapeLayer _routeLayer, Boolean _lowPerfo)
        {
            if (!DownloadSucceeded || (Points.Length < 2))
                return;

            LocationCollection ItineraryList = new LocationCollection();
            MapPolyline routeLine = new MapPolyline();

            double _numberPoints = EncodedItineraries.Count();

            foreach (String _encodedItinerary in EncodedItineraries)
            {
                //Get the route line data
                List<GpsLocation> _routePath = DecodePolyline(_encodedItinerary);

                int RATIO = (_lowPerfo)?30:3;

                if (_routePath.Count >= 2)
                {
                    for (int _idx = _routePath.Count - 1; _idx >= RATIO - 1; _idx = _idx - RATIO)
                        for (int _idxRatio = 0; _idxRatio < RATIO-1; _idxRatio++)
                            _routePath.RemoveAt(_idx - _idxRatio);

                    foreach (GpsLocation _location in _routePath)
                        ItineraryList.Add(new Bing.Maps.Location(_location.Latitude, _location.Longitude));
                }
            }


            //Create a MapPolyline of the route and add it to the map
            routeLine = new MapPolyline()
            {
                Color = Colors.Red,
                Locations = ItineraryList,
                Width = 5
            };

            _routeLayer.Shapes.Add(routeLine);
        }

        public List<GpsLocation> DecodePolyline(String _encodedPoints)
        {
            int index = 0;
            int lat = 0;
            int lng = 0;
            List<GpsLocation> val = new List<GpsLocation>();

            try
            {
                int shift;
                int result;
                while (index < _encodedPoints.Length)
                {
                    shift = 0;
                    result = 0;
                    while (true)
                    {
                        int b = _encodedPoints[index++] - '?';
                        result |= ((b & 31) << shift);
                        shift += 5;
                        if (b < 32)
                            break;
                    }
                    lat += ((result & 1) != 0 ? ~(result >> 1) : result >> 1);

                    shift = 0;
                    result = 0;
                    while (true)
                    {
                        int b = _encodedPoints[index++] - '?';
                        result |= ((b & 31) << shift);
                        shift += 5;
                        if (b < 32)
                            break;
                    }
                    lng += ((result & 1) != 0 ? ~(result >> 1) : result >> 1);

                    val.Add(new GpsLocation(lat / 1e5, lng / 1e5));
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            return val;
        }
    }
}
