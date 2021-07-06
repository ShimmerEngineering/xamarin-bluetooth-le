using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Acr.UserDialogs;
using BLE.Client.Extensions;
using MvvmCross.ViewModels;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Extensions;
using Plugin.Permissions.Abstractions;
using Plugin.Settings.Abstractions;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross;
using Xamarin.Forms;
using shimmer.Services;
using shimmer.Models;
using ShimmerAPI;
using shimmer.Sensors;
using static shimmer.Models.ShimmerBLEEventData;
using shimmer.Communications;
using ShimmerBLEAPI.Communications;
using ShimmerBLEAPI.Devices;
using System.IO;

namespace BLE.Client.ViewModels
{
    public class DeviceListViewModel : BaseViewModel, IObserver<String>//, INotifyPropertyChanged
    {
        private readonly IBluetoothLE _bluetoothLe;
        private readonly IUserDialogs _userDialogs;
        private readonly ISettings _settings;
        private Guid _previousGuid;
        private CancellationTokenSource _cancellationTokenSource;
        VerisenseBLEDevice VerisenseBLEDevice;

        public Guid PreviousGuid
        {
            get => _previousGuid;
            set
            {
                _previousGuid = value;
                _settings.AddOrUpdateValue("lastguid", _previousGuid.ToString());
                RaisePropertyChanged();
                RaisePropertyChanged(() => ConnectToPreviousCommand);
            }
        }
        DeviceManagerPluginBLE BLEManager;
        public MvxCommand RefreshCommand => new MvxCommand(() => TryStartScanning(true));
        public MvxCommand<DeviceListItemViewModel> DisconnectCommand => new MvxCommand<DeviceListItemViewModel>(DisconnectDevice);

        public MvxCommand<DeviceListItemViewModel> ConnectDisposeCommand => new MvxCommand<DeviceListItemViewModel>(ConnectAndDisposeDevice);
        public MvxCommand TestSpeedCommand => new MvxCommand(() => TestSpeed());
        public MvxCommand ConnectCommand => new MvxCommand(() => Connect());
        public MvxCommand DisconnectVRECommand => new MvxCommand(() => Disconnect());
        public MvxCommand ReadStatusCommand => new MvxCommand(() => ReadStatus());
        public MvxCommand ReadProdConfCommand => new MvxCommand(() => ReadProdConf());
        public MvxCommand ReadOpConfCommand => new MvxCommand(() => ReadOpConf());
        public MvxCommand WriteTimeCommand => new MvxCommand(() => WriteTime());
        public MvxCommand DownloadDataCommand => new MvxCommand(() => DownloadData());
        public MvxCommand StreamDataCommand => new MvxCommand(() => StreamData());
        public MvxCommand StopStreamCommand => new MvxCommand(() => StopStream());
        public MvxCommand PairCommand => new MvxCommand(() => PairDev());
        public MvxCommand EnableAcc2Gyro => new MvxCommand(() => EnableAccGyro());
        public MvxCommand DisableAcc2Gyro => new MvxCommand(() => DisableAccGyro());
        public MvxCommand EnableAcc => new MvxCommand(() => EnableAccel());
        public MvxCommand DisableAcc => new MvxCommand(() => DisableAccel());
        public MvxCommand ConfigureVerisenseDevice => new MvxCommand(() => ConfigureDevice());
        

        public ObservableCollection<DeviceListItemViewModel> Devices { get; set; } = new ObservableCollection<DeviceListItemViewModel>();
        public bool IsRefreshing => (Adapter != null) ? Adapter.IsScanning : false;
        public bool IsStateOn => _bluetoothLe.IsOn;
        public string StateText => GetStateText();
        public DeviceListItemViewModel SelectedDevice
        {
            get => null;
            set
            {
                if (value != null)
                {
                    HandleSelectedDevice(value);
                }

                RaisePropertyChanged();
            }
        }

        bool _useAutoConnect;
        public bool UseAutoConnect
        {
            get => _useAutoConnect;

            set
            {
                if (_useAutoConnect == value)
                    return;

                _useAutoConnect = value;
                RaisePropertyChanged();
            }
        }

        bool _deviceLogging;
        public bool DeviceLogging
        {
            get => _deviceLogging;

            set
            {
                if (_deviceLogging == value)
                    return;

                _deviceLogging = value;
                RaisePropertyChanged();
            }
        }

