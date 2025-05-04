using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.PlotModels;
using CapnoAnalyzer.Services;

namespace CapnoAnalyzer.Models.Device
{
    public class Device : BindableBase
    {
        private readonly SerialPortsManager _portManager;
        private CancellationTokenSource _autoSendTokenSource;

        #region Komutlar
        public ICommand SendMessageCommand { get; }
        public ICommand AutoSendMessageCommand { get; }        
        public ICommand AutoSaveDataCommand { get; }        
        #endregion

        #region Constructor
        public Device(SerialPortsManager manager, string portName, DeviceStatus deviceStatus)
        {
            _portManager = manager;
            _portManager.DeviceDisconnected += OnDeviceDisconnected;
            _portManager.MessageReceived += OnMessageReceived;

            Properties = new DeviceProperties
            {
                PortName = portName,
                Status = deviceStatus
            };

            SendMessageCommand = new DeviceRelayCommand(SendMessage, CanSendMessage);
            AutoSendMessageCommand = new DeviceRelayCommand(ToggleAutoSend);
            AutoSaveDataCommand = new DeviceRelayCommand(ToggleAutoSaveData);

            // İşlemi başlat
            InitializeDeviceAsync();
        }
        #endregion

        #region Normal Mesaj Gönderme
        private void SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(Interface.OutgoingMessage))
            {
                _portManager.SendMessage(Properties.PortName, Interface.OutgoingMessage);
            }
        }

        private bool CanSendMessage()
        {
            return (Properties.Status == DeviceStatus.Connected || Properties.Status == DeviceStatus.Identified) &&
                   !string.IsNullOrWhiteSpace(Interface.OutgoingMessage);
        }
        #endregion

        #region Otomatik Veri Kaydetme

        // Otomatik veri kaydetme için gerekli alanlar
        private StreamWriter _autoSaveWriter;
        private string _autoSaveFilePath;
        private bool _autoSaveDataActive;

        // "Save Data" aktif mi?
        public bool AutoSaveDataActive
        {
            get => _autoSaveDataActive;
            set => SetProperty(ref _autoSaveDataActive, value);
        }

        /// <summary>
        /// Kullanıcı arayüzündeki "Save Data" (AutoSaveData) CheckBox’ı
        /// işaretlenip/işareti kaldırıldığında tetiklenir.
        /// </summary>
        public void ToggleAutoSaveData()
        {
            if (AutoSaveDataActive)
            {
                // Şu anda aktifse kapat
                StopAutoSaveData();
            }
            else
            {
                // Kapalıysa başlat
                StartAutoSaveData();
            }
        }

        /// <summary>
        /// Yeni bir .txt dosyası oluşturur ve yazıma hazırlar.
        /// </summary>
        private void StartAutoSaveData()
        {
            if (AutoSaveDataActive) return;  // Zaten aktifse tekrar yapma

            AutoSaveDataActive = true;

            // Masaüstünde klasör ve dosya yolunu oluştur
            SetAutoSaveFilePath();

            // Dosyayı aç (append:false => yeni)
            _autoSaveWriter = new StreamWriter(_autoSaveFilePath, append: false);

            // Başlangıç notu (isteğe bağlı)
            _autoSaveWriter.WriteLine($"=== Log Start for {Properties.PortName} at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _autoSaveWriter.Flush();
        }

        /// <summary>
        /// Açık dosyayı "Log End" satırı yazarak kapatır, kaynakları temizler.
        /// </summary>
        private void StopAutoSaveData()
        {
            if (!AutoSaveDataActive) return; // Zaten kapalıysa çık

            AutoSaveDataActive = false;

            if (_autoSaveWriter != null)
            {
                // Bitiş notu (isteğe bağlı)
                _autoSaveWriter.WriteLine($"=== Log End at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                _autoSaveWriter.Flush();
                _autoSaveWriter.Close();
                _autoSaveWriter.Dispose();
                _autoSaveWriter = null;
            }
        }

        /// <summary>
        /// Masaüstünde "CapnoLogs" klasörü içinde
        /// "PortName_yyyyMMdd_HHmmss.txt" formatında bir log dosyası belirler.
        /// </summary>
        private void SetAutoSaveFilePath()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string logFolder = Path.Combine(desktopPath, "CapnoLogs");
            Directory.CreateDirectory(logFolder);

            // Örnek dosya adı: "COM3_20250409_153000.txt"
            string fileName = $"{Properties.PortName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            _autoSaveFilePath = Path.Combine(logFolder, fileName);
        }

        /// <summary>
        /// SerialPortsManager tarafından herhangi bir porttan veri geldiğinde
        /// tetiklenen olaydır. Bu veri gerçekten bu cihaza (this.PortName) aitse 
        /// ve Save Data aktifse, dosyaya yazar.
        /// </summary>
        private void OnMessageReceived(string receivedPortName, string data)
        {
            // Bu cihazın portu mu?
            if (Properties.PortName == receivedPortName)
            {
                // "Save Data" aktifse ve dosya açıksa
                if (AutoSaveDataActive && _autoSaveWriter != null)
                {
                    // Zaman damgası ekleyerek log
                    string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    _autoSaveWriter.WriteLine($"[{timeStamp}] {data}");
                    _autoSaveWriter.Flush();  // Her satırda flush ederseniz anlık yazılır
                }
            }
        }
        #endregion

        #region Otomatik Mesaj Gönderme
        private bool _autoSendActive = false;
        public bool AutoSendActive
        {
            get => _autoSendActive;
            set => SetProperty(ref _autoSendActive, value);
        }

        public void ToggleAutoSend()
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

        private async void StartAutoSend()
        {
            if (!CanSendMessage()) return;

            AutoSendActive = true;
            _autoSendTokenSource = new CancellationTokenSource();
            var token = _autoSendTokenSource.Token;

            try
            {
                while (AutoSendActive && !token.IsCancellationRequested)
                {
                    _portManager.SendMessage(Properties.PortName, Interface.OutgoingMessage);
                    await Task.Delay(10, token); // 10ms bekle
                }
            }
            catch (TaskCanceledException)
            {
                // Görev iptal edildiğinde bir şey yapmaya gerek yok
            }
        }

        public void StopAutoSend()
        {
            AutoSendActive = false;
            _autoSendTokenSource?.Cancel();
        }
        #endregion

        #region Cihaz Bağlantı Yönetimi
        private void OnDeviceDisconnected(string portName)
        {
            if (Properties.PortName == portName)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Properties.Status = DeviceStatus.Disconnected;
                    NotifyUIForRemoval();
                });
            }
        }

        public event Action<Device> RemoveDeviceFromUI;

        private void NotifyUIForRemoval() => RemoveDeviceFromUI?.Invoke(this);

        public void DisposeDevice()
        {
            StopAutoSend();
            _portManager.DeviceDisconnected -= OnDeviceDisconnected;
        }
        #endregion

        #region Özellikler
        private ObservableCollection<string> _incomingMessage = new();
        public ObservableCollection<string> IncomingMessage
        {
            get => _incomingMessage;
            set => SetProperty(ref _incomingMessage, value);
        }
        
        private DeviceData _deviceData = new();
        public DeviceData DeviceData
        {
            get => _deviceData;
            set => SetProperty(ref _deviceData, value);
        }

        private DeviceInterface _interface = new();
        public DeviceInterface Interface
        {
            get => _interface;
            set => SetProperty(ref _interface, value);
        }

        private DataPacket_1 _dataPacket_1 = new();
        public DataPacket_1 DataPacket_1
        {
            get => _dataPacket_1;
            set => SetProperty(ref _dataPacket_1, value);
        }

        private DataPacket_2 _dataPacket_2 = new();
        public DataPacket_2 DataPacket_2
        {
            get => _dataPacket_2;
            set => SetProperty(ref _dataPacket_2, value);
        }

        private DataPacket_3 _dataPacket_3 = new(); 
        public DataPacket_3 DataPacket_3
        {
            get => _dataPacket_3;
            set => SetProperty(ref _dataPacket_3, value);
        }

        private DeviceProperties _properties = new();
        public DeviceProperties Properties
        {
            get => _properties;
            set => SetProperty(ref _properties, value);
        }
        #endregion

        private async void InitializeDeviceAsync()
        {
            await StartSensorProcessingAsync();
        }

        private bool isProcessing = false; // İşlem durumu kontrolü için bayrak
        private CancellationTokenSource cancellationTokenSource = null; // Task iptal mekanizması

        public async Task StartSensorProcessingAsync()
        {
            // Eğer işlem zaten çalışıyorsa yeni bir işlem başlatma
            if (isProcessing)
            {
                Debug.WriteLine("Sensor processing is already running.");
                return;
            }

            // İşlem durumu ve iptal mekanizmasını başlat
            isProcessing = true;
            cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Sonsuz döngü içinde sensör işleme başlat
                await Task.Run(async () =>
                {
                    while (!cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        var samplingStartTime = DateTime.Now;
                        var gasSensorValues = new List<double>();
                        var referenceSensorValues = new List<double>();

                        // MaxValueSamplingTime süresi boyunca sensör verilerini topla
                        while ((DateTime.Now - samplingStartTime).TotalSeconds < Interface.MaxValueSamplingTime)
                        {
                            if (cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                Debug.WriteLine("Sensor processing canceled.");
                                break;
                            }

                            // Gelen verileri kontrol et ve biriktir
                            switch (Properties.DataPacketType)
                            {
                                case "1":
                                    gasSensorValues.Add(DataPacket_1.GasSensor);
                                    referenceSensorValues.Add(DataPacket_1.ReferenceSensor);
                                    break;

                                case "2":
                                    gasSensorValues.Add(DataPacket_2.GainAdsVoltagesIIR[0]);
                                    referenceSensorValues.Add(DataPacket_2.GainAdsVoltagesIIR[1]);
                                    break;

                                case "3":
                                    gasSensorValues.Add(DataPacket_3.Ch0);
                                    referenceSensorValues.Add(DataPacket_3.Ch1);
                                    break;

                                default:
                                    Debug.WriteLine("Unknown DataPacketType.");
                                    break;
                            }

                            // Küçük bir gecikme ekleyerek CPU kullanımını optimize et
                            await Task.Delay(10, cancellationTokenSource.Token);
                        }

                        // Max ve Min değerleri hesapla ve Interface.Data'ya ata
                        if (gasSensorValues.Any())
                        {
                            Interface.Data.GasSensorMaxValue = gasSensorValues.Max();
                            Interface.Data.GasSensorMinValue = gasSensorValues.Min();
                        }

                        if (referenceSensorValues.Any())
                        {
                            Interface.Data.ReferenceSensorMaxValue = referenceSensorValues.Max();
                            Interface.Data.ReferenceSensorMinValue = referenceSensorValues.Min();
                        }
                    }
                }, cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Sensor processing task was canceled.");
            }
            finally
            {
                // İşlem durumu sıfırla ve kaynakları temizle
                isProcessing = false;
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }

        public void StopSensorProcessing()
        {
            // Eğer işlem çalışıyorsa iptal et
            if (isProcessing && cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }
        }

    }

    #region DeviceProperties Sınıfı
    public class DeviceProperties : BindableBase
    {
        public int SampleCount { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.Now;

        public DeviceProperties()
        {
            Status = DeviceStatus.Disconnected;
            BaudRate = 0;
            DataPacketType = "1";
            SampleCount = 0;
            DataSamplingFrequency = 0;
            PortName = string.Empty;
            ProductId = string.Empty;
            FirmwareVersion = string.Empty;
        }

        private DeviceStatus _status;
        public DeviceStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value); 
        }

        private int _dataSamplingFrequency;
        public int DataSamplingFrequency
        {
            get => _dataSamplingFrequency;
            set => SetProperty(ref _dataSamplingFrequency, value);
        }

        private string _portName;
        public string PortName
        {
            get => _portName;
            set => SetProperty(ref _portName, value);
        }

        private string _productId;
        public string ProductId
        {
            get => _productId;
            set => SetProperty(ref _productId, value);
        }

        private string _firmwareVersion;
        public string FirmwareVersion
        {
            get => _firmwareVersion;
            set => SetProperty(ref _firmwareVersion, value);
        }

        private int _baudRate;
        public int BaudRate
        {
            get => _baudRate;
            set => SetProperty(ref _baudRate, value);
        }

        private string _dataPacketType;
        public string DataPacketType
        {
            get => _dataPacketType;
            set => SetProperty(ref _dataPacketType, value);
        }
    }
    #endregion

    #region DeviceStatus Enum
    public enum DeviceStatus
    {
        Connected,
        Disconnected,
        Identified,
        Unidentified
    }
    #endregion
}
