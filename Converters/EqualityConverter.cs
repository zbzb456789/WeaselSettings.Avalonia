using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace WeaselSettings.Avalonia.Converters
{
    public class EqualityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter == null)
                return false;

            return string.Equals(value?.ToString(), parameter.ToString(), StringComparison.Ordinal);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // 只有“选中”时才把参数写回属性；“取消选中”时不要动绑定值，
            // 否则会把 string 属性写成 null，触发不必要的 OnChanged
            return value is bool b && b ? parameter?.ToString() : BindingOperations.DoNothing;
        }
    }
}