        bool _AccelEnabled;
        public bool SensorAccel
        {
            get => _AccelEnabled;

            set
            {
                if (_AccelEnabled == value)
                    return;

                _AccelEnabled = value;
                RaisePropertyChanged();
            }
        }

        bool _Accel2Enabled;
        public bool SensorAccel2
        {
            get => _Accel2Enabled;

            set
            {
                if (_Accel2Enabled == value)
                    return;

                _Accel2Enabled = value;
                RaisePropertyChanged();
            }
        }

        bool _GyroEnabled;
        public bool SensorGyro
        {
            get => _GyroEnabled;

            set
            {
                if (_GyroEnabled == value)
                    return;

                _GyroEnabled = value;
                RaisePropertyChanged();
            }
        }

        public MvxCommand StopScanCommand => new MvxCommand(() =>
        {
            _cancellationTokenSource.Cancel();
            CleanupCancellationToken();
            RaisePropertyChanged(() => IsRefreshing);
        }, () => _cancellationTokenSource != null);

        readonly IPermissions _permissions;

        public List<ScanMode> ScanModes => Enum.GetValues(typeof(ScanMode)).Cast<ScanMode>().ToList();

        public ScanMode SelectedScanMode
        {
            get => Adapter.ScanMode;
            set => Adapter.ScanMode = value;
        }
        Logging Logging;
        public DeviceListViewModel(IBluetoothLE bluetoothLe, IAdapter adapter, IUserDialogs userDialogs, ISettings settings, IPermissions permissions) : base(adapter)
        {
            _permissions = permissions;
            _bluetoothLe = bluetoothLe;
            _userDialogs = userDialogs;
            _settings = settings;
            // quick and dirty :>
            BLEManager = new DeviceManagerPluginBLE();

            _bluetoothLe.StateChanged += OnStateChanged;
            Adapter.DeviceDiscovered += OnDeviceDiscovered;
            Adapter.DeviceAdvertised += OnDeviceDiscovered;
            Adapter.ScanTimeoutElapsed += Adapter_ScanTimeoutElapsed;
            Adapter.DeviceDisconnected += OnDeviceDisconnected;
            Adapter.DeviceConnectionLost += OnDeviceConnectionLost;
            //Adapter.DeviceConnected += (sender, e) => Adapter.DisconnectDeviceAsync(e.Device);
             

            Adapter.ScanMode = ScanMode.LowLatency;

        }
        void OnCheckBoxCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            // Perform required operation after examining e.Value
            System.Console.WriteLine();
        }
        private Task GetPreviousGuidAsync()
        {
            return Task.Run(() =>
            {
                var guidString = _settings.GetValueOrDefault("lastguid", string.Empty);
                PreviousGuid = !string.IsNullOrEmpty(guidString) ? Guid.Parse(guidString) : Guid.Empty;
            });
        }

        private void OnDeviceConnectionLost(object sender, DeviceErrorEventArgs e)
        {
            Devices.FirstOrDefault(d => d.Id == e.Device.Id)?.Update();

            _userDialogs.HideLoading();
            _userDialogs.ErrorToast("Error", $"Connection LOST {e.Device.Name}", TimeSpan.FromMilliseconds(6000));
        }

        private void OnStateChanged(object sender, BluetoothStateChangedArgs e)
        {
            RaisePropertyChanged(nameof(IsStateOn));
            RaisePropertyChanged(nameof(StateText));
            //TryStartScanning();
        }

        private string GetStateText()
        {
            switch (_bluetoothLe.State)
            {
                case BluetoothState.Unknown:
                    return "Unknown BLE state.";
                case BluetoothState.Unavailable:
                    return "BLE is not available on this device.";
                case BluetoothState.Unauthorized:
                    return "You are not allowed to use BLE.";
                case BluetoothState.TurningOn:
                    return "BLE is warming up, please wait.";
                case BluetoothState.On:
                    return "BLE is on.";
                case BluetoothState.TurningOff:
                    return "BLE is turning off. That's sad!";
                case BluetoothState.Off:
                    return "BLE is off. Turn it on!";
                default:
                    return "Unknown BLE state.";
            }
        }

        private void Adapter_ScanTimeoutElapsed(object sender, EventArgs e)
        {
            RaisePropertyChanged(() => IsRefreshing);

            CleanupCancellationToken();
        }

        private void OnDeviceDiscovered(object sender, DeviceEventArgs args)
        {
            AddOrUpdateDevice(args.Device);
        }

