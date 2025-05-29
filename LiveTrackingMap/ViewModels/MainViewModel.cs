using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveTrackingMap.Models;
using LiveTrackingMap.Services;
using LiveTrackingMap.Features;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Mapsui.Extensions;
using Mapsui.Widgets;

namespace LiveTrackingMap.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly GarminUsbService _garminService;
        public Map Map { get; }

        private readonly MemoryLayer _mainDeviceLayer;
        private readonly PointFeature _mainDevicePositionFeature;

        private readonly MemoryLayer _dogTracksLayer;
        private readonly Dictionary<int, DogTrackMapObject> _dogTrackObjects = new Dictionary<int, DogTrackMapObject>();

        private readonly PolygonEditingManager _polygonEditingManager;
        private readonly WritableLayer _editablePolygonLayer;

        [ObservableProperty]
        private bool _isDrawingPolygon;

        [ObservableProperty]
        private string _statusMessage = "Ready. Click 'Start Listening'.";

        [ObservableProperty]
        private bool _isListening;

        [ObservableProperty]
        private PvtDataD800? _currentPvtData;

        [ObservableProperty]
        private string? _currentUtmString;

        private Random _mapsuiColorRandom = new Random();

        public ObservableCollection<DogTrackMapObject> DisplayableDogTracks { get; } = new ObservableCollection<DogTrackMapObject>();

        [ObservableProperty]
        private DogTrackMapObject? _selectedDogTrackForInfo;

        public IRelayCommand<DogTrackMapObject> ShowDogInfoCommand { get; }
        public IRelayCommand OpenDogInfoWindowCommand { get; }
        public IRelayCommand TogglePolygonEditCommand { get; }

        public MainViewModel()
        {
            Map = MapConfigurationService.CreateDefaultMap();

            _mainDevicePositionFeature = new PointFeature(0, 0) { ["Name"] = "Main Device" };
            _mainDevicePositionFeature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Triangle,
                SymbolScale = 1.0,
                Fill = new Brush(Color.Blue),
                Outline = new Pen(Color.White, 2)
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

            // Initialize Marl center position
            var marlCenter = SphericalMercator.FromLonLat(7.06933, 51.66255).ToMPoint();
            Map.Home = navigator => navigator.CenterOnAndZoomTo(marlCenter, 20);

            _garminService = new GarminUsbService();
            _garminService.StatusMessageChanged += GarminService_StatusMessageChanged;
            _garminService.IsConnectedChanged += OnIsConnectedChanged;
            _garminService.MainDevicePvtUpdated += OnMainDevicePvtUpdated;
            _garminService.DogDataUpdated += OnDogDataUpdated;

            _editablePolygonLayer = new WritableLayer
            {
                Style = MapConfigurationService.CreateEditLayerStyle(),
                IsMapInfoLayer = true
            };
            Map.Layers.Add(_editablePolygonLayer);

            _polygonEditingManager = new PolygonEditingManager(_editablePolygonLayer);

            StartListeningAsync();
            TogglePolygonEditCommand = new RelayCommand(TogglePolygonEdit);
            ShowDogInfoCommand = new RelayCommand<DogTrackMapObject>(ShowDogInfoWindow);
        }

        private void ShowDogInfoWindow(DogTrackMapObject? dogMapObject)
        {
            if (dogMapObject == null) return;

            DogInfoWindow infoWindow = new DogInfoWindow
            {
                DataContext = dogMapObject,
                Owner = Application.Current.MainWindow
            };
            infoWindow.Show();
        }

        private async Task StartListeningAsync()
        {
            StatusMessage = "Attempting to start USB listener...";
            Application.Current.Dispatcher.Invoke(() => IsListening = true);
            await _garminService.StartAsync();
        }

        private void StopListening()
        {
            _garminService.Stop();
        }

        public void InitializeEditing(IMapControl mapControl)
        {
            _polygonEditingManager.InitializeEditing(mapControl);
            TogglePolygonEditCommand.NotifyCanExecuteChanged();
            StatusMessage = "Editing tools initialized. Select a mode.";
        }

        private void TogglePolygonEdit()
        {
            _polygonEditingManager.TogglePolygonEdit();
            IsDrawingPolygon = _polygonEditingManager.IsDrawingPolygon;
            StatusMessage = IsDrawingPolygon ? 
                "Draw Polygon mode activated. Click on map to add vertices. Double-click to finish." : 
                "Draw Polygon mode deactivated.";
        }

        private void GarminService_StatusMessageChanged(string message)
        {
            Application.Current.Dispatcher.Invoke(() => StatusMessage = message);
        }

        private void OnIsConnectedChanged(bool connected)
        {
            Application.Current.Dispatcher.Invoke(() => IsListening = connected);
        }

        private void OnMainDevicePvtUpdated(PvtDataD800 pvtData)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentPvtData = pvtData;
                StatusMessage = $"Main Device PVT: Lat {pvtData.LatitudeDegrees:F5}, Lon {pvtData.LongitudeDegrees:F5}";

                if (pvtData.FixType >= 2 && pvtData.LatitudeDegrees >= -80 && pvtData.LatitudeDegrees <= 84)
                {
                    UpdateMainDevicePosition(pvtData);
                }
                else
                {
                    CurrentUtmString = "Invalid Fix or Lat/Lon OOB for UTM";
                }
            });
        }

        private void UpdateMainDevicePosition(PvtDataD800 pvtData)
        {
            MPoint deviceMapPosition = SphericalMercator.FromLonLat(
                pvtData.LongitudeDegrees, 
                pvtData.LatitudeDegrees).ToMPoint();

            _mainDevicePositionFeature.Point.X = deviceMapPosition.X;
            _mainDevicePositionFeature.Point.Y = deviceMapPosition.Y;
            _mainDeviceLayer.DataHasChanged();

            try
            {
                CurrentUtmString = CoordinateTranformer.ToUtm(
                    pvtData.LongitudeDegrees, 
                    pvtData.LatitudeDegrees).ToString();
            }
            catch 
            { 
                CurrentUtmString = "UTM Conv. Error"; 
            }

            UpdateMapViewIfNeeded(deviceMapPosition);
        }

        private void UpdateMapViewIfNeeded(MPoint deviceMapPosition)
        {
            var currentResolution = Map.Navigator.Viewport.Resolution;
            if (_mainDevicePositionFeature.Point.X != 0 || _mainDevicePositionFeature.Point.Y != 0)
            {
                if (Map.Extent == null || currentResolution > 100)
                {
                    Map.Navigator.CenterOnAndZoomTo(deviceMapPosition, Math.Min(currentResolution, 50), 500);
                }
                else if (!Map.Extent.Contains(_mainDevicePositionFeature.Point))
                {
                    Map.Navigator.CenterOn(deviceMapPosition, 500);
                }
            }
        }

        private void OnDogDataUpdated(DogCollarData dogData)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(dogData.DogName)) return;

                StatusMessage = $"Dog '{dogData.DogName}' Update: Lat {dogData.LatitudeDegrees:F5}, Lon {dogData.LongitudeDegrees:F5}";

                UpdateOrCreateDogTrack(dogData);
            });
        }

        private void UpdateOrCreateDogTrack(DogCollarData dogData)
        {
            if (!_dogTrackObjects.TryGetValue(dogData.ID, out var trackMapObject))
            {
                trackMapObject = CreateNewDogTrack(dogData);
            }

            trackMapObject.UpdateName(dogData.DogName);
            trackMapObject.AddPoint(dogData.LongitudeDegrees, dogData.LatitudeDegrees);
            _dogTracksLayer.DataHasChanged();
        }

        private DogTrackMapObject CreateNewDogTrack(DogCollarData dogData)
        {
            var trackColor = GetDogColor(dogData);
            var trackMapObject = new DogTrackMapObject(dogData.DogName, trackColor);
            
            _dogTrackObjects[dogData.ID] = trackMapObject;
            DisplayableDogTracks.Add(trackMapObject);

            var features = (List<IFeature>)_dogTracksLayer.Features;
            features.Add(trackMapObject.PathMapFeature);
            features.Add(trackMapObject.LastPositionMarkerMapFeature);

            return trackMapObject;
        }

        private Color GetDogColor(DogCollarData dogData)
        {
            return dogData.ColorCandidateByte21 switch
            {
                0x02 => Color.DarkGreen,
                0x03 => new Color(218, 165, 32),
                0x0B => Color.Yellow,
                _ => GetRandomMapsuiColor()
            };
        }

        private Color GetRandomMapsuiColor()
        {
            return new Color(
                _mapsuiColorRandom.Next(200), 
                _mapsuiColorRandom.Next(200), 
                _mapsuiColorRandom.Next(200));
        }
    }
}