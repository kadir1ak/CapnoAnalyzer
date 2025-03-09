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
                DeviceStatus = deviceStatus
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
            return (Properties.DeviceStatus == DeviceStatus.Connected || Properties.DeviceStatus == DeviceStatus.Identified) &&
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
                    Properties.DeviceStatus = DeviceStatus.Disconnected;
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
            DeviceStatus = DeviceStatus.Disconnected;
            BaudRate = 9600;
            SampleCount = 0;
        }

        public DeviceStatus DeviceStatus { get; set; }
        public int BaudRate { get; set; }
        public int DataSamplingFrequency { get; set; }
        public string ID { get; set; } = string.Empty;
        public string PortName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductModel { get; set; } = string.Empty;
        public string ManufactureDate { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
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
