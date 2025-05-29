using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
// Assuming your service and models are directly in LiveTrackingMap
// If they are in sub-namespaces like LiveTrackingMap.Services, add:
// using LiveTrackingMap.Services; 
// using LiveTrackingMap.Models; // (if you create this for PvtDataD800, DogCollarData etc.)

using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Nts.Editing;
using Mapsui.Nts.Extensions;
using Mapsui.Nts.Widgets;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Mapsui.Tiling;
using Mapsui.UI;
using Mapsui.Widgets;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Brush = Mapsui.Styles.Brush;
using Color = Mapsui.Styles.Color;
using Pen = Mapsui.Styles.Pen; // For Application.Current.Dispatcher

// Ensure all your classes (GarminUsbService, PvtDataD800, DogCollarData, CoordinateConverter etc.)
// are also declared within this namespace or that you have the correct using directives
// if they are in sub-namespaces of LiveTrackingMap.
namespace LiveTrackingMap // Or just LiveTrackingMap if MainViewModel is there
{
    // Helper class DogTrackMapObject (can be in this file or its own .cs file within the namespace)
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
        // This list will store all NTS coordinates for the track
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

        public MPoint CurrentPosition
        {
            get
            {
                return LastPositionMarkerMapFeature.Point;
            }
        }

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

            // Add the new coordinate to the list of vertices for the NTS LineString
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

    public partial class MainViewModel : ObservableObject
    {
        private readonly GarminUsbService _garminService; // Assuming GarminUsbService is in LiveTrackingMap or LiveTrackingMap.Services
        public Map Map { get; }

        private readonly MemoryLayer _mainDeviceLayer;
        private readonly Mapsui.Layers.PointFeature _mainDevicePositionFeature;

        private readonly MemoryLayer _dogTracksLayer;
        private readonly Dictionary<int, DogTrackMapObject> _dogTrackObjects = new Dictionary<int, DogTrackMapObject>();

        private EditManager? _editManager;
        private EditManipulation? _editManipulation;
        private WritableLayer? _editablePolygonLayer; // Layer to draw new polygons on
        private IMapControl? _mapControlRef; // To store reference to the MapControl

        [ObservableProperty]
        private bool _isDrawingPolygon;

        [ObservableProperty]
        private string _statusMessage = "Ready. Click 'Start Listening'.";

        [ObservableProperty]
        private bool _isListening;

        [ObservableProperty]
        private PvtDataD800? _currentPvtData; // Assuming PvtDataD800 is in LiveTrackingMap or LiveTrackingMap.Models

        [ObservableProperty]
        private string? _currentUtmString;

        private Random _mapsuiColorRandom = new Random();

        public ObservableCollection<DogTrackMapObject> DisplayableDogTracks { get; } = new ObservableCollection<DogTrackMapObject>();

        [ObservableProperty]
        private DogTrackMapObject? _selectedDogTrackForInfo; // For SelectedItem binding from ListBox

        public IRelayCommand<DogTrackMapObject> ShowDogInfoCommand { get; }
        public IRelayCommand OpenDogInfoWindowCommand { get; } // For button if not using ListBox selection directly



