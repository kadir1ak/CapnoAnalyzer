using System.IO.Ports;
using System.Collections.ObjectModel;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Services;
using System.Windows.Input;
using CapnoAnalyzer.Models.PlotModels;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;

namespace CapnoAnalyzer.Models.Device
{
    public class Device : BindableBase
    {
        private readonly SerialPortsManager PortManager;
        private CancellationTokenSource _autoSendTokenSource;

        // Giden mesajları kontrol eden komutlar
        public ICommand SendMessageCommand { get; }
        public ICommand AutoSendMessageCommand { get; }

        public Device(SerialPortsManager manager, string portName, DeviceStatus deviceStatus)
        {
            PortManager = manager;
            PortManager.DeviceDisconnected += OnDeviceDisconnected;

            Properties.PortName = portName;
            Properties.DeviceStatus = deviceStatus;

            SendMessageCommand = new DeviceRelayCommand(SendMessage, CanSendMessage);
            AutoSendMessageCommand = new DeviceRelayCommand(AutoSend);

            // Sensor değişikliklerini dinle ve grafiği güncelle
            Interface.Sensor.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(Sensor.Time) ||
                    args.PropertyName == nameof(Sensor.GasSensor) ||
                    args.PropertyName == nameof(Sensor.ReferenceSensor))
                {
                    UpdatePlotWithSensorData();
                }
            };
        }

        // === NORMAL MESAJ GÖNDERME ===
        private void SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(Interface.OutgoingMessage))
            {
                PortManager.SendMessage(Properties.PortName, Interface.OutgoingMessage);
            }
        }

        private bool CanSendMessage()
        {
            return (Properties.DeviceStatus == DeviceStatus.Connected || Properties.DeviceStatus == DeviceStatus.Identified) && !string.IsNullOrWhiteSpace(Interface.OutgoingMessage);
        }

        // === OTOMATİK GÖNDERME ===
        private bool _autoSendActive = false;
        public bool AutoSendActive
        {
            get => _autoSendActive;
            set => SetProperty(ref _autoSendActive, value);
        }

        private void AutoSend()
        {
            if (AutoSendActive)
            {
                StopAutoSend();
            }
            else
            {
                StartAutoSend();
            }
        }

        private void StartAutoSend()
        {
            if (!CanSendMessage()) return;

            AutoSendActive = true;
            _autoSendTokenSource = new CancellationTokenSource();
            CancellationToken token = _autoSendTokenSource.Token;

            Task.Run(async () =>
            {
                while (AutoSendActive && !token.IsCancellationRequested)
                {
                    PortManager.SendMessage(Properties.PortName, Interface.OutgoingMessage);
                    await Task.Delay(10, token); // 10ms bekle
                }
            }, token);
        }

        public void StopAutoSend()
        {
            AutoSendActive = false;
            _autoSendTokenSource?.Cancel();
        }

        private void OnDeviceDisconnected(string portName)
        {
            if (Properties.PortName == portName) // Eğer bağlı olduğu port kaldırıldıysa
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Properties.DeviceStatus = DeviceStatus.Disconnected; // UI'da "Disconnected" olarak göster
                    NotifyUIForRemoval(); // UI'daki listeden kaldırma işlemi için çağrı yap.
                });
            }
        }

        // UI tarafında cihazı kaldırmak için olay
        public event Action<Device> RemoveDeviceFromUI;

        private void NotifyUIForRemoval()
        {
            RemoveDeviceFromUI?.Invoke(this);
        }

        // Cihazın nesnesini tamamen silmek istiyorsak:
        public void DisposeDevice()
        {
            StopAutoSend();
            PortManager.DeviceDisconnected -= OnDeviceDisconnected;
        }

        // Gelen mesajları tutacağımız koleksiyon
        private ObservableCollection<string> _incomingMessage = new ObservableCollection<string>();
        public ObservableCollection<string> IncomingMessage
        {
            get => _incomingMessage;
            set => SetProperty(ref _incomingMessage, value);
        }

        private DeviceInterface _interface = new DeviceInterface();
        public DeviceInterface Interface
        {
            get => _interface;
            set => SetProperty(ref _interface, value);
        }

        private Sensor _sensor = new Sensor();
        public Sensor Sensor
        {
            get => _sensor;
            set => SetProperty(ref _sensor, value);
        }

        private DeviceProperties _properties = new DeviceProperties();
        public DeviceProperties Properties
        {
            get => _properties;
            set => SetProperty(ref _properties, value);
        }

        /// <summary>
        /// Sensor'dan gelen verilerle grafiği günceller.
        /// </summary>
        private void UpdatePlotWithSensorData()
        {
            Interface.MyPlot.AddDataPoint(Interface.Sensor.Time, Interface.Sensor.GasSensor, Interface.Sensor.ReferenceSensor);
        }
    }

    public class DeviceProperties : BindableBase
    {
        public int sampleCount = 0;

        public DateTime lastUpdate = DateTime.Now;

        public DeviceProperties()
        {
            DeviceStatus = DeviceStatus.Disconnected;
            ID = string.Empty;
            BaudRate = 9600;
            DataSamplingFrequency = 0;
            PortName = string.Empty;
            CompanyName = string.Empty;
            ProductName = string.Empty;
            ProductModel = string.Empty;
            ManufactureDate = string.Empty;
            ProductId = string.Empty;
            FirmwareVersion = string.Empty;
        }

        public DeviceStatus DeviceStatus { get; set; }
        public int BaudRate { get; set; }
        public int DataSamplingFrequency { get; set; }
        public string ID { get; set; }
        public string PortName { get; set; }
        public string CompanyName { get; set; }
        public string ProductName { get; set; }
        public string ProductModel { get; set; }
        public string ManufactureDate { get; set; }
        public string ProductId { get; set; }
        public string FirmwareVersion { get; set; }
    }

    public enum DeviceStatus
    {
        Connected,
        Disconnected,
        Identified,
        Unidentified
    }
}
