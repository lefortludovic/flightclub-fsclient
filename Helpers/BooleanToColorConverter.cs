using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FlightClub.FsClient.Helpers;

public class BooleanToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(System.Windows.Media.Color.FromRgb(0xA6, 0xE3, 0xA1));
    private static readonly SolidColorBrush RedBrush = new(System.Windows.Media.Color.FromRgb(0xF3, 0x8B, 0xA8));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? GreenBrush : RedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
