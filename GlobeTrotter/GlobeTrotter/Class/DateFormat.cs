using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace GlobeTrotter
{
    public class DateFormat
    {
        public enum EMode
        {
            Month,
            DayDigit,
            Day,
            Hour
        }

        public static String[] _monthofYearEN =
        {
            "January",
            "February",
            "March",
            "April",
            "May",
            "June",
            "July",
            "August",
            "September",
            "October",
            "November",
            "December"
        };

        public static String[] _monthofYearFR =
        {
            "Janvier",
            "Fevrier",
            "Mars",
            "Avril",
            "Mai",
            "Juin",
            "Juillet",
            "Août",
            "Septembre",
            "Octobre",
            "Novembre",
            "Décembre"
        };

        public static String[] _monthofYearShortEN =
        {
            "Jan.",
            "Feb.",
            "Mar.",
            "Apr.",
            "May",
            "Jun.",
            "Jul.",
            "Aug.",
            "Sep.",
            "Oct.",
            "Nov.",
            "Dec."
        };

        public static String[] _monthofYearShortFR =
        {
            "Jan.",
            "Fév.",
            "Mar.",
            "Avr.",
            "Mai",
            "Jui.",
            "Jul.",
            "Aoû.",
            "Sep.",
            "Oct.",
            "Nov.",
            "Déc."
        };

        public static String Table(int _index, Boolean _short)
        {
            if (_short)
            {
                if (CultureInfo.CurrentCulture.Parent.Name == "fr")
                    return _monthofYearShortFR[_index - 1];
                else
                    return _monthofYearShortEN[_index - 1];
            }
            else
            {
                if (CultureInfo.CurrentCulture.Parent.Name == "fr")
                    return _monthofYearFR[_index - 1];
                else
                    return _monthofYearEN[_index - 1];
            }
        }

        public static String DateDisplayLocalization(DateTime _time, EMode _mode)
        {
            String _specifier = "D2";
            switch (_mode)
            {
                case EMode.Month:
                    return _time.Year + " " + DateFormat.Table(_time.Month, true);
                case EMode.DayDigit:
                    return _time.Year + "-" + _time.Month.ToString(_specifier) + "-" + _time.Day.ToString(_specifier);
                case EMode.Day:
                    {
                        if (CultureInfo.CurrentCulture.Parent.Name == "fr")
                            return _time.Day.ToString(_specifier) + " " + DateFormat.Table(_time.Month, true) + " " + _time.Year;
                        else
                            return DateFormat.Table(_time.Month, true) + " " + _time.Day.ToString(_specifier) + ", " + _time.Year;
                    }
                case EMode.Hour:
                    return _time.Date.Day.ToString(_specifier) + " " + DateFormat.Table(_time.Date.Month, true) + " " + _time.Year + " - " + _time.Hour + ":" + _time.Minute;
                default:
                    return _time.TimeOfDay.ToString();
            }
        }
    }
}
