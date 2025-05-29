using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LiveTrackingMap
{
        public class GarminUsbService : IDisposable
        {
            private readonly GarminUsbDevice _garminDevice;
            private CancellationTokenSource _cts;
            private bool _isProcessing = false;

            public event Action<PvtDataD800> MainDevicePvtUpdated;
            public event Action<DogCollarData> DogDataUpdated;
            // public event Action<List<TrackedEntityData>> MultiPersonDataUpdated; // If using packet 0x72
            public event Action<string> StatusMessageChanged;
            public event Action<bool> IsConnectedChanged; // To notify connection status

            public GarminUsbService()
            {
                // GarminPacketProcessor is not strictly needed if GarminUsbDevice now dispatches typed data
                _garminDevice = new GarminUsbDevice(null); // Pass null or a simplified processor

                // Configure GarminUsbDevice to call our handlers
                _garminDevice.SetPvtDataHandler(pvtData =>
                    Application.Current.Dispatcher.Invoke(() => MainDevicePvtUpdated?.Invoke(pvtData)));

                _garminDevice.SetDogCollarDataHandler(dogData =>
                    Application.Current.Dispatcher.Invoke(() => DogDataUpdated?.Invoke(dogData)));

                // If you had a multi-person handler for packet 0x72
                // _garminDevice.SetMultiPersonDataHandler(entities =>
                //    Application.Current.Dispatcher.Invoke(() => MultiPersonDataUpdated?.Invoke(entities)));

                _garminDevice.SetStatusMessageHandler(message =>
                    Application.Current.Dispatcher.Invoke(() => StatusMessageChanged?.Invoke(message)));

                _garminDevice.SetUsbProtocolLayerDataHandler(usbHeader =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (usbHeader.PacketType == 0 && usbHeader.ApplicationPacketID == NativeMethods.PID_SESSION_STARTED)
                        {
                            StatusMessageChanged?.Invoke("USB Session Started with device. Sending Start PVT command...");
                            // This command should ideally be sent after confirming session started
                            // and device is ready. GarminUsbDevice can manage this internally.
                            _garminDevice.SendStartPvtDataCommand();
                        }
                    });
                });
            }

            public async Task StartAsync()
            {
                if (_isProcessing) return;
                bool connectionSuccess = false;

                do
                {
                    _isProcessing = true;
                    _cts = new CancellationTokenSource();
                    StatusMessageChanged?.Invoke("Attempting to connect to Garmin device...");

                    await Task.Run(() =>
                    {
                        if (_garminDevice.Connect()) // Connect now also sends StartSession internally
                        {
                            Application.Current.Dispatcher.Invoke(() => IsConnectedChanged?.Invoke(true));
                            StatusMessageChanged?.Invoke("Device connected. Starting listener...");
                            connectionSuccess = true;
                            _garminDevice.StartListening();
                        }
                        else
                        {
                            StatusMessageChanged?.Invoke("Could not connect to Garmin device.");
                            Application.Current.Dispatcher.Invoke(() => IsConnectedChanged?.Invoke(false));
                            _isProcessing = false;
                            Thread.Sleep(500);
                        }
                    }, _cts.Token);
                }
                while (connectionSuccess == false);
            }

            public void Stop()
            {
                if (!_isProcessing) return;
                StatusMessageChanged?.Invoke("Stopping USB listening...");
                _cts?.Cancel();
                _garminDevice.StopListening(); // Ensure this call effectively stops the background thread
                Application.Current.Dispatcher.Invoke(() => IsConnectedChanged?.Invoke(false));
                _isProcessing = false;
                StatusMessageChanged?.Invoke("USB listening stopped.");
            }

            public void Dispose()
            {
                Stop();
                _garminDevice?.Dispose();
                _cts?.Dispose();
            }
        }
    }
