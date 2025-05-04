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
using CapnoAnalyzer.Views.DevicesViews.DevicesControl;
using System;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using MathNet.Numerics.Distributions;
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

        //public CalibrationViewModel CalibrationVM = new CalibrationViewModel();

        // -- 1) Bağlı Cihazların Listesi --
        private ObservableCollection<Device> _connectedDevices = new();
        public ObservableCollection<Device> ConnectedDevices
        {
            get => _connectedDevices;
            set
            {
                _connectedDevices = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<Device> _identifiedDevices = new();
        public ObservableCollection<Device> IdentifiedDevices
        {
            get => _identifiedDevices;
            set
            {
                _identifiedDevices = value;
                OnPropertyChanged();
            }
        }

        // -- 2) Manager'dan gelen port listeleri -- 
        public ObservableCollection<SerialPort> ConnectedPorts => PortManager.ConnectedPorts;
        public ObservableCollection<string> AvailablePorts => PortManager.AvailablePorts;

        // Cihaz ekleme/kaldırma için bir olay tanımlıyoruz
        public event Action<Device> DeviceAdded;
        public event Action<Device> DeviceRemoved;

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
                DeviceIDUpdate();
                DeviceTestModeUpdate();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _deviceID;
        public string DeviceID
        {
            get => _deviceID;
            set
            {
                SetProperty(ref _deviceID, value);
            }
        }

        private string _dataPacketType;
        public string DataPacketType
        {
            get => _dataPacketType;
            set
            {
                SetProperty(ref _dataPacketType, value);
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

            ConnectedDevices = new ObservableCollection<Device>();
            IdentifiedDevices = new ObservableCollection<Device>();
            IdentifiedDevices.CollectionChanged += IdentifiedDevices_CollectionChanged;

            ConnectCommand = new DeviceRelayCommand(ExecuteConnect, CanExecuteConnect);
            DisconnectCommand = new DeviceRelayCommand(ExecuteDisconnect, CanExecuteDisconnect);
            IdentifyDeviceCommand = new DeviceRelayCommand(ExecuteIdentifyDevice, CanExecuteIdentifyDevice);

            // UI Güncelleme Döngüsünü Başlat
            StartConnectedDevicesUpdateInterfaceDataLoop();
            StartIdentifiedDevicesUpdateInterfaceDataLoop();

        }
        private void IdentifiedDevices_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Device newDevice in e.NewItems)
                {
                    DeviceAdded?.Invoke(newDevice); // Yeni cihaz eklendiğinde tetiklenir
                }
            }

            if (e.OldItems != null)
            {
                foreach (Device removedDevice in e.OldItems)
                {
                    DeviceRemoved?.Invoke(removedDevice); // Cihaz kaldırıldığında tetiklenir
                }
            }
        }
        private void DeviceIDUpdate()
        {
            try
            {
                // Seçili porta göre cihazı bul
                var device = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);

                if (device != null)
                {
                    // Eğer cihaz bulunduysa ve DeviceID bilgisi mevcutsa, DeviceID'yi ata
                    if (!string.IsNullOrEmpty(device.Properties.ProductId))
                    {
                        DeviceID = device.Properties.ProductId;
                    }
                }
                else
                {
                    DeviceID = SelectedPortName;
                }

                // UI'yi güncelle
                OnPropertyChanged(nameof(IdentifiedDevices));
                OnPropertyChanged(nameof(ConnectedDevices));

                Debug.WriteLine($"Device ID Updated: {SelectedPortName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Device ID Update Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeviceTestModeUpdate()
        {
            try
            {
                // Seçili porta göre cihazı bul
                var device = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);

                if (device != null)
                {
                    // Eğer cihaz bulunduysa ve TestMode bilgisi mevcutsa, TestMode'u ata
                    if (!string.IsNullOrEmpty(device.Properties.DataPacketType))
                    {
                        DataPacketType = device.Properties.DataPacketType;
                    }
                }
                else
                {
                    DataPacketType = "1";
                }

                // UI'yi güncelle
                OnPropertyChanged(nameof(IdentifiedDevices));
                OnPropertyChanged(nameof(ConnectedDevices));

                Debug.WriteLine($"Device Test Mode Updated: {SelectedPortName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Device Test Mode Update Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                            if (device == null)
                                continue; // 🔥 Null nesneleri atla, hata önle.

                            device.Interface.SyncWithDevice(device);
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

                    DeviceDataParsing(device, data);
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
                existingDevice.Properties.ProductId = parts[4];
                existingDevice.Properties.FirmwareVersion = parts[5];
                existingDevice.Properties.Status = DeviceStatus.Identified;
            }
            else
            {
                // Cihaz yoksa, özelliklerini ata ve koleksiyona ekle
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
                newDevice.Properties.BaudRate = SelectedBaudRate;
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
                    identifiedDevice.Properties.ProductId = DeviceID;
                    identifiedDevice.Properties.BaudRate = SelectedBaudRate;
                    identifiedDevice.Properties.DataPacketType = DataPacketType;
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

        // Butonun aktif olması için: Seçili bir Device olmalı (SelectedPortName boş olmamalı).
        // Seçili porta bağlı bir cihaz olmalı(ConnectedDevices listesinde, IsConnected == true olan bir cihaz bulunmalı).
        // Bu cihaz, IdentifiedDevices listesinde olmamalı.
        private bool CanExecuteIdentifyDevice()
        {
            // Seçili port adı boş mu?
            if (string.IsNullOrEmpty(SelectedPortName))
                return false;

            // Seçili porta bağlı bir cihaz var mı ve bu cihaz IdentifiedDevices listesinde değil mi?
            return ConnectedDevices.Any(connectedDevice =>
                connectedDevice.Properties.PortName == SelectedPortName &&
                connectedDevice.Properties.Status == DeviceStatus.Connected &&
                !IdentifiedDevices.Any(identifiedDevice =>
                    identifiedDevice.Properties.PortName == SelectedPortName));
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

        private void DeviceDataParsing(Device device, string data)
        {
            try
            {
                switch (device.Properties.DataPacketType)
                {
                    case "1":
                        UpdateDeviceDataPacket_1(device, data);
                        break;
                    case "2":
                        UpdateDeviceDataPacket_2(device, data);
                        break;
                    case "3":
                        UpdateDeviceDataPacket_3(device, data);
                        break;
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

        private void UpdateDeviceDataPacket_1(Device device, string data)
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
                    device.DataPacket_1.Time = time;
                    device.DataPacket_1.GasSensor = ch1;
                    device.DataPacket_1.ReferenceSensor = ch2;
                    device.DataPacket_1.Temperature = temp;
                    device.DataPacket_1.Humidity = hum;

                    device.DeviceData.SensorData.Time = time;
                    device.DeviceData.SensorData.IIR_Gas_Voltage = ch1;
                    device.DeviceData.SensorData.IIR_Ref_Voltage = ch2;
                    device.DeviceData.SensorData.IR_Status = 0.0;
                }
                else
                {
                    // UI iş parçacığında değilsek Dispatcher kullan
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        device.DataPacket_1.Time = time;
                        device.DataPacket_1.GasSensor = ch1;
                        device.DataPacket_1.ReferenceSensor = ch2;
                        device.DataPacket_1.Temperature = temp;
                        device.DataPacket_1.Humidity = hum;

                        device.DeviceData.SensorData.Time = time;
                        device.DeviceData.SensorData.IIR_Gas_Voltage = ch1;
                        device.DeviceData.SensorData.IIR_Ref_Voltage = ch2;
                        device.DeviceData.SensorData.IR_Status = 0.0;
                    });
                }
            }
        }
        private void UpdateDeviceDataPacket_2(Device device, string data)
        {
            // Veri paketinin "GV" ile başladığını kontrol et
            if (!data.StartsWith("GV"))
            {
                // Veri doğru formatta değilse işlemi sonlandır
                return;
            }

            // "GV," kısmını kaldır ve veriyi ayrıştır
            string[] dataParts = data.Substring(3).Split(',');

            // Veri parça sayısını ve türlerini kontrol et
            if (dataParts.Length == 17 &&
                double.TryParse(dataParts[0].Replace('.', ','), out double time) &&
                double.TryParse(dataParts[1].Replace('.', ','), out double ang1) &&
                double.TryParse(dataParts[2].Replace('.', ','), out double ang2) &&
                double.TryParse(dataParts[3].Replace('.', ','), out double ang3) &&
                double.TryParse(dataParts[4].Replace('.', ','), out double raw1) &&
                double.TryParse(dataParts[5].Replace('.', ','), out double raw2) &&
                double.TryParse(dataParts[6].Replace('.', ','), out double raw3) &&
                double.TryParse(dataParts[7].Replace('.', ','), out double raw4) &&
                double.TryParse(dataParts[8].Replace('.', ','), out double volt1) &&
                double.TryParse(dataParts[9].Replace('.', ','), out double volt2) &&
                double.TryParse(dataParts[10].Replace('.', ','), out double volt3) &&
                double.TryParse(dataParts[11].Replace('.', ','), out double volt4) &&
                double.TryParse(dataParts[12].Replace('.', ','), out double voltF2) &&
                double.TryParse(dataParts[13].Replace('.', ','), out double voltF3) &&
                double.TryParse(dataParts[14].Replace('.', ','), out double voltIIR2) &&
                double.TryParse(dataParts[15].Replace('.', ','), out double voltIIR3) &&
                int.TryParse(dataParts[16], out int irStatus))
            {
                // UI güncellemesi için Dispatcher kontrolü
                if (Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    // Verileri güncelle
                    device.DataPacket_2.Time = time;
                    device.DataPacket_2.AngVoltages = new[] { ang1, ang2, ang3 };
                    device.DataPacket_2.AdsRawValues = new[] { raw1, raw2, raw3, raw4 };
                    device.DataPacket_2.AdsVoltages = new[] { volt1, volt2, volt3, volt4 };
                    device.DataPacket_2.GainAdsVoltagesF = new[] { voltF2 * 10.0, voltF3 * 10.0 };
                    device.DataPacket_2.GainAdsVoltagesIIR = new[] { voltIIR2 * 10.0, voltIIR3 * 10.0 };
                    device.DataPacket_2.IrStatus = irStatus;

                    device.DeviceData.SensorData.Time = time;
                    device.DeviceData.SensorData.IIR_Gas_Voltage = voltIIR2 * 10.0;
                    device.DeviceData.SensorData.IIR_Ref_Voltage = voltIIR3 * 10.0;
                    device.DeviceData.SensorData.IR_Status = irStatus;
                }
                else
                {
                    // Dispatcher kullanarak verileri güncelle
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        device.DataPacket_2.Time = time;
                        device.DataPacket_2.AngVoltages = new[] { ang1, ang2, ang3 };
                        device.DataPacket_2.AdsRawValues = new[] { raw1, raw2, raw3, raw4 };
                        device.DataPacket_2.AdsVoltages = new[] { volt1, volt2, volt3, volt4 };
                        device.DataPacket_2.GainAdsVoltagesF = new[] { voltF2 * 10.0, voltF3 * 10.0 };
                        device.DataPacket_2.GainAdsVoltagesIIR = new[] { voltIIR2 * 10.0, voltIIR3 * 10.0 };
                        device.DataPacket_2.IrStatus = irStatus;

                        device.DeviceData.SensorData.Time = time;
                        device.DeviceData.SensorData.IIR_Gas_Voltage = voltIIR2 * 10.0;
                        device.DeviceData.SensorData.IIR_Ref_Voltage = voltIIR3 * 10.0;
                        device.DeviceData.SensorData.IR_Status = irStatus;
                    });
                }
            }
        }

        private void UpdateDeviceDataPacket_3(Device device, string data)
        {
            // Veriyi ayrıştır ve UI güncellemesini yap
            string[] dataParts = data.Split(',');
            if (dataParts.Length == 5 &&
                double.TryParse(dataParts[0].Replace('.', ','), out double time) &&
                double.TryParse(dataParts[2].Replace('.', ','), out double ch0) &&
                double.TryParse(dataParts[3].Replace('.', ','), out double ch1) &&
                int.TryParse(dataParts[4], out int frame) &&
                int.TryParse(dataParts[4], out int emitter))
            {
                if (Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    device.DataPacket_3.Time = time;
                    device.DataPacket_3.Ch0 = ch0;
                    device.DataPacket_3.Ch1 = ch1;
                    device.DataPacket_3.Frame = frame;
                    device.DataPacket_3.Emitter = emitter;

                    device.DeviceData.SensorData.Time = time;
                    device.DeviceData.SensorData.IIR_Gas_Voltage = ch0;
                    device.DeviceData.SensorData.IIR_Ref_Voltage = ch1;
                    device.DeviceData.SensorData.IR_Status = emitter;
                }
                else
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        device.DataPacket_3.Time = time;
                        device.DataPacket_3.Ch0 = ch0;
                        device.DataPacket_3.Ch1 = ch1;
                        device.DataPacket_3.Frame = frame;
                        device.DataPacket_3.Emitter = emitter;

                        device.DeviceData.SensorData.Time = time;
                        device.DeviceData.SensorData.IIR_Gas_Voltage = ch0;
                        device.DeviceData.SensorData.IIR_Ref_Voltage = ch1;
                        device.DeviceData.SensorData.IR_Status = emitter;
                    });
                }
            }
        }
    }
}
