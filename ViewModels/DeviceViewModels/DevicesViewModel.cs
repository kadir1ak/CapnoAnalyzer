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
using System.ComponentModel;
using CapnoAnalyzer.Views.DevicesViews.Devices;
namespace CapnoAnalyzer.ViewModels.DeviceViewModels
{
    public class DevicesViewModel : BindableBase
    {
        private readonly SerialPortsManager PortManager;

        private CancellationTokenSource _updateConnectedDevicesInterfaceLoopCancellationTokenSource;
        private readonly object _ConnectedDevicesInterfaceDataLock = new();
        private int ConnectedDevicesUpdateTimeMillisecond = 100;  // 10 Hz (100ms)

        private CancellationTokenSource _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource;
        private readonly object _IdentifiedDevicesInterfaceDataLock = new();
        private int IdentifiedDevicesUpdateTimeMillisecond = 100;  // 10 Hz (100ms)

        // -- 1) Bağlı Cihazların Listesi --
        public ObservableCollection<Device> ConnectedDevices { get; } = new ObservableCollection<Device>();
        public ObservableCollection<Device> IdentifiedDevices { get; } = new ObservableCollection<Device>();

        // -- 2) Manager'dan gelen port listeleri -- 
        public ObservableCollection<SerialPort> ConnectedPorts => PortManager.ConnectedPorts;
        public ObservableCollection<string> AvailablePorts => PortManager.AvailablePorts;

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