        private void AddOrUpdateDevice(IDevice device)
        {
            InvokeOnMainThread(() =>
            {
                var vm = Devices.FirstOrDefault(d => d.Device.Id == device.Id);
                if (vm != null)
                {
                    vm.Update();
                }
                else
                {
                    Devices.Add(new DeviceListItemViewModel(device));
                }
            });
        }

        public override async void ViewAppeared()
        {
            base.ViewAppeared();

            await GetPreviousGuidAsync();
            //TryStartScanning();

            GetSystemConnectedOrPairedDevices();

        }

        private void GetSystemConnectedOrPairedDevices()
        {
            try
            {
                //heart rate
                var guid = Guid.Parse("0000180d-0000-1000-8000-00805f9b34fb");

                // SystemDevices = Adapter.GetSystemConnectedOrPairedDevices(new[] { guid }).Select(d => new DeviceListItemViewModel(d)).ToList();
                // remove the GUID filter for test
                // Avoid to loose already IDevice with a connection, otherwise you can't close it
                // Keep the reference of already known devices and drop all not in returned list.
                var pairedOrConnectedDeviceWithNullGatt = Adapter.GetSystemConnectedOrPairedDevices();
                SystemDevices.RemoveAll(sd => !pairedOrConnectedDeviceWithNullGatt.Any(p => p.Id == sd.Id));
                SystemDevices.AddRange(pairedOrConnectedDeviceWithNullGatt.Where(d => !SystemDevices.Any(sd => sd.Id == d.Id)).Select(d => new DeviceListItemViewModel(d)));
                RaisePropertyChanged(() => SystemDevices);
            }
            catch (Exception ex)
            {
                Trace.Message("Failed to retreive system connected devices. {0}", ex.Message);
            }
        }

        public List<DeviceListItemViewModel> SystemDevices { get; private set; } = new List<DeviceListItemViewModel>();

        public override void ViewDisappeared()
        {
            base.ViewDisappeared();

            Adapter.StopScanningForDevicesAsync();
            RaisePropertyChanged(() => IsRefreshing);
        }

        private async void TryStartScanning(bool refresh = false)
        {
            if (Xamarin.Forms.Device.RuntimePlatform == Device.Android)
            {
                var status = await _permissions.CheckPermissionStatusAsync(Permission.Location);
                if (status != PermissionStatus.Granted)
                {
                    var permissionResult = await _permissions.RequestPermissionsAsync(Permission.Location);

                    if (permissionResult.First().Value != PermissionStatus.Granted)
                    {
                        await _userDialogs.AlertAsync("Permission denied. Not scanning.");
                        _permissions.OpenAppSettings();
                        return;
                    }
                }
            }

            if (IsStateOn && (refresh || !Devices.Any()) && !IsRefreshing)
            {
                ScanForDevices();
            }
        }

        private async void ScanForDevices()
        {
            Devices.Clear();

            foreach (var connectedDevice in Adapter.ConnectedDevices)
            {
                //update rssi for already connected evices (so tha 0 is not shown in the list)
                try
                {
                    await connectedDevice.UpdateRssiAsync();
                }
                catch (Exception ex)
                {
                    Trace.Message(ex.Message);
                    await _userDialogs.AlertAsync($"Failed to update RSSI for {connectedDevice.Name}");
                }

                AddOrUpdateDevice(connectedDevice);
            }

            _cancellationTokenSource = new CancellationTokenSource();
            await RaisePropertyChanged(() => StopScanCommand);

            await RaisePropertyChanged(() => IsRefreshing);
            Adapter.ScanMode = ScanMode.LowLatency;
            //await Adapter.StartScanningForDevicesAsync(_cancellationTokenSource.Token);
            BLEManager.StartScanForDevices();
        }

