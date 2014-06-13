using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.UI.Popups;
using Windows.Storage;
using System.Diagnostics;

namespace GlobeTrotter
{
    public class AlbumSummary
    {
        public String Id;
        public String Name;
        public String StrLocation;
        public String StrLocationShort;
        public String PathThumb;
        public List<String> PicturesThumb;
        public Boolean Sample;

        public AlbumSummary()
        {
            PicturesThumb = new List<String>();
        }
    }
}