        private int _selectedBaudRate = 921600;
        public int SelectedBaudRate
        {
            get => _selectedBaudRate;
            set
            {
                SetProperty(ref _selectedBaudRate, value);
                // Port adı değişince Connect butonunu aktif/pasif güncelle:
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // -- 5) Komutlar (Connect/Disconnect/SendMessage) --
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand IdentifyDeviceCommand { get; }

        // -- 6) Yapıcı Metot --
        public DevicesViewModel()
        {
            PortManager = new SerialPortsManager();
            // Serial port değişikliklerini dinle
            PortManager.MessageReceived += OnMessageReceived;

            ConnectCommand = new DeviceRelayCommand(ExecuteConnect, CanExecuteConnect);
            DisconnectCommand = new DeviceRelayCommand(ExecuteDisconnect, CanExecuteDisconnect);
            IdentifyDeviceCommand = new DeviceRelayCommand(ExecuteIdentifyDevice, CanExecuteIdentifyDevice);

            // UI Güncelleme Döngüsünü Başlat
            StartConnectedDevicesUpdateInterfaceDataLoop();
            StartIdentifiedDevicesUpdateInterfaceDataLoop();

        }

        private async Task UpdateConnectedDevicesInterfaceDataLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(ConnectedDevicesUpdateTimeMillisecond, token);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var device in ConnectedDevices.ToList()) // 🔥 Eğer liste değişirse hata almamak için kopyasını al
                        {
                            if (device == null || device.IncomingMessage == null)
                                continue; // 🔥 Null kontrolü ekle

                            var lastMessage = device.IncomingMessage.LastOrDefault();
                            if (lastMessage == null) continue;

                            device.Interface?.IncomingMessage.Add(lastMessage);

                            while (device.Interface?.IncomingMessage.Count > 10)
                            {
                                device.Interface.IncomingMessage.RemoveAt(0);
                            }

                            DeviceIdentification(device);
                        }

                        // **UI güncellemesi**
                        OnPropertyChanged(nameof(IdentifiedDevices));
                        OnPropertyChanged(nameof(ConnectedDevices));
                        OnPropertyChanged(nameof(ConnectedPorts));
                    });
                }
            }
            catch (TaskCanceledException)
            {
                // **Görev iptal edildi, hata fırlatma**
                Debug.WriteLine("UpdateConnectedDevicesInterfaceDataLoop task canceled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Interface update loop error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void StartConnectedDevicesUpdateInterfaceDataLoop()
        {
            StopConnectedDevicesUpdateInterfaceDataLoop(); // Eski döngüyü durdur
            _updateConnectedDevicesInterfaceLoopCancellationTokenSource = new CancellationTokenSource();
            var token = _updateConnectedDevicesInterfaceLoopCancellationTokenSource.Token;
            _ = UpdateConnectedDevicesInterfaceDataLoop(token);
        }

        public void StopConnectedDevicesUpdateInterfaceDataLoop()
        {
            if (_updateConnectedDevicesInterfaceLoopCancellationTokenSource != null &&
                !_updateConnectedDevicesInterfaceLoopCancellationTokenSource.IsCancellationRequested)
            {
                _updateConnectedDevicesInterfaceLoopCancellationTokenSource.Cancel();
                _updateConnectedDevicesInterfaceLoopCancellationTokenSource.Dispose();
                _updateConnectedDevicesInterfaceLoopCancellationTokenSource = null;
            }
        }
        private async Task UpdateIdentifiedDevicesInterfaceDataLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(IdentifiedDevicesUpdateTimeMillisecond, token);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var device in IdentifiedDevices.ToList()) // 🔥 Kopyasını al, değişiklik olursa hata engellenir.
                        {
                            if (device == null || device.Interface == null || device.Sensor == null)
                                continue; // 🔥 Null nesneleri atla, hata önle.

                            bool hasSensorChanged =
                                device.Interface.Sensor.GasSensor != device.Sensor.GasSensor ||
                                device.Interface.Sensor.ReferenceSensor != device.Sensor.ReferenceSensor ||
                                device.Interface.Sensor.Temperature != device.Sensor.Temperature ||
                                device.Interface.Sensor.Humidity != device.Sensor.Humidity;

                            if (hasSensorChanged)
                            {
                                device.Interface.Sensor = new Sensor
                                {
                                    Time = device.Sensor.Time,
                                    GasSensor = device.Sensor.GasSensor,
                                    ReferenceSensor = device.Sensor.ReferenceSensor,
                                    Temperature = device.Sensor.Temperature,
                                    Humidity = device.Sensor.Humidity
                                };
                                device.Interface.UpdatePlot(); // 🔥 Hata kaynağı olabilecek noktayı kontrol ettik.
                            }
                        }

                        OnPropertyChanged(nameof(IdentifiedDevices));
                    });
                }
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("UpdateIdentifiedDevicesInterfaceDataLoop task canceled."); // ❌ Hata yerine debug mesajı.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Interface update loop error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void StartIdentifiedDevicesUpdateInterfaceDataLoop()
        {
            StopIdentifiedDevicesUpdateInterfaceDataLoop(); // Eski döngüyü durdur
            _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource = new CancellationTokenSource();
            var token = _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource.Token;
            _ = UpdateIdentifiedDevicesInterfaceDataLoop(token);
        }

        public void StopIdentifiedDevicesUpdateInterfaceDataLoop()
        {
            if (_updateIdentifiedDevicesInterfaceLoopCancellationTokenSource != null && !_updateIdentifiedDevicesInterfaceLoopCancellationTokenSource.IsCancellationRequested)
            {
                _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource.Cancel();
                _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource.Dispose();
                _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource = null;
            }
        }
        // ========== Gelen Veri Yakalama (Event) ==========
        private void OnMessageReceived(string portName, string data)
        {
            // Mesajları UI Thread üzerinde eklemek için:
            Application.Current.Dispatcher.Invoke(() =>
            {
                var device = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == portName);
                if (device != null)
                {
                    device.IncomingMessage.Add(data);
                    while (device.IncomingMessage.Count > 10)
                    {
                        device.IncomingMessage.RemoveAt(0);
                    }

                    SensorDataParsing(device, data);
                    CalculateSampleRate(device);
                }
            });
        }
        private void CalculateSampleRate(Device device)
        {
            device.Properties.SampleCount++;
            var now = DateTime.Now;
            var elapsed = now - device.Properties.LastUpdate;

            if (elapsed.TotalSeconds >= 1) // Her saniyede bir hesapla
            {
                device.Properties.DataSamplingFrequency = device.Properties.SampleCount;
                device.Properties.SampleCount = 0;
                device.Properties.LastUpdate = now;
            }
        }
        private void DeviceIdentification(Device device)
        {
            try
            {
                if (device.Properties.Status == DeviceStatus.Identified)
                    return;

                // Null kontrolü
                var lastMessage = device.IncomingMessage.LastOrDefault();
                if (lastMessage == null || string.IsNullOrWhiteSpace(lastMessage))
                    return; // Mesaj yoksa işlemi sonlandır

                string deviceInfo = lastMessage;
                Debug.WriteLine(deviceInfo);
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
                Debug.WriteLine($"Error parsing device info: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void AssignDeviceProperties(Device device, string[] parts)
        {
            // Mevcut cihazı IdentifiedDevices koleksiyonunda kontrol et
            var existingDevice = IdentifiedDevices.FirstOrDefault(d => d.Properties.PortName == device.Properties.PortName);

            if (existingDevice != null)
            {
                // Cihaz zaten varsa, özelliklerini güncelle
                existingDevice.Properties.CompanyName = parts[0];
                existingDevice.Properties.ProductName = parts[1];
                existingDevice.Properties.ProductModel = parts[2];
                existingDevice.Properties.ManufactureDate = parts[3];
                existingDevice.Properties.ProductId = parts[4];
                existingDevice.Properties.FirmwareVersion = parts[5];
                existingDevice.Properties.Status = DeviceStatus.Identified;
            }
            else
            {
                // Cihaz yoksa, özelliklerini ata ve koleksiyona ekle
                device.Properties.CompanyName = parts[0];
                device.Properties.ProductName = parts[1];
                device.Properties.ProductModel = parts[2];
                device.Properties.ManufactureDate = parts[3];
                device.Properties.ProductId = parts[4];
                device.Properties.FirmwareVersion = parts[5];
                device.Properties.Status = DeviceStatus.Identified;

                IdentifiedDevices.Add(device);
            }
            Debug.WriteLine($"Device Identified: {device.Properties.PortName}");
            // UI güncellemesi
            OnPropertyChanged(nameof(IdentifiedDevices));
            OnPropertyChanged(nameof(ConnectedDevices));
        }
        // ========== CONNECT ==========
        private void ExecuteConnect()
        {
            if (string.IsNullOrEmpty(SelectedPortName))
                return;

            // 1) Manager üzerinden ilgili portu aç
            PortManager.ConnectToPort(SelectedPortName, SelectedBaudRate);

            // 2) ConnectedDevices içinde bu porta ait bir cihaz var mı?
            var existingDevice = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);
            if (existingDevice == null)
            {
                // Yoksa yeni bir device oluşturup ekleyin:
                var newDevice = new Device(PortManager, SelectedPortName, deviceStatus: DeviceStatus.Connected);
                ConnectedDevices.Add(newDevice);
                Device = newDevice;
            }
            else
            {
                // Varsa yalnızca IsConnected durumunu güncelle
                existingDevice.Properties.Status = DeviceStatus.Connected;
                Device = existingDevice;
            }
            Debug.WriteLine($"ConnectedDevices: {Device.Properties.PortName}");
     
            // UI'yi yeniden tetikle
            OnPropertyChanged(nameof(ConnectedDevices));
            OnPropertyChanged(nameof(ConnectedPorts));
        }

        // Butonun aktif olması için: Sadece Port seçiliyse
        private bool CanExecuteConnect() => !string.IsNullOrEmpty(SelectedPortName);

        // ========== DISCONNECT ==========
        private async void ExecuteDisconnect()
        {
            // İlk olarak IdentifiedDevices içerisindeki cihazı kontrol et
            var identifiedDevice = IdentifiedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);
            if (identifiedDevice != null)
            {
                // PortName boşsa işlem yapma
                if (!string.IsNullOrEmpty(identifiedDevice.Properties.PortName))
                {
                    // Manager'dan portu kapat (arka planda çalıştır)
                    await Task.Run(() => PortManager.DisconnectFromPort(identifiedDevice.Properties.PortName));

                    // Cihazın durumunu güncelle
                    identifiedDevice.Properties.Status = DeviceStatus.Disconnected;
                    identifiedDevice.StopAutoSend();

                    // IdentifiedDevices'tan cihazı çıkar
                    IdentifiedDevices.Remove(identifiedDevice);

                    // UI'yi güncelle
                    OnPropertyChanged(nameof(IdentifiedDevices));
                }
            }

            // Daha sonra ConnectedDevices içerisindeki cihazı kontrol et
            var connectedDevice = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);
            if (connectedDevice != null)
            {
                // PortName boşsa işlem yapma
                if (!string.IsNullOrEmpty(connectedDevice.Properties.PortName))
                {
                    // Manager'dan portu kapat (arka planda çalıştır)
                    await Task.Run(() => PortManager.DisconnectFromPort(connectedDevice.Properties.PortName));

                    // Cihazın durumunu güncelle
                    connectedDevice.Properties.Status = DeviceStatus.Disconnected;
                    connectedDevice.StopAutoSend();

                    // ConnectedDevices'tan cihazı çıkar
                    ConnectedDevices.Remove(connectedDevice);

                    // UI'yi güncelle
                    OnPropertyChanged(nameof(ConnectedDevices));
                }
            }
            Debug.WriteLine($"Disconnected: {SelectedPortName}");
            // Seçili cihazı sıfırla (UI'da buton vs. güncellenecek)
            Device = null;
            OnPropertyChanged(nameof(ConnectedPorts));
        }

        // Butonun aktif olması için: Seçili bir Device ve Port’u dolu olmalı
        private bool CanExecuteDisconnect()
        {
            // Seçili port adına sahip, IsConnected=true durumda bir Device var mı?
            return !string.IsNullOrEmpty(SelectedPortName) && ConnectedDevices.Any(d => d.Properties.PortName == SelectedPortName && (d.Properties.Status == DeviceStatus.Connected || d.Properties.Status == DeviceStatus.Identified));
        }
        // ========== DISCONNECT ==========
        private async void ExecuteIdentifyDevice()
        {
            // İlk olarak IdentifiedDevices içerisindeki cihazı kontrol et
            var identifiedDevice = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);
            if (identifiedDevice != null)
            {
                // PortName boşsa işlem yapma
                if (!string.IsNullOrEmpty(identifiedDevice.Properties.PortName))
                {
                    // Cihaz zaten varsa, özelliklerini güncelle
                    identifiedDevice.Properties.CompanyName = "Null";
                    identifiedDevice.Properties.ProductName = "Null";
                    identifiedDevice.Properties.ProductModel = "Null";
                    identifiedDevice.Properties.ManufactureDate = "Null";
                    identifiedDevice.Properties.ProductId = SelectedPortName;
                    identifiedDevice.Properties.FirmwareVersion = "Null";
                    identifiedDevice.Properties.Status = DeviceStatus.Identified;

                    // IdentifiedDevices'tan cihazı çıkar
                    IdentifiedDevices.Add(identifiedDevice);

                    // UI'yi güncelle
                    OnPropertyChanged(nameof(IdentifiedDevices));
                    OnPropertyChanged(nameof(ConnectedDevices));
                }
            }
            Debug.WriteLine($"Identify Device: {SelectedPortName}");
        }

        // Butonun aktif olması için: Seçili bir Device ve Port’u dolu olmalı
        private bool CanExecuteIdentifyDevice()
        {
            // Seçili port adına sahip, IsConnected=true durumda bir Device var mı?
            return !string.IsNullOrEmpty(SelectedPortName) && ConnectedDevices.Any(d => d.Properties.PortName == SelectedPortName && (d.Properties.Status == DeviceStatus.Connected || d.Properties.Status == DeviceStatus.Identified));
        }

        // ========== Cihazın Durumunu Bildir ==========
        public DeviceStatus? IsDeviceStatus(string portName)
        {
            var device = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == portName);
            return device?.Properties.Status;
        }

        // ========== Port Bağlı mı Kontrol ==========
        public bool IsPortConnected(string portName)
        {
            return PortManager.ConnectedPorts.Any(sp => sp.PortName == portName);
        }
        public void CloseAllPorts()
        {
            foreach (var device in ConnectedDevices)
            {
                device.StopAutoSend(); // Otomatik veri göndermeyi durdur
                PortManager.DisconnectFromPort(device.Properties.PortName); // Tüm portları kapat
            }

            // Portları yönetim nesnesinden de temizle
            PortManager.ConnectedPorts.Clear();
        }

        private void SensorDataParsing(Device device, string data)
        {
            try
            {
                // Veriyi ayrıştır ve UI güncellemesini yap
                string[] dataParts = data.Split(',');
                if (dataParts.Length == 5 &&
                    double.TryParse(dataParts[0].Replace('.', ','), out double time) &&
                    double.TryParse(dataParts[1].Replace('.', ','), out double ch1) &&
                    double.TryParse(dataParts[2].Replace('.', ','), out double ch2) &&
                    double.TryParse(dataParts[3].Replace('.', ','), out double temp) &&
                    double.TryParse(dataParts[4].Replace('.', ','), out double hum))
                {
                    if (Application.Current?.Dispatcher.CheckAccess() == true)
                    {
                        // UI iş parçacığında isek doğrudan çalıştır
                        UpdateSensorData(device, time, ch1, ch2, temp, hum);
                    }
                    else
                    {
                        // UI iş parçacığında değilsek Dispatcher kullan
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            UpdateSensorData(device, time, ch1, ch2, temp, hum);
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // İşlem iptal edildiğinde hata fırlatmayı önle
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected Error in Data Processing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSensorData(Device device, double time, double ch1, double ch2, double temp, double hum)
        {
            device.Sensor.Time = time;
            device.Sensor.GasSensor = ch1;
            device.Sensor.ReferenceSensor = ch2;
            device.Sensor.Temperature = temp;
            device.Sensor.Humidity = hum;
        }
    }
}
