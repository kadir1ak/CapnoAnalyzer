using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Diagnostics;
using System.Threading.Tasks;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.Services;
using System.Collections.Concurrent;
namespace CapnoAnalyzer.ViewModels.DeviceViewModels
{
    public class DevicesViewModel : BindableBase
    {
        private readonly SerialPortsManager _manager;

        private CancellationTokenSource _updateInterfaceLoopCancellationTokenSource;
        private readonly object _InterfaceDataLock = new();
        private int UpdateTimeMillisecond = 100;  // 10 Hz (100ms)

        // -- 1) Bağlı Cihazların Listesi --
        public ObservableCollection<Device> ConnectedDevices { get; } = new ObservableCollection<Device>();

        // -- 2) Manager'dan gelen port listeleri --
        public ObservableCollection<SerialPort> ConnectedPorts => _manager.ConnectedPorts;
        public ObservableCollection<string> AvailablePorts => _manager.AvailablePorts;

        // -- 3) Seçili Cihaz Nesnesi --
        private Device _device;
        public Device Device
        {
            get => _device;
            set
            {
                SetProperty(ref _device, value);
                // Command'ların tekrar CanExecute kontrolü yapmasını sağlamak için:
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // -- 4) UI’daki ComboBox’tan seçilen “PortName” --
        private string _selectedPortName;
        public string SelectedPortName
        {
            get => _selectedPortName;
            set
            {
                SetProperty(ref _selectedPortName, value);
                // Port adı değişince Connect butonunu aktif/pasif güncelle:
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // -- 5) Komutlar (Connect/Disconnect/SendMessage) --
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }

        // -- 6) Yapıcı Metot --
        public DevicesViewModel()
        {
            _manager = new SerialPortsManager();
            _manager.MessageReceived += OnMessageReceived;

            ConnectCommand = new RelayCommand(ExecuteConnect, CanExecuteConnect);
            DisconnectCommand = new RelayCommand(ExecuteDisconnect, CanExecuteDisconnect);

            // UI Güncelleme Döngüsünü Başlat
            StartUpdateInterfaceDataLoop();
        }
        private async Task UpdateInterfaceDataLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(UpdateTimeMillisecond, token);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var device in ConnectedDevices)
                        {
                            if (device.Messages.LastOrDefault()?.IncomingMessage == null)
                            {
                                continue;
                            }
                            if (device != null)
                            {
                                var newMessage = new DeviceMessage
                                {
                                    IncomingMessageIndex = device.Messages.Count,
                                    IncomingMessage = device.Messages.LastOrDefault()?.IncomingMessage
                                };
                                device.Interface.Messages.Add(newMessage);
                                DeviceIdentification(device);
                            }
                        }

                        // UI'yi yeniden tetikle
                        RaisePropertyChanged(nameof(ConnectedDevices));
                        RaisePropertyChanged(nameof(ConnectedPorts));
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Interface update loop error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void StartUpdateInterfaceDataLoop()
        {
            StopUpdateInterfaceDataLoop(); // Eski döngüyü durdur
            _updateInterfaceLoopCancellationTokenSource = new CancellationTokenSource();
            var token = _updateInterfaceLoopCancellationTokenSource.Token;
            _ = UpdateInterfaceDataLoop(token);
        }

        public void StopUpdateInterfaceDataLoop()
        {
            if (_updateInterfaceLoopCancellationTokenSource != null && !_updateInterfaceLoopCancellationTokenSource.IsCancellationRequested)
            {
                _updateInterfaceLoopCancellationTokenSource.Cancel();
                _updateInterfaceLoopCancellationTokenSource.Dispose();
                _updateInterfaceLoopCancellationTokenSource = null;
            }
        }

        // ========== Gelen Veri Yakalama (Event) ==========
        private void OnMessageReceived(string portName, string data)
        {
            // Mesajları UI Thread üzerinde eklemek için:
            Application.Current.Dispatcher.Invoke(() =>
            {
                var device = ConnectedDevices.FirstOrDefault(d => d.PortName == portName);
                if (device != null)
                {
                    var newMessage = new DeviceMessage
                    {
                        IncomingMessageIndex = device.Messages.Count,
                        IncomingMessage = data
                    };
                    device.Messages.Add(newMessage);
                    CalculateSampleRate(device);
                }
            });
        }
        private void CalculateSampleRate(Device device)
        {
            device.sampleCount++;
            var now = DateTime.Now;
            var elapsed = now - device.lastUpdate;

            if (elapsed.TotalSeconds >= 1) // Her saniyede bir hesapla
            {
                device.DataSamplingFrequency = device.sampleCount;
                device.sampleCount = 0;
                device.lastUpdate = now;
            }
        }
        private void DeviceIdentification(Device device)
        {
            try
            {
                if (device.DeviceStatus == DeviceStatus.Identified)
                    return;

                // Null kontrolü
                var lastMessage = device.Messages.LastOrDefault();
                if (lastMessage == null || string.IsNullOrWhiteSpace(lastMessage.IncomingMessage))
                    return; // Mesaj yoksa işlemi sonlandır

                string deviceInfo = lastMessage.IncomingMessage;
                Console.WriteLine(deviceInfo);
                string[] parts = deviceInfo.Split(';');
                if (parts.Length == 6 &&
                    !string.IsNullOrWhiteSpace(parts[0]) &&
                    !string.IsNullOrWhiteSpace(parts[1]) &&
                    !string.IsNullOrWhiteSpace(parts[2]) &&
                    !string.IsNullOrWhiteSpace(parts[3]) &&
                    !string.IsNullOrWhiteSpace(parts[4]) &&
                    !string.IsNullOrWhiteSpace(parts[5]))
                {
                    // UI iş parçacığında çalıştır
                    if (Application.Current?.Dispatcher.CheckAccess() == true)
                    {
                        AssignDeviceProperties(device, parts);
                    }
                    else
                    {
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            AssignDeviceProperties(device, parts);
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing device info: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AssignDeviceProperties(Device device, string[] parts)
        {
            device.Properties.CompanyName = parts[0];
            device.Properties.ProductName = parts[1];
            device.Properties.ProductModel = parts[2];
            device.Properties.ManufactureDate = parts[3];
            device.Properties.ProductId = parts[4];
            device.Properties.FirmwareVersion = parts[5];
            device.DeviceStatus = DeviceStatus.Identified;
        }
        // ========== CONNECT ==========
        private void ExecuteConnect()
        {
            if (string.IsNullOrEmpty(SelectedPortName))
                return;

            // 1) Manager üzerinden ilgili portu aç
            _manager.ConnectToPort(SelectedPortName, 921600);

            // 2) ConnectedDevices içinde bu porta ait bir cihaz var mı?
            var existingDevice = ConnectedDevices.FirstOrDefault(d => d.PortName == SelectedPortName);
            if (existingDevice == null)
            {
                // Yoksa yeni bir device oluşturup ekleyin:
                var newDevice = new Device(_manager, SelectedPortName, deviceStatus: DeviceStatus.Connected);
                ConnectedDevices.Add(newDevice);
                Device = newDevice;
            }
            else
            {
                // Varsa yalnızca IsConnected durumunu güncelle
                existingDevice.DeviceStatus = DeviceStatus.Connected;
                Device = existingDevice;
            }
            // UI'yi yeniden tetikle
            RaisePropertyChanged(nameof(ConnectedDevices));
            RaisePropertyChanged(nameof(ConnectedPorts));
        }

        // Butonun aktif olması için: Sadece Port seçiliyse
        private bool CanExecuteConnect() => !string.IsNullOrEmpty(SelectedPortName);

        // ========== DISCONNECT ==========
        private async void ExecuteDisconnect()
        {
            // Seçili bir Device yoksa çık
            Device = ConnectedDevices.FirstOrDefault(d => d.PortName == SelectedPortName);
            if (Device == null)
                return;

            // PortName boşsa çık
            if (string.IsNullOrEmpty(Device.PortName))
                return;

            // Manager'dan portu kapat (ağır iş ise arka planda)
            await Task.Run(() => _manager.DisconnectFromPort(Device.PortName));

            // Device'i işaretle
            Device.DeviceStatus = DeviceStatus.Disconnected;
            Device.StopAutoSend();

            // ConnectedDevices'tan çıkar
            var devToRemove = ConnectedDevices.FirstOrDefault(d => d.PortName == Device.PortName);
            if (devToRemove != null)
            {
                ConnectedDevices.Remove(devToRemove);
            }

            // Seçili cihazi null'la (UI'da buton vs. güncellenecek)
            Device = null;
            // UI'yi yeniden tetikle
            RaisePropertyChanged(nameof(ConnectedDevices));
            RaisePropertyChanged(nameof(ConnectedPorts));
        }

        // Butonun aktif olması için: Seçili bir Device ve Port’u dolu olmalı
        private bool CanExecuteDisconnect()
        {
            // Seçili port adına sahip, IsConnected=true durumda bir Device var mı?
            return !string.IsNullOrEmpty(SelectedPortName) && ConnectedDevices.Any(d => d.PortName == SelectedPortName && (d.DeviceStatus == DeviceStatus.Connected || d.DeviceStatus == DeviceStatus.Identified));
        }

        // ========== Cihazın Durumunu Bildir ==========
        public DeviceStatus? IsDeviceStatus(string portName)
        {
            var device = ConnectedDevices.FirstOrDefault(d => d.PortName == portName);
            return device?.DeviceStatus;
        }

        // ========== Port Bağlı mı Kontrol ==========
        public bool IsPortConnected(string portName)
        {
            return _manager.ConnectedPorts.Any(sp => sp.PortName == portName);
        }
    }
}
