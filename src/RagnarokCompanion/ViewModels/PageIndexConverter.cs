using System;
using System.Globalization;
using System.Windows.Data;
namespace RagnarokCompanion.ViewModels;
public sealed class PageIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is int index && index.ToString(CultureInfo.InvariantCulture) == parameter?.ToString();
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? int.Parse(parameter.ToString()!, CultureInfo.InvariantCulture) : Binding.DoNothing;
}
