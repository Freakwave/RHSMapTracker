using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui;
using Mapsui.Nts;
using Mapsui.Styles;
using Mapsui.Projections;
using Mapsui.Rendering;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using Mapsui.Layers;
using Mapsui.Nts.Extensions;
using Mapsui.Extensions;
using System.IO;

namespace LiveTrackingMap.Models
{
    public partial class DogTrackMapObject : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        private string _deviceName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        private string _customName = string.Empty;

        public string DisplayName
        {
            get
            {
                if(CustomName == DeviceName || CustomName == String.Empty)
                {
                    return DeviceName;
                }
                else
                {
                    return $"{CustomName} ({DeviceName})";
                }
            }
        }

        [ObservableProperty]
        private MPoint _currentMapPositionMPoint;

        private List<Coordinate> _trackPathNtsCoordinates = new List<Coordinate>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DogColorBrush))]
        [NotifyPropertyChangedFor(nameof(DogColorPen))]
        [NotifyPropertyChangedFor(nameof(ContrastDogColor))]
        [NotifyPropertyChangedFor(nameof(ContrastDogColorBrush))]
        private Color _dogColor;

        public Brush DogColorBrush => new Brush(DogColor);
        public Pen DogColorPen => new Pen(DogColor, 3);

        public Color ContrastDogColor => GetContrastingForegroundColor(DogColor);
        public Brush ContrastDogColorBrush => new Brush(ContrastDogColor);

        public LineString TrackGeometryNts { get; private set; }
        public GeometryFeature PathMapFeature { get; }

        [ObservableProperty]
        private PointFeature lastPositionMarkerMapFeature;

        public MPoint CurrentPosition => LastPositionMarkerMapFeature.Point;

        public List<MPoint>? Track => PathMapFeature?.Geometry?.Coordinates
                                    .Select(coord => coord.ToMPoint())
                                    .Reverse()
                                    .ToList();

        public double TrackLength => PathMapFeature?.Geometry?.Length ?? 0.0;




        private static int? _dogBitmapId;
        private static int GetDogBitmapId()
        {
            if (_dogBitmapId.HasValue)
            {
                return _dogBitmapId.Value;
            }

            using var resourceStream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("LiveTrackingMap.Assets.paw.svg");

            if (resourceStream == null)
            {
                _dogBitmapId = -1;
                return -1;
            }

            var permanentStream = new MemoryStream();
            resourceStream.CopyTo(permanentStream);
            permanentStream.Position = 0;

            _dogBitmapId = BitmapRegistry.Instance.Register(permanentStream);
            return _dogBitmapId.Value;
        }

        partial void OnDogColorChanged(Color value)
        {
            PathMapFeature?.Styles.OfType<VectorStyle>().FirstOrDefault()?.Line = DogColorPen;
            LastPositionMarkerMapFeature?.Styles.OfType<SymbolStyle>().FirstOrDefault()?.Line = DogColorPen;
            LastPositionMarkerMapFeature?.Styles.OfType<LabelStyle>().FirstOrDefault()?.BackColor = DogColorBrush;
            LastPositionMarkerMapFeature?.Styles.OfType<LabelStyle>().FirstOrDefault()?.ForeColor = GetContrastingForegroundColor(DogColor); ;
        }

        public DogTrackMapObject(string name, Color trackColor)
        {
            DeviceName = name;
            DogColor = trackColor;

            PathMapFeature = new GeometryFeature(new LineString(Array.Empty<Coordinate>()));
            PathMapFeature.Styles.Add(new VectorStyle { Line = DogColorPen });
            PathMapFeature["Name"] = $"{DisplayName} Path";

            LastPositionMarkerMapFeature = new PointFeature(0, 0);
            LastPositionMarkerMapFeature["Name"] = DisplayName;

            // Create bitmap style for the dog marker
            LastPositionMarkerMapFeature.Styles.Add(new SymbolStyle
            {
                BitmapId = GetDogBitmapId(),
                SymbolType = SymbolType.Image,
                UnitType = UnitType.Pixel,
                SymbolScale = 0.04,  
                Opacity = 0.8f,        
                SymbolOffset = new Offset(0, 0),
                Line = DogColorPen,
            });

            // Add label style
            LastPositionMarkerMapFeature.Styles.Add(new LabelStyle
            {
                LabelColumn = "Name",
                Font = new Font { FontFamily = "Arial", Size = 10 },
                Offset = new Offset(0, -25), // Adjusted offset to account for image height
                BackColor = DogColorBrush,
                ForeColor = GetContrastingForegroundColor(DogColor)
                //Halo = new Pen(Color.FromArgb(120, 255, 255, 255), 1)
            });
            _currentMapPositionMPoint = new MPoint(0, 0);
        }

        public static Color GetContrastingForegroundColor(Color backgroundColor)
        {
            double luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
            return luminance > 0.5 ? Color.Black : Color.White;
        }

        public void UpdateName(string newName)
        {
            DeviceName = newName;
            LastPositionMarkerMapFeature["Name"] = DisplayName;
            PathMapFeature["Name"] = $"{DisplayName} Path"; 
        }

        public void AddPoint(double longitudeDegrees, double latitudeDegrees)
        {
            MPoint newMapPoint = SphericalMercator.FromLonLat(longitudeDegrees, latitudeDegrees).ToMPoint();
            var ntsCoordinate = newMapPoint.ToCoordinate();

            _trackPathNtsCoordinates.Add(ntsCoordinate);

            if (_trackPathNtsCoordinates.Count >= 2)
            {
                PathMapFeature.Geometry = new LineString(_trackPathNtsCoordinates.ToArray());
            }
            else
            {
                PathMapFeature.Geometry = new LineString(Array.Empty<Coordinate>());
            }

            LastPositionMarkerMapFeature.Point.X = newMapPoint.X;
            LastPositionMarkerMapFeature.Point.Y = newMapPoint.Y;

            OnPropertyChanged(nameof(CurrentPosition));
            OnPropertyChanged(nameof(TrackLength));
            OnPropertyChanged(nameof(Track));

            CurrentMapPositionMPoint = newMapPoint;
        }
    }
}