using System;
using System.Globalization;
using System.Windows.Data;

namespace NetShaper.App.Converters
{
    /// <summary>
    /// Converts an enum value to its integer index and back.
    /// </summary>
    public sealed class EnumToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum)
                return System.Convert.ToInt32(value);
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && targetType.IsEnum)
                return Enum.ToObject(targetType, intValue);
            return Enum.ToObject(targetType, 0);
        }
    }
}
