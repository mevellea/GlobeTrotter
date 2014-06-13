using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlobeTrotter
{
    public enum EResourceType
    {
        ITINERARY,
        MAP,
        PLACE
    }

    class Quota
    {
        public static uint QUOTA_MAX = 1000;

        private DateTime _dayToday;
        private uint _quota;
        private App _app;

        Quota(App _a)
        {
            _app = _a;
            //_dayToday = _app.AppSettings.GetDayQuota();
            _dayToday = DateTime.Now;

            if (_dayToday.DayOfYear == DateTime.Today.DayOfYear)
                Load();
            else
                Reinit();
        }

        public Boolean LimitReached()
        {
            return (_quota == 0);
        }

        public void Update(EResourceType _type)
        {
            switch (_type)
            {
                case EResourceType.ITINERARY:
                    {
                        _quota -= 10;
                        break;
                    }
                case EResourceType.MAP:
                    {
                        _quota -= 1;
                        break;
                    }
                case EResourceType.PLACE:
                    {
                        _quota -= 5;
                        break;
                    }     
            }
        }

        public void Save()
        {
        }

        public void Reinit()
        {
            _quota = QUOTA_MAX;
            Save();
        }

        private void Load()
        {
        }
    }
}