        public MainViewModel()
        {
            Map = new Map();
            Map.Layers.Add(OpenStreetMap.CreateTileLayer());

            _mainDevicePositionFeature = new Mapsui.Layers.PointFeature(0, 0) { ["Name"] = "Main Device" };
            _mainDevicePositionFeature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Triangle,
                SymbolScale = 1.0,
                Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Blue),
                Outline = new Pen(Mapsui.Styles.Color.White, 2)
            });
            _mainDeviceLayer = new MemoryLayer("Main Device Layer")
            {
                Features = new List<IFeature> { _mainDevicePositionFeature },
                IsMapInfoLayer = true
            };
            Map.Layers.Add(_mainDeviceLayer);

            _dogTracksLayer = new MemoryLayer("Dog Tracks Layer")
            {
                Features = new List<IFeature>(),
                IsMapInfoLayer = true
            };
            Map.Layers.Add(_dogTracksLayer);

            Map.Widgets.Add(new MapInfoWidget(Map));

            var marlLon = 7.06933; // Bebelstraße Lon
            var marlLat = 51.66255; // Bebelstraße Lat
            var marlMapCenter = SphericalMercator.FromLonLat(marlLon, marlLat).ToMPoint();
            double initialResolution = 50; // Example resolution
            try
            {
                // Note: Map.Resolutions may not exist directly or be populated at this stage in all Mapsui versions.
                // It's often better to use known good resolution values for OSM.
                // Example: OSM Level 14 is ~9.55, Level 10 is ~152.87. Level 16 is ~2.38.
                // For a neighborhood view, resolution between 1 and 10 might be good.
                initialResolution = 20; // Adjust as needed
            }
            catch { /* Use default if Resolutions access fails */ }
            Map.Home = navigator => navigator.CenterOnAndZoomTo(marlMapCenter, initialResolution);

            // Assuming GarminUsbService is in the same namespace or a sub-namespace like LiveTrackingMap.Services
            _garminService = new GarminUsbService();
            _garminService.StatusMessageChanged += GarminService_StatusMessageChanged;
            _garminService.IsConnectedChanged += OnIsConnectedChanged;
            _garminService.MainDevicePvtUpdated += OnMainDevicePvtUpdated;
            _garminService.DogDataUpdated += OnDogDataUpdated;

            // Layer for user-drawn polygons
            _editablePolygonLayer = new WritableLayer()
            {
                Style = CreateEditLayerStyle(), 
                IsMapInfoLayer = true
               
            };
            Map.Layers.Add(_editablePolygonLayer);

            StartListeningAsync();
            TogglePolygonEditCommand = new RelayCommand(TogglePolygonEdit);
        }

        [RelayCommand]
        private void ShowDogInfoWindow(DogTrackMapObject? dogMapObject)
        {
            DogInfoWindow infoWindow = new DogInfoWindow
            {
                DataContext = dogMapObject,
                Owner = Application.Current.MainWindow // Ensures proper window behavior
            };
            infoWindow.Show(); // Use ShowDialog() for a modal window
        }

        private static StyleCollection CreateEditLayerStyle()
        {
            return new StyleCollection
            {
                Styles = {
                CreateEditLayerBasicStyle(),
                CreateSelectedStyle()
            }
            };
        }

        private static IStyle CreateEditLayerBasicStyle()
        {
            var editStyle = new VectorStyle
            {
                Fill = new Brush(EditModeColor),
                Line = new Pen(EditModeColor, 3),
                Outline = new Pen(EditModeColor, 3)
            };
            return editStyle;
        }

        private static readonly Color EditModeColor = new Color(255, 0, 0, 180);

        private static IStyle CreateSelectedStyle()
        {
            // To show the selected style a ThemeStyle is used which switches on and off the SelectedStyle
            // depending on a "Selected" attribute.
            return new ThemeStyle(f => (bool?)f["Selected"] == true ? SelectedStyle : DisableStyle);
        }
        private static readonly SymbolStyle? SelectedStyle = new SymbolStyle
        {
            Fill = null,
            Outline = new Pen(Color.Red, 3),
            Line = new Pen(Color.Red, 3)
        };

        private static readonly SymbolStyle? DisableStyle = new SymbolStyle { Enabled = false };

        public IAsyncRelayCommand StartListeningCommand { get; }
        public IRelayCommand StopListeningCommand { get; }

        private void GarminService_StatusMessageChanged(string message)
        {
            Application.Current.Dispatcher.Invoke(() => StatusMessage = message);
        }

        private void OnIsConnectedChanged(bool connected)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsListening = connected;
            });
        }

        private async Task StartListeningAsync()
        {
            StatusMessage = "Attempting to start USB listener...";
            // Optimistically set IsListening, let OnIsConnectedChanged confirm
            Application.Current.Dispatcher.Invoke(() => {
                IsListening = true;
            });
            await _garminService.StartAsync();
        }

        private void StopListening()
        {
            _garminService.Stop();
            // OnIsConnectedChanged should set IsListening to false via the service
        }

        public void InitializeEditing(IMapControl mapControl)
        {
            _mapControlRef = mapControl;
            if (_mapControlRef == null || _editablePolygonLayer == null) return;

            _editManipulation = new EditManipulation(); // Handles mouse/touch for editing

            _editManager = new EditManager
            {
                Layer = _editablePolygonLayer, // The layer where new features will be created
                                               // EditManipulation = _editManipulation // Set if EditManager constructor/property expects it directly
            };

            if (_mapControlRef.Map != this.Map)
            {
                StatusMessage = "Warning: MapControl's Map instance might differ from ViewModel's Map.";
            }
            var editingWidget = new EditingWidget(mapControl, _editManager, _editManipulation);
            Map.Widgets.Add(editingWidget);


            TogglePolygonEditCommand.NotifyCanExecuteChanged(); // Enable command now
            StatusMessage = "Editing tools initialized. Select a mode.";
        }


        public IRelayCommand TogglePolygonEditCommand { get; }

        public void TogglePolygonEdit()
        {
            if (_editManager == null) return;

            if (_editManager.EditMode != EditMode.None)
            {
                _editManager.EndEdit();
                _editManager.EditMode = EditMode.None; // Turn off drawing
                IsDrawingPolygon = false;
                var drawnFeature = _editablePolygonLayer?.GetFeatures().LastOrDefault() as GeometryFeature;
                string? v = drawnFeature?.ToString();
            }
            else
            {
                _editManager.EditMode = EditMode.AddPolygon; // Turn on drawing
                IsDrawingPolygon = true;
                StatusMessage = "Draw Polygon mode activated. Click on map to add vertices. Double-click to finish.";
            }
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && IsDrawingPolygon)
            {
                TogglePolygonEdit();
            }
        }

        private void OnMainDevicePvtUpdated(PvtDataD800 pvtData) // Assuming PvtDataD800 is in LiveTrackingMap
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentPvtData = pvtData;
                StatusMessage = $"Main Device PVT: Lat {pvtData.LatitudeDegrees:F5}, Lon {pvtData.LongitudeDegrees:F5}";

                if (pvtData.FixType >= 2 && pvtData.LatitudeDegrees >= -80 && pvtData.LatitudeDegrees <= 84)
                {
                    MPoint deviceMapPosition = SphericalMercator.FromLonLat(pvtData.LongitudeDegrees, pvtData.LatitudeDegrees).ToMPoint();
                    _mainDevicePositionFeature.Point.X = deviceMapPosition.X;
                    _mainDevicePositionFeature.Point.Y = deviceMapPosition.Y;
                    _mainDeviceLayer.DataHasChanged();

                    try
                    {
                        // Assuming CoordinateConverter is in LiveTrackingMap
                        CurrentUtmString = CoordinateTranformer.ToUtm(pvtData.LongitudeDegrees, pvtData.LatitudeDegrees).ToString();
                    }
                    catch { CurrentUtmString = "UTM Conv. Error"; }

                    // Center map on the first valid main device point, or if view is too zoomed out
                    var currentResolution = Map.Navigator.Viewport.Resolution;
                    if (_mainDevicePositionFeature.Point.X != 0 || _mainDevicePositionFeature.Point.Y != 0)
                    {
                        if (Map.Extent == null || currentResolution > 100) // Arbitrary threshold for "zoomed out"
                        {
                            Map.Navigator.CenterOnAndZoomTo(deviceMapPosition, Math.Min(currentResolution, 50), 500);
                        }
                        else if (!Map.Extent.Contains(_mainDevicePositionFeature.Point))
                        {
                            Map.Navigator.CenterOn(deviceMapPosition, 500); // Pan smoothly
                        }
                    }
                }
                else
                {
                    CurrentUtmString = "Invalid Fix or Lat/Lon OOB for UTM";
                }
            });
        }

        private Mapsui.Styles.Color GetRandomMapsuiColor()
        {
            return new Mapsui.Styles.Color(_mapsuiColorRandom.Next(200), _mapsuiColorRandom.Next(200), _mapsuiColorRandom.Next(200));
        }

        private Mapsui.Styles.Color GetDogColor(DogCollarData dogData) // Assuming DogCollarData is in LiveTrackingMap
        {
            // Using confirmed ColorCandidateByte21 (payload byte 21)
            switch (dogData.ColorCandidateByte21)
            {
                case 0x02: return Mapsui.Styles.Color.DarkGreen;
                case 0x03: return new Mapsui.Styles.Color(218, 165, 32); // Approx DarkGoldenrod for "Dark Yellow" / "Brown"
                case 0x0B: return Mapsui.Styles.Color.Yellow;    // From PC software observation for Hund3 (Iter3)
                // Add more mappings as you discover them for your device's palette
                // Example for red, if you find its byte value (e.g., 0x09)
                // case 0x09: return Mapsui.Styles.Color.Red;
                default: return GetRandomMapsuiColor();
            }
        }

        private void OnDogDataUpdated(DogCollarData dogData) // Assuming DogCollarData is in LiveTrackingMap
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(dogData.DogName)) return;

                StatusMessage = $"Dog '{dogData.DogName}' Update: Lat {dogData.LatitudeDegrees:F5}, Lon {dogData.LongitudeDegrees:F5}";

                if (!_dogTrackObjects.TryGetValue(dogData.ID, out var trackMapObject))
                {
                    Mapsui.Styles.Color trackColor = GetDogColor(dogData);

                    trackMapObject = new DogTrackMapObject(dogData.DogName, trackColor);
                    _dogTrackObjects[dogData.ID] = trackMapObject;


                    DisplayableDogTracks.Add(trackMapObject);

                    var features = (List<IFeature>)_dogTracksLayer.Features;
                    features.Add(trackMapObject.PathMapFeature);
                    features.Add(trackMapObject.LastPositionMarkerMapFeature);
                }

                trackMapObject.UpdateName(dogData.DogName);
                trackMapObject.AddPoint(dogData.LongitudeDegrees, dogData.LatitudeDegrees);
                _dogTracksLayer.DataHasChanged();
            });
        }
    }
}