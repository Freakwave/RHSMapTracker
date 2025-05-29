using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui;
using Mapsui.Nts;
using Mapsui.Styles;
using Mapsui.Projections;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using Mapsui.Layers;
using Mapsui.Nts.Extensions;
using Mapsui.Extensions;

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
        private Color _dogColor;

        partial void OnDogColorChanged(Color value)
        {
            PathMapFeature?.Styles.OfType<VectorStyle>().FirstOrDefault()?.Line = DogColorPen;
            LastPositionMarkerMapFeature?.Styles.OfType<SymbolStyle>().FirstOrDefault()?.Fill = DogColorBrush;
        }

        public Brush DogColorBrush => new Brush(DogColor);
        public Pen DogColorPen => new Pen(DogColor, 3);

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

        public DogTrackMapObject(string name, Color trackColor)
        {
            DeviceName = name;
            DogColor = trackColor;

            PathMapFeature = new GeometryFeature(new LineString(Array.Empty<Coordinate>()));
            PathMapFeature.Styles.Add(new VectorStyle { Line = DogColorPen });
            PathMapFeature["Name"] = $"{DisplayName} Path";

            LastPositionMarkerMapFeature = new PointFeature(0, 0);
            LastPositionMarkerMapFeature["Name"] = DisplayName;
            LastPositionMarkerMapFeature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 0.7,
                Fill = DogColorBrush,
                Outline = new Pen(Color.Black, 1)
            });
            LastPositionMarkerMapFeature.Styles.Add(new LabelStyle
            {
                LabelColumn = "Name",
                Font = new Font { FontFamily = "Arial", Size = 10 },
                Offset = new Offset(0, -15),
                BackColor = new Brush(Color.FromArgb(180, 255, 255, 255)),
                Halo = new Pen(Color.FromArgb(120, 255, 255, 255), 1)
            });
            _currentMapPositionMPoint = new MPoint(0, 0);
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