        private void CleanupCancellationToken()
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            RaisePropertyChanged(() => StopScanCommand);
        }

        private async void DisconnectDevice(DeviceListItemViewModel device)
        {
            try
            {
                if (!device.IsConnected)
                    return;

                _userDialogs.ShowLoading($"Disconnecting {device.Name}...");

                await Adapter.DisconnectDeviceAsync(device.Device);
            }
            catch (Exception ex)
            {
                await _userDialogs.AlertAsync(ex.Message, "Disconnect error");
            }
            finally
            {
                device.Update();
                _userDialogs.HideLoading();
            }
        }

        private void HandleSelectedDevice(DeviceListItemViewModel device)
        {
            var config = new ActionSheetConfig();

            if (device.IsConnected)
            {
                config.Add("Update RSSI", async () =>
                {
                    try
                    {
                        _userDialogs.ShowLoading();

                        await device.Device.UpdateRssiAsync();
                        await device.RaisePropertyChanged(nameof(device.Rssi));

                        _userDialogs.HideLoading();

                        _userDialogs.Toast($"RSSI updated {device.Rssi}", TimeSpan.FromSeconds(1));
                    }
                    catch (Exception ex)
                    {
                        _userDialogs.HideLoading();
                        await _userDialogs.AlertAsync($"Failed to update rssi. Exception: {ex.Message}");
                    }
                });

                config.Add("Show Services", async () =>
                {
                    await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<ServiceListViewModel, MvxBundle>(new MvxBundle(new Dictionary<string, string> { { DeviceIdKey, device.Device.Id.ToString() } }));
                });

                config.Destructive = new ActionSheetOption("Disconnect", () => DisconnectCommand.Execute(device));
            }
            else
            {
                config.Add("Connect", async () =>
                {
                    if (await ConnectDeviceAsync(device))
                    {
                        var navigation = Mvx.IoCProvider.Resolve<IMvxNavigationService>();
                        await navigation.Navigate<ServiceListViewModel, MvxBundle>(new MvxBundle(new Dictionary<string, string> { { DeviceIdKey, device.Device.Id.ToString() } }));
                    }
                });

                config.Add("Connect & Dispose", () => ConnectDisposeCommand.Execute(device));
            }

            config.Add("Copy GUID", () => CopyGuidCommand.Execute(device));
            config.Cancel = new ActionSheetOption("Cancel");
            config.SetTitle("Device Options");
            _userDialogs.ActionSheet(config);
        }

        private async Task<bool> ConnectDeviceAsync(DeviceListItemViewModel device, bool showPrompt = true)
        {
            if (showPrompt && !await _userDialogs.ConfirmAsync($"Connect to device '{device.Name}'?"))
            {
                return false;
            }

            CancellationTokenSource tokenSource = new CancellationTokenSource();
            try
            {
                var config = new ProgressDialogConfig()
                {
                    Title = $"Connecting to '{device.Id}'",
                    CancelText = "Cancel",
                    IsDeterministic = false,
                    OnCancel = tokenSource.Cancel
                };

                using (var progress = _userDialogs.Progress(config))
                {
                    progress.Show();

                    await Adapter.ConnectToDeviceAsync(device.Device, new ConnectParameters(autoConnect: UseAutoConnect, forceBleTransport: true), tokenSource.Token);
                }

                await _userDialogs.AlertAsync($"Connected to {device.Device.Name}.");

                PreviousGuid = device.Device.Id;
                return true;

            }
            catch (Exception ex)
            {
                await _userDialogs.AlertAsync(ex.Message, "Connection error");
                Trace.Message(ex.Message);
                return false;
            }
            finally
            {
                _userDialogs.HideLoading();
                device.Update();
                tokenSource.Dispose();
                tokenSource = null;
            }
        }


        public MvxCommand ConnectToPreviousCommand => new MvxCommand(ConnectToPreviousDeviceAsync, CanConnectToPrevious);

        private async void ConnectToPreviousDeviceAsync()
        {
            IDevice device;
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            try
            {
                var config = new ProgressDialogConfig()
                {
                    Title = $"Searching for '{PreviousGuid}'",
                    CancelText = "Cancel",
                    IsDeterministic = false,
                    OnCancel = tokenSource.Cancel
                };

                using (var progress = _userDialogs.Progress(config))
                {
                    progress.Show();

                    device = await Adapter.ConnectToKnownDeviceAsync(PreviousGuid, new ConnectParameters(autoConnect: UseAutoConnect, forceBleTransport: false), tokenSource.Token);

                }

                await _userDialogs.AlertAsync($"Connected to {device.Name}.");

                var deviceItem = Devices.FirstOrDefault(d => d.Device.Id == device.Id);
                if (deviceItem == null)
                {
                    deviceItem = new DeviceListItemViewModel(device);
                    Devices.Add(deviceItem);
                }
                else
                {
                    deviceItem.Update(device);
                }
            }
            catch (Exception ex)
            {
                _userDialogs.ErrorToast(string.Empty, ex.Message, TimeSpan.FromSeconds(5));
                return;
            }
            finally
            {
                tokenSource.Dispose();
                tokenSource = null;
            }
        }

        private bool CanConnectToPrevious()
        {
            return PreviousGuid != default;
        }

        private async void ConnectAndDisposeDevice(DeviceListItemViewModel item)
        {
            try
            {
                using (item.Device)
                {
                    _userDialogs.ShowLoading($"Connecting to {item.Name} ...");
                    await Adapter.ConnectToDeviceAsync(item.Device);

                    // TODO make this configurable
                    var resultMTU = await item.Device.RequestMtuAsync(60);
                    System.Diagnostics.Debug.WriteLine($"Requested MTU. Result is {resultMTU}");

                    // TODO make this configurable
                    var resultInterval = item.Device.UpdateConnectionInterval(ConnectionInterval.High);
                    System.Diagnostics.Debug.WriteLine($"Set Connection Interval. Result is {resultInterval}");

                    item.Update();
                    await _userDialogs.AlertAsync($"Connected {item.Device.Name}");

                    _userDialogs.HideLoading();
                    for (var i = 5; i >= 1; i--)
                    {
                        _userDialogs.ShowLoading($"Disconnect in {i}s...");

                        await Task.Delay(1000);

                        _userDialogs.HideLoading();
                    }
                }
            }
            catch (Exception ex)
            {
                await _userDialogs.AlertAsync(ex.Message, "Failed to connect and dispose.");
            }
            finally
            {
                _userDialogs.HideLoading();
            }


        }

        private void OnDeviceDisconnected(object sender, DeviceEventArgs e)
        {
            Devices.FirstOrDefault(d => d.Id == e.Device.Id)?.Update();
            _userDialogs.HideLoading();
            _userDialogs.Toast($"Disconnected {e.Device.Name}", TimeSpan.FromSeconds(3));

            Console.WriteLine($"Disconnected {e.Device.Name}");
        }

        public MvxCommand<DeviceListItemViewModel> CopyGuidCommand => new MvxCommand<DeviceListItemViewModel>(device =>
        {
            PreviousGuid = device.Id;
        });

        

        private void ShimmerDevice_BLEEvent(object sender, ShimmerBLEEventData e)
        {
            
            if (e.CurrentEvent == VerisenseBLEEvent.NewDataPacket)
            {
                ObjectCluster ojc = ((ObjectCluster)e.ObjMsg);
                if (ojc.GetNames().Contains(SensorLIS2DW12.ObjectClusterSensorName.LIS2DW12_ACC_X))
                {
                    if (Logging == null)
                    {
                        var folder = Path.Combine(DependencyService.Get<ILocalFolderService>().GetBinFileDirectory());
                        if (!Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                        }
                        DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        double time = (DateTime.UtcNow - UnixEpoch).TotalMilliseconds;
                        Logging = new Logging(Path.Combine(folder, time.ToString() + "SensorLIS2DW12.csv"), ",");
                    }
                    Logging.WriteData(ojc);
                    var a2x = ojc.GetData(SensorLIS2DW12.ObjectClusterSensorName.LIS2DW12_ACC_X, ShimmerConfiguration.SignalFormats.CAL);
                    var a2y = ojc.GetData(SensorLIS2DW12.ObjectClusterSensorName.LIS2DW12_ACC_Y, ShimmerConfiguration.SignalFormats.CAL);
                    var a2z = ojc.GetData(SensorLIS2DW12.ObjectClusterSensorName.LIS2DW12_ACC_Z, ShimmerConfiguration.SignalFormats.CAL);
                    System.Console.WriteLine("New Data Packet: " + "  X : " + a2x.Data + "  Y : " + a2y.Data + "  Z : " + a2z.Data);
                    DeviceMessage = SensorLIS2DW12.SensorName + " New Data Packet: " + "  X : " + Math.Round(a2x.Data, 2) + "  Y : " + Math.Round(a2y.Data, 2) + "  Z : " + Math.Round(a2z.Data, 2);
                }
                if (ojc.GetNames().Contains(SensorLSM6DS3.ObjectClusterSensorName.LSM6DS3_ACC_X)
                    || ojc.GetNames().Contains(SensorLSM6DS3.ObjectClusterSensorName.LSM6DS3_GYRO_X))
                {
                    if (Logging == null)
                    {
                        var folder = Path.Combine(DependencyService.Get<ILocalFolderService>().GetBinFileDirectory());
                        if (!Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                        }
                        DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        double time = (DateTime.UtcNow - UnixEpoch).TotalMilliseconds;
                        Logging = new Logging(Path.Combine(folder, time.ToString() + "SensorLIS2DW12.csv"), ",");
                    }

                    Logging.WriteData(ojc);
                    DeviceMessage = SensorLIS2DW12.SensorName + " New Data Packet: ";
                    if (ojc.GetNames().Contains(SensorLSM6DS3.ObjectClusterSensorName.LSM6DS3_ACC_X)){
                        var a2x = ojc.GetData(SensorLSM6DS3.ObjectClusterSensorName.LSM6DS3_ACC_X, ShimmerConfiguration.SignalFormats.CAL);
                        var a2y = ojc.GetData(SensorLSM6DS3.ObjectClusterSensorName.LSM6DS3_ACC_Y, ShimmerConfiguration.SignalFormats.CAL);
                        var a2z = ojc.GetData(SensorLSM6DS3.ObjectClusterSensorName.LSM6DS3_ACC_Z, ShimmerConfiguration.SignalFormats.CAL);
                        System.Console.WriteLine("New Data Packet: " + "  X : " + a2x.Data + "  Y : " + a2y.Data + "  Z : " + a2z.Data);
                        DeviceMessage = " ; ACCEL =  X : " + Math.Round(a2x.Data, 2) + "  Y : " + Math.Round(a2y.Data, 2) + "  Z : " + Math.Round(a2z.Data, 2);
                    }
                    if (ojc.GetNames().Contains(SensorLSM6DS3.ObjectClusterSensorName.LSM6DS3_GYRO_X)){
                        var g2x = ojc.GetData(SensorLSM6DS3.ObjectClusterSensorName.LSM6DS3_GYRO_X, ShimmerConfiguration.SignalFormats.CAL);
                        var g2y = ojc.GetData(SensorLSM6DS3.ObjectClusterSensorName.LSM6DS3_GYRO_Y, ShimmerConfiguration.SignalFormats.CAL);
                        var g2z = ojc.GetData(SensorLSM6DS3.ObjectClusterSensorName.LSM6DS3_GYRO_Z, ShimmerConfiguration.SignalFormats.CAL);
                        System.Console.WriteLine("New Data Packet: " + "  X : " + g2x.Data + "  Y : " + g2y.Data + "  Z : " + g2z.Data);
                        DeviceMessage = DeviceMessage + " ; Gyro = X : " + Math.Round(g2x.Data, 2) + "  Y : " + Math.Round(g2y.Data, 2) + "  Z : " + Math.Round(g2z.Data, 2);
                    }
                }
            } else if (e.CurrentEvent== VerisenseBLEEvent.StateChange)
            {
                System.Console.WriteLine("SHIMMER DEVICE BLE EVENT: " + VerisenseBLEDevice.GetVerisenseBLEState().ToString());
                DeviceState = "Device State: " + VerisenseBLEDevice.GetVerisenseBLEState().ToString();
                if (VerisenseBLEDevice.GetVerisenseBLEState().Equals(ShimmerDeviceBluetoothState.Connected))
                {
                    DeviceLogging = VerisenseBLEDevice.isLoggingEnabled();
                    SensorAccel = ((SensorLIS2DW12)VerisenseBLEDevice.GetSensor(SensorLIS2DW12.SensorName)).IsAccelEnabled();
                    SensorAccel2 = ((SensorLSM6DS3)VerisenseBLEDevice.GetSensor(SensorLSM6DS3.SensorName)).IsAccelEnabled();
                    SensorGyro = ((SensorLSM6DS3)VerisenseBLEDevice.GetSensor(SensorLSM6DS3.SensorName)).IsGyroEnabled();
                }
            } else if (e.CurrentEvent == VerisenseBLEEvent.SyncLoggedDataNewPayload)
            {
                DeviceMessage = VerisenseBLEDevice.dataFilePath + " : " + e.Message;
            } else if (e.CurrentEvent == VerisenseBLEEvent.SyncLoggedDataComplete)
            {
                DeviceMessage = VerisenseBLEDevice.dataFilePath + " : " + e.CurrentEvent.ToString();
            } else if (e.CurrentEvent == VerisenseBLEEvent.RequestResponse)
            {
                if ((RequestType)e.ObjMsg == RequestType.Status) {
                    DeviceMessage = "Battery % =" + VerisenseBLEDevice.GetStatus().BatteryPercent + " UsbPowered =" + VerisenseBLEDevice.GetStatus().UsbPowered;
                } else if ((RequestType)e.ObjMsg == RequestType.ProductionConfig)
                {
                    DeviceMessage = "Hardware Version =v" + VerisenseBLEDevice.GetProductionConfig().REV_HW_MAJOR + "." + VerisenseBLEDevice.GetProductionConfig().REV_HW_MINOR + " Firmware Version =v" + VerisenseBLEDevice.GetProductionConfig().REV_FW_MAJOR + "." + VerisenseBLEDevice.GetProductionConfig().REV_FW_MINOR + "." + VerisenseBLEDevice.GetProductionConfig().REV_FW_INTERNAL;
                } else if ((RequestType)e.ObjMsg == RequestType.OperationalConfigRead)
                {
                    DeviceMessage = "Operational Config Received";
                }
            }
        }

        protected async void Connect()
        {
            if (VerisenseBLEDevice!=null)
            {
                VerisenseBLEDevice.ShimmerBLEEvent -= ShimmerDevice_BLEEvent;
            }
            VerisenseBLEDevice = new VerisenseBLEDevice(PreviousGuid.ToString(), "SensorName", shimmer.Models.CommunicationState.CommunicationMode.ForceDataTransferSync);
            VerisenseBLEDevice.ShimmerBLEEvent += ShimmerDevice_BLEEvent;
            VerisenseBLEDevice.Connect(true);

        }
        protected async void Disconnect()
        {
            VerisenseBLEDevice.Disconnect();
        }
        protected async void ReadStatus()
        {
            VerisenseBLEDevice.ExecuteRequest(RequestType.Status);
        }

        protected async void ReadProdConf()
        {
            VerisenseBLEDevice.ExecuteRequest(RequestType.ProductionConfig);
        }

        protected async void ReadOpConf()
        {
            VerisenseBLEDevice.ExecuteRequest(RequestType.OperationalConfigRead);
        }


        protected async void WriteTime()
        {
            VerisenseBLEDevice.ExecuteRequest(RequestType.RTCWrite);
        }
        protected async void ConfigureDevice()
        {
            var clone = new VerisenseBLEDevice(VerisenseBLEDevice);
            clone.setLoggingEnabled(_deviceLogging);
            if (SensorAccel)
            {
                ((SensorLIS2DW12)clone.GetSensor(SensorLIS2DW12.SensorName)).SetAccelEnabled(true);
            } else
            {
                ((SensorLIS2DW12)clone.GetSensor(SensorLIS2DW12.SensorName)).SetAccelEnabled(false);
            }
            if (SensorAccel2)
            {
                ((SensorLSM6DS3)clone.GetSensor(SensorLSM6DS3.SensorName)).SetAccelEnabled(true);
            } else
            {
                ((SensorLSM6DS3)clone.GetSensor(SensorLSM6DS3.SensorName)).SetAccelEnabled(false);
            }
            if (SensorGyro)
            {
                ((SensorLSM6DS3)clone.GetSensor(SensorLSM6DS3.SensorName)).SetGyroEnabled(true);
            } else
            {
                ((SensorLSM6DS3)clone.GetSensor(SensorLSM6DS3.SensorName)).SetGyroEnabled(true);
            }
            var opconfigbytes = clone.GenerateConfigurationBytes();
            var compare = VerisenseBLEDevice.GetOperationalConfigByteArray(); //make sure the byte values havent changed
            VerisenseBLEDevice.ExecuteRequest(RequestType.OperationalConfigWrite, opconfigbytes);
        }
        protected async void EnableAccGyro()
        {
            var clone = new VerisenseBLEDevice(VerisenseBLEDevice);
            var sensor = clone.GetSensor("Accel2");
            ((SensorLSM6DS3)sensor).SetAccelEnabled(true);
            ((SensorLSM6DS3)sensor).SetGyroEnabled(true);
            //((SensorLIS2DW12)sensor).Enabled = false;
            var opconfigbytes = clone.GenerateConfigurationBytes();
            var compare = VerisenseBLEDevice.GetOperationalConfigByteArray(); //make sure the byte values havent changed
            VerisenseBLEDevice.ExecuteRequest(RequestType.OperationalConfigWrite, opconfigbytes);
        }
  
        protected async void DisableAccGyro()
        {
            var clone = new VerisenseBLEDevice(VerisenseBLEDevice);
            var sensor = clone.GetSensor("Accel2");
            ((SensorLSM6DS3)sensor).SetAccelEnabled(false);
            ((SensorLSM6DS3)sensor).SetGyroEnabled(false);
            //((SensorLIS2DW12)sensor).Enabled = false;
            var opconfigbytes = clone.GenerateConfigurationBytes();
            var compare = VerisenseBLEDevice.GetOperationalConfigByteArray(); //make sure the byte values havent changed
            VerisenseBLEDevice.ExecuteRequest(RequestType.OperationalConfigWrite, opconfigbytes);
        }

        protected async void EnableAccel()
        {
            var clone = new VerisenseBLEDevice(VerisenseBLEDevice);
            var sensor = clone.GetSensor("Accel1");
            //((SensorLSM6DS3)sensor).Gyro_Enabled = true;
            //((SensorLSM6DS3)sensor).Accel2_Enabled = true;
            ((SensorLIS2DW12)sensor).SetAccelEnabled(true);
            var opconfigbytes = clone.GenerateConfigurationBytes();
            var compare = VerisenseBLEDevice.GetOperationalConfigByteArray(); //make sure the byte values havent changed
            VerisenseBLEDevice.ExecuteRequest(RequestType.OperationalConfigWrite, opconfigbytes);
        }

        protected async void DisableAccel()
        {
            var clone = new VerisenseBLEDevice(VerisenseBLEDevice);
            var sensor = clone.GetSensor("Accel1");
            //(SensorLSM6DS3)sensor).Gyro_Enabled = false;
            //((SensorLSM6DS3)sensor).Accel2_Enabled = false;
            ((SensorLIS2DW12)sensor).SetAccelEnabled(false);
            var opconfigbytes = clone.GenerateConfigurationBytes();
            var compare = VerisenseBLEDevice.GetOperationalConfigByteArray(); //make sure the byte values havent changed
            VerisenseBLEDevice.ExecuteRequest(RequestType.OperationalConfigWrite, opconfigbytes);
        }

        protected async void DownloadData()
        {
            /*
            ForegroundSyncService serv = new ForegroundSyncService(PreviousGuid.ToString(), "SensorName", shimmer.Models.CommunicationState.CommunicationMode.ForceDataTransferSync);
            serv.ShimmerBLEEvent += ShimmerDevice_BLEEvent;
            bool success = await serv.GetKnownDevice();
            if (success)
            {
                var data = await serv.ExecuteDataRequest();
            }
            */
            var data = await VerisenseBLEDevice.ExecuteRequest(RequestType.DataSync);
           
        }
        protected async void StreamData()
        {

            //var data = await SyncService.ExecuteStreamRequest();
            var data = VerisenseBLEDevice.ExecuteRequest(RequestType.StartStreaming);

        }

        protected async void PairDev()
        {
            //var service = DependencyService.Get<IVerisenseBLEPairing>();
            var service = DependencyService.Get<IVerisenseBLEManager>();
            service.PairVerisenseDevice("");
        }
        protected async void StopStream()
        {
            //SyncService.SendStopStreamRequestCommandOnMainThread();
            var data = VerisenseBLEDevice.ExecuteRequest(RequestType.StopStreaming);
            if (Logging != null)
            {
                Logging.CloseFile();
            }
            Logging = null;
        }
        protected async void TestSpeed()
        {
            SpeedTestService serv = new SpeedTestService(PreviousGuid.ToString());
            serv.Subscribe(this);
            await serv.GetKnownDevice();
            if (serv.ConnectedASM != null)
            {
                System.Console.WriteLine("Memory Lookup Execution");
                await serv.ExecuteMemoryLookupTableCommand();
            }
            else
            {
                System.Console.WriteLine("Connect Fail");
            }
        }

        string displayText = "1) Pair Verisense Sensor to PC first! 2) Scan, select sensor and choose copy GUID, 3) Start Speed Test or Data Sync";
        public string DeviceMessage
        {
            protected set
            {
                if (displayText != value)
                {
                    displayText = value;
                    RaisePropertyChanged();
                }
            }
            get { return displayText; }
        }
        string deviceState = "Device State: Unknown";
        public string DeviceState
        {
            protected set
            {
                if (deviceState != value)
                {
                    deviceState = value;
                    RaisePropertyChanged();
                }
            }
            get { return deviceState; }
        }
        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(string value)
        {
            Trace.Message("Works" + value);
            DeviceMessage = value;
        }
    }
}