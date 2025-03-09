using System;
using System.Collections.ObjectModel;
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
        #endregion

        #region Constructor
        public Device(SerialPortsManager manager, string portName, DeviceStatus deviceStatus)
        {
            _portManager = manager;
            _portManager.DeviceDisconnected += OnDeviceDisconnected;

            Properties = new DeviceProperties
            {
                PortName = portName,
                Status = deviceStatus
            };

            SendMessageCommand = new DeviceRelayCommand(SendMessage, CanSendMessage);
            AutoSendMessageCommand = new DeviceRelayCommand(ToggleAutoSend);
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

        #region Otomatik Mesaj Gönderme
        private bool _autoSendActive;
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

        private DeviceInterface _interface = new();
        public DeviceInterface Interface
        {
            get => _interface;
            set => SetProperty(ref _interface, value);
        }

        private Sensor _sensor = new();
        public Sensor Sensor
        {
            get => _sensor;
            set => SetProperty(ref _sensor, value);
        }

        private DeviceProperties _properties = new();
        public DeviceProperties Properties
        {
            get => _properties;
            set => SetProperty(ref _properties, value);
        }
        #endregion
    }

    #region DeviceProperties Sınıfı
    public class DeviceProperties : BindableBase
    {
        public int SampleCount { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.Now;

        public DeviceProperties()
        {
            Status = DeviceStatus.Disconnected;
            BaudRate = 921600;
            SampleCount = 0;
            DataSamplingFrequency = 0;
        }

        private DeviceStatus _status = new();
        public DeviceStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private int _dataSamplingFrequency = new();
        public int DataSamplingFrequency
        {
            get => _dataSamplingFrequency;
            set => SetProperty(ref _dataSamplingFrequency, value);
        }

        public string ID { get; set; } = string.Empty;
        public string PortName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductModel { get; set; } = string.Empty;
        public string ManufactureDate { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
        public int BaudRate { get; set; }
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
