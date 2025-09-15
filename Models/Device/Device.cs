using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
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
        public ICommand SendCalibrationCoefficientsCommand { get; }
        public ICommand SendSettingsCommand { get; }
        #endregion

        #region Constructor
        public Device(SerialPortsManager manager, string portName, DeviceStatus deviceStatus)
        {
            _portManager = manager;
            _portManager.DeviceDisconnected += OnDeviceDisconnected;
            _portManager.MessageReceived += OnMessageReceived;

            Properties = new DeviceProperties { PortName = portName, Status = deviceStatus };

            SendMessageCommand = new DeviceRelayCommand(SendMessage, CanSendMessage);
            AutoSendMessageCommand = new DeviceRelayCommand(ToggleAutoSend);
            AutoSaveDataCommand = new DeviceRelayCommand(ToggleAutoSaveData);
            SendCalibrationCoefficientsCommand = new DeviceRelayCommand(SendCalibrationCoefficients, CanSendCalibrationCoefficients);
            SendSettingsCommand = new DeviceRelayCommand(SendSettings, CanSendSettings);

            // Not: Grafik kayıt/servisi kaldırıldı. Plot yönetimi DeviceDataParser → SensorPlot.Enqueue ile yapılır.

            InitializeDeviceAsync();
        }

        public void DisposeDevice()
        {
            StopAutoSend();
            _portManager.DeviceDisconnected -= OnDeviceDisconnected;
            _portManager.MessageReceived -= OnMessageReceived;

            // Plot modelinin timer'ını temizle
            Interface?.SensorPlot?.Dispose();
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
        private StreamWriter _autoSaveWriter;
        private string _autoSaveFilePath;
        private bool _autoSaveDataActive;

        public bool AutoSaveDataActive
        {
            get => _autoSaveDataActive;
            set => SetProperty(ref _autoSaveDataActive, value);
        }

        public void ToggleAutoSaveData()
        {
            if (AutoSaveDataActive)
                StopAutoSaveData();
            else
                StartAutoSaveData();
        }

        private void StartAutoSaveData()
        {
            if (AutoSaveDataActive) return;

            AutoSaveDataActive = true;
            SetAutoSaveFilePath();

            _autoSaveWriter = new StreamWriter(_autoSaveFilePath, append: false);
            _autoSaveWriter.WriteLine($"=== Log Start for {Properties.PortName} at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _autoSaveWriter.Flush();
        }

        private void StopAutoSaveData()
        {
            if (!AutoSaveDataActive) return;

            AutoSaveDataActive = false;

            if (_autoSaveWriter != null)
            {
                _autoSaveWriter.WriteLine($"=== Log End at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                _autoSaveWriter.Flush();
                _autoSaveWriter.Close();
                _autoSaveWriter.Dispose();
                _autoSaveWriter = null;
            }
        }

        private void SetAutoSaveFilePath()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string logFolder = System.IO.Path.Combine(desktopPath, "CapnoLogs");
            Directory.CreateDirectory(logFolder);

            string fileName = $"{Properties.PortName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            _autoSaveFilePath = System.IO.Path.Combine(logFolder, fileName);
        }

        private void OnMessageReceived(string receivedPortName, string data)
        {
            if (Properties.PortName == receivedPortName)
            {
                if (AutoSaveDataActive && _autoSaveWriter != null)
                {
                    string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    _autoSaveWriter.WriteLine($"[{timeStamp}] {data}");
                    _autoSaveWriter.Flush();
                }
            }
        }
        #endregion

        #region Cihaz Ayarları ve Kalibrasyon Komutları

        // "Ayarları Gönder" butonu için komut. UI'daki tüm ayarları tek bir metin olarak cihaza yollar.
        private void SendSettings()
        {
            var settings = Interface.ChannelSettings;

            // Ayarları modelden oku
            var emitterOn = settings.EmitterOnTime;
            var emitterOff = settings.EmitterOffTime;
            var gain0 = ParseSettingValue(settings.Ch0.Gain);
            var hpFilter0 = ParseSettingValue(settings.Ch0.HpFilter);
            var trans0 = ParseSettingValue(settings.Ch0.Transmittance);
            var lpFilter0 = ParseSettingValue(settings.Ch0.LpFilter);
            var gain1 = ParseSettingValue(settings.Ch1.Gain);
            var hpFilter1 = ParseSettingValue(settings.Ch1.HpFilter);
            var trans1 = ParseSettingValue(settings.Ch1.Transmittance);
            var lpFilter1 = ParseSettingValue(settings.Ch1.LpFilter);

            // Donanımın anlayacağı "CFG,..." formatında komutu oluştur
            string commandString = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "CFG,{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                emitterOn, emitterOff,
                gain0, hpFilter0, trans0, lpFilter0,
                gain1, hpFilter1, trans1, lpFilter1);

            // Komutu seri porttan gönder
            _portManager.SendMessage(Properties.PortName, commandString);
        }

        // "Katsayıları Gönder" butonu için komut. Kalibrasyon verilerini cihaza yollar.
        private void SendCalibrationCoefficients()
        {
            // Kalibrasyon katsayılarını modelden oku
            var A = Interface.DeviceData.CalibrationCoefficients.A;
            var B = Interface.DeviceData.CalibrationCoefficients.B;
            var C = Interface.DeviceData.CalibrationCoefficients.C;
            var R = Interface.DeviceData.CalibrationCoefficients.R;
            var Zero = Interface.DeviceData.CalibrationData.Zero;

            // Donanımın anlayacağı "CV,..." formatında komutu oluştur
            string commandString = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "CV,{0},{1},{2},{3},{4}", A, B, C, R, Zero);

            // Komutu seri porttan gönder
            _portManager.SendMessage(Properties.PortName, commandString);
        }

        // Komut gönderme butonlarının aktif olup olmayacağını belirler.
        private bool CanSendSettings()
        {
            return Properties.Status == DeviceStatus.Connected || Properties.Status == DeviceStatus.Identified;
        }

        // Komut gönderme butonlarının aktif olup olmayacağını belirler.
        private bool CanSendCalibrationCoefficients()
        {
            return Properties.Status == DeviceStatus.Connected || Properties.Status == DeviceStatus.Identified;
        }

        // "180Hz" gibi metinleri 180.0 gibi sayılara çeviren yardımcı metot.
        private double ParseSettingValue(string input)
        {
            if (string.IsNullOrEmpty(input)) return 0;

            string numericPart = new string(input.Where(c => char.IsDigit(c) || c == '.').ToArray());

            // CultureInfo.InvariantCulture, ondalık ayraç olarak her zaman nokta (.) kullanılmasını sağlar.
            if (double.TryParse(numericPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }

            return 0; // Çevirme başarısız olursa 0 döner.
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
            if (AutoSendActive) StopAutoSend();
            else StartAutoSend();
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
                    await Task.Delay(10, token); // 10ms
                }
            }
            catch (TaskCanceledException) { /* ignore */ }
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
        #endregion

        #region Özellikler
        private ObservableCollection<string> _incomingMessage = new();
        public ObservableCollection<string> IncomingMessage
        {
            get => _incomingMessage;
            set => SetProperty(ref _incomingMessage, value);
        }

        private DeviceDataType _deviceData = new();
        public DeviceDataType DeviceData
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

        private bool isProcessing = false;
        private CancellationTokenSource cancellationTokenSource = null;

        public async Task StartSensorProcessingAsync()
        {
            if (isProcessing)
            {
                Debug.WriteLine("Sensor processing is already running.");
                return;
            }

            isProcessing = true;
            cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Task.Run(async () =>
                {
                    while (!cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        var samplingStartTime = DateTime.Now;
                        var gasSensorValues = new List<double>();
                        var referenceSensorValues = new List<double>();

                        while ((DateTime.Now - samplingStartTime).TotalSeconds < Interface.MaxValueSamplingTime)
                        {
                            if (cancellationTokenSource.Token.IsCancellationRequested)
                                break;

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

                            await Task.Delay(10, cancellationTokenSource.Token);
                        }

                        if (gasSensorValues.Any())
                        {
                            Interface.Data.GasSensorMaxValue = gasSensorValues.Max();
                            Interface.Data.GasSensorMinValue = gasSensorValues.Min();
                            Interface.Data.GasSensorBandValue = Math.Abs(Interface.Data.GasSensorMaxValue - Interface.Data.GasSensorMinValue);
                        }

                        if (referenceSensorValues.Any())
                        {
                            Interface.Data.ReferenceSensorMaxValue = referenceSensorValues.Max();
                            Interface.Data.ReferenceSensorMinValue = referenceSensorValues.Min();
                            Interface.Data.ReferenceSensorBandValue = Math.Abs(Interface.Data.ReferenceSensorMaxValue - Interface.Data.ReferenceSensorMinValue);
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
                isProcessing = false;
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
            }
        }

        public void StopSensorProcessing()
        {
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
