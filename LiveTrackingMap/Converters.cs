using Mapsui;
using Mapsui.Projections;
using Mapsui.Styles;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace LiveTrackingMap
{
    public class CoordinateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MPoint coordinates) // Assuming your x/y are in a Point struct or similar
            {
                string format = LiveTrackingMap.Properties.Settings.Default.CoordinateSystem;

                var lonlat = SphericalMercator.ToLonLat(coordinates.X, coordinates.Y);
                switch (format)
                {
                    case "UTM":
                        return CoordinateTranformer.ToUtm(lonlat.lon, lonlat.lat).ToString(); // Placeholder
                    case "LatLongDegrees":
                        return $"Deg: {lonlat.lon:F6}, {lonlat.lat:F6}"; // Placeholder
                    default:
                        return $"{coordinates.X}, {coordinates.Y}";
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }



    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Media.Color mediaColor) // Assuming your x/y are in a Point struct or similar
            {
                return Mapsui.Styles.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
            }
            else if (value is Mapsui.Styles.Color mapsColor)
            {
                return System.Windows.Media.Color.FromArgb((byte)mapsColor.A, (byte)mapsColor.R, (byte)mapsColor.G, (byte)mapsColor.B);
            }
            else if (value is System.Windows.Media.SolidColorBrush mediaBrush)
            {
                return new Mapsui.Styles.Brush(new Mapsui.Styles.Color(mediaBrush.Color.A, mediaBrush.Color.R, mediaBrush.Color.G, mediaBrush.Color.B));
            }
            else if (value is Mapsui.Styles.Brush mapsBrush)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)mapsBrush.Color.A, (byte)mapsBrush.Color.R, (byte)mapsBrush.Color.G, (byte)mapsBrush.Color.B));
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(value, targetType, parameter, culture);
        }
    }
}
