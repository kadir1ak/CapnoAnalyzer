using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.Windows;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.PlotModels;

namespace CapnoAnalyzer.Models.Device
{
    public class DeviceInterface : BindableBase
    {
        /// <summary>
        /// Constructor: Her cihaz için ayrı `DevicePlot` ve `Sensor` nesneleri oluşturur.
        /// </summary>
        public DeviceInterface()
        {
            SampleMode = SampleMode.RMS;
            SensorPlot = new DevicePlot(); // **Her cihaz için yeni bir `PlotModel`**
            Data = new AllDataPaket();
            IncomingMessage = new ObservableCollection<string>();
        }

        /// <summary>
        /// Gelen mesajları tutmak için ObservableCollection.
        /// </summary>
        private ObservableCollection<string> _incomingMessage;
        public ObservableCollection<string> IncomingMessage
        {
            get => _incomingMessage;
            set => SetProperty(ref _incomingMessage, value);
        }

        /// <summary>
        /// UI üzerinden gönderilecek geçici metin.
        /// </summary>
        private string _outgoingMessage;
        public string OutgoingMessage
        {
            get => _outgoingMessage;
            set => SetProperty(ref _outgoingMessage, value);
        }

        /// <summary>
        /// Sensor nesnesi (veri kaynağı).
        /// </summary>
        private AllDataPaket _data;
        public AllDataPaket Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }   

        /// <summary>
        /// Grafik modeli (Her cihaz için ayrı bir `DevicePlot` oluşturuluyor).
        /// </summary>
        private DevicePlot _sensorPlot;
        public DevicePlot SensorPlot
        {
            get => _sensorPlot;
            set
            {
                if (_sensorPlot != value)
                {
                    _sensorPlot = value;
                    OnPropertyChanged();
                }
            }
        }

        private SampleMode _sampleMode;
        public IEnumerable<SampleMode> SampleModes => System.Enum.GetValues(typeof(SampleMode)) as IEnumerable<SampleMode>;
        public SampleMode SampleMode
        {
            get => _sampleMode;
            set
            {
                if (_sampleMode != value)
                {
                    _sampleMode = value;
                    OnPropertyChanged(nameof(SampleMode));
                }
            }
        }

        private int _maxValueSamplingTime = 10;
        public int MaxValueSamplingTime
        {
            get => _maxValueSamplingTime;
            set => SetProperty(ref _maxValueSamplingTime, value);
        }

        private int _sampleTime = 10;
        public int SampleTime
        {
            get => _sampleTime;
            set => SetProperty(ref _sampleTime, value);
        }

        private int _timeCount = 0;
        public int TimeCount
        {
            get => _timeCount;
            set => SetProperty(ref _timeCount, value);
        }

        private int _sampleTimeCount = 0;
        public int SampleTimeCount
        {
            get => _sampleTimeCount;
            set => SetProperty(ref _sampleTimeCount, value);
        }

        private int _sampleTimeProgressBar = 0;
        public int SampleTimeProgressBar
        {
            get => _sampleTimeProgressBar;
            set => SetProperty(ref _sampleTimeProgressBar, value);
        }

        private bool _isInputEnabled = true;
        public bool IsInputEnabled
        {
            get => _isInputEnabled;
            set => SetProperty(ref _isInputEnabled, value);
        }
        /// <summary>
        /// Gelen mesajları listeye ekler.
        /// </summary>
        public void AddIncomingMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                IncomingMessage.Add(message);
                if (IncomingMessage.Count > 10)
                {
                    IncomingMessage.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Kullanıcının yazdığı mesajı gönderir.
        /// </summary>
        public void SendOutgoingMessage()
        {
            if (!string.IsNullOrWhiteSpace(OutgoingMessage))
            {
                // Mesajı gönder (örneğin bir cihazla iletişim kur)
                AddIncomingMessage($"Sent: {OutgoingMessage}");
                OutgoingMessage = string.Empty; // Mesaj gönderildikten sonra temizle
            }
        }

        /// <summary>
        /// Sensör verilerini grafik modeline ekler.
        /// </summary>
        public void UpdatePlot()
        {
            if (SensorPlot != null)
            {
                SensorPlot.AddDataPoint(Data.Time, Data.GasSensor, Data.ReferenceSensor);
            }
        }

        /// <summary>
        /// Gerçek verilerle arayüz verilerini günceller.
        /// </summary>
        public void SyncWithDevice(Device device)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (device == null) return;

                // Arayüzde gösterilecek verileri güncelle
                if (device.Properties.DataPacketType == "1")
                {
                    Data.Time = device.DataPacket_1.Time;
                    Data.GasSensor = device.DataPacket_1.GasSensor;
                    Data.ReferenceSensor = device.DataPacket_1.ReferenceSensor;
                    Data.Temperature = device.DataPacket_1.Temperature;
                    Data.Humidity = device.DataPacket_1.Humidity;
                }
                else if (device.Properties.DataPacketType == "2")
                {
                    Data.Time = device.DataPacket_2.Time;
                    Data.GasSensor = device.DataPacket_2.GainAdsVoltagesIIR[0];
                    Data.ReferenceSensor = device.DataPacket_2.GainAdsVoltagesIIR[1];

                    Data.AngVoltages = device.DataPacket_2.AngVoltages;
                    Data.AdsRawValues = device.DataPacket_2.AdsRawValues;
                    Data.AdsVoltages = device.DataPacket_2.AdsVoltages;
                    Data.GainAdsVoltagesF = device.DataPacket_2.GainAdsVoltagesF;
                    Data.GainAdsVoltagesIIR = device.DataPacket_2.GainAdsVoltagesIIR;
                    Data.IrStatus = device.DataPacket_2.IrStatus;
                }
                else if (device.Properties.DataPacketType == "3")
                {
                    Data.Time = device.DataPacket_3.Time;
                    Data.GasSensor = device.DataPacket_3.Ch0;
                    Data.ReferenceSensor = device.DataPacket_3.Ch1;

                    Data.Ch0 = device.DataPacket_3.Ch0;
                    Data.Ch1 = device.DataPacket_3.Ch1;
                    Data.Frame = device.DataPacket_3.Frame;
                    Data.Emitter = device.DataPacket_3.Emitter;
                }

                // 
                if (device.Properties.DataPacketType == "1")
                {
                    Data.DataPacket_1_Status = Visibility.Visible;
                    Data.DataPacket_3_Status = Visibility.Collapsed;
                    Data.DataPacket_3_Status = Visibility.Collapsed;
                }
                else if (device.Properties.DataPacketType == "2")
                {
                    Data.DataPacket_1_Status = Visibility.Collapsed;
                    Data.DataPacket_2_Status = Visibility.Visible;
                    Data.DataPacket_3_Status = Visibility.Collapsed;
                }
                else if (device.Properties.DataPacketType == "3")
                {
                    Data.DataPacket_1_Status = Visibility.Collapsed;
                    Data.DataPacket_2_Status = Visibility.Collapsed;
                    Data.DataPacket_3_Status = Visibility.Visible;
                }
                else
                {
                    Data.DataPacket_1_Status = Visibility.Collapsed;
                    Data.DataPacket_2_Status = Visibility.Collapsed;
                    Data.DataPacket_3_Status = Visibility.Collapsed;
                }

                // Grafiği güncelle
                UpdatePlot();
            });         
        }       
    }
    public enum SampleMode
    {
        RMS,
        PP,
        AVG
    }
}
