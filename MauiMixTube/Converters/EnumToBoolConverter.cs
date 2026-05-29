using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MauiMixTube.Converters
{
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string checkValue = value?.ToString() ?? String.Empty;
            string targetValue = parameter?.ToString() ?? String.Empty;

            return checkValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is string targetValue)
            {
                return Enum.Parse(targetType, targetValue);
            }

            return Binding.DoNothing;
        }
    }

}
