using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;


namespace GlobeTrotter
{
    public class PercentToDateConverter : IValueConverter
    {
        public Type TypeDisplay;

        public PercentToDateConverter()
        {
            CurrentName = "";
            FinalName = "";
        }

        public List<StAlbumDateName> DateNameDisplay;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((DateNameDisplay != null) && (value != null) && (value.GetType() == typeof(System.Double)))
            {
                int index = (int)Math.Round((double)value);
                return DateFormat.DateDisplayLocalization(DateNameDisplay[index].Date, Mode);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return 0;
        }

        public string CurrentName { get; set; }

        public string FinalName { get; set; }

        public DateFormat.EMode Mode { get; set; }
    }
}
