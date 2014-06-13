using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlobeTrotter
{
    public class StAlbumDateName
    {
        public DateTime Date;
        public String Name;

        public StAlbumDateName(String _nameIn, DateTime _dateIn)
        {
            Date = _dateIn;
            Name = _nameIn;
        }
    }
}
