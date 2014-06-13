using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.Maps;
using Bing.Maps.Search;

namespace GlobeTrotter
{
    public class PlaceInfos
    {
        public Boolean InfoPresent;
        public String City;
        public String Region;
        public String Establishment;
        public String Country;
        public String Code;

        public PlaceInfos()
        {
            InfoPresent = false;
        }

        public void Clear()
        {
            City = "";
            Region = "";
            Establishment = "";
            Country = "";
            Code = "";
        }
    }
}
