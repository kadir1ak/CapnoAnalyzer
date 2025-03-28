﻿using System;
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
