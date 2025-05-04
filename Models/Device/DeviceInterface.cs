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
            SensorPlot = new SensorChartModel(); // **Her cihaz için yeni bir `PlotModel`**
            CalculatedGasPlot = new CalculatedGasChartModel(); // **Her cihaz için yeni bir `PlotModel`**
            Data = new AllDataPaket();
            DeviceData = new DeviceDataType();
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

        private DeviceDataType _deviceData;
        public DeviceDataType DeviceData
        {
            get => _deviceData;
            set => SetProperty(ref _deviceData, value);
        }

        /// <summary>
        /// Grafik modeli (Her cihaz için ayrı bir `DevicePlot` oluşturuluyor).
        /// </summary>
        private SensorChartModel _sensorPlot;
        public SensorChartModel SensorPlot
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
        
        private CalculatedGasChartModel _calculatedGasPlot;
        public CalculatedGasChartModel CalculatedGasPlot
        {
            get => _calculatedGasPlot;
            set
            {
                if (_calculatedGasPlot != value)
                {
                    _calculatedGasPlot = value;
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
        public void UpdateSensorPlot()
        {
            if (SensorPlot != null)
            {
                SensorPlot.AddDataPoint(Data.Time, Data.GasSensor, Data.ReferenceSensor);
               
            }
        }
        public void UpdateCalculatedGasPlot()
        {
            if (CalculatedGasPlot != null)
            {
                //CalculatedGasPlot.AddDataPoint(Data.Time, Data.GasSensor);
                CalculatedGasPlot.AddDataPoint(DeviceData.SensorData.Time, DeviceData.SensorData.IIR_Gas_Voltage);
            }
        }

        /// <summary>
        /// Gerçek verilerle arayüz verilerini günceller.
        /// </summary>
        public void SyncWithDevice(Device device)
        {
            try
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

                        DeviceData.SensorData.Time = device.DeviceData.SensorData.Time;
                        DeviceData.SensorData.IIR_Gas_Voltage = device.DeviceData.SensorData.IIR_Gas_Voltage;
                        DeviceData.SensorData.IIR_Ref_Voltage = device.DeviceData.SensorData.IIR_Ref_Voltage;
                        DeviceData.SensorData.IR_Status = device.DeviceData.SensorData.IR_Status;
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

                        DeviceData.SensorData.Time = device.DeviceData.SensorData.Time;
                        DeviceData.SensorData.IIR_Gas_Voltage = device.DeviceData.SensorData.IIR_Gas_Voltage;
                        DeviceData.SensorData.IIR_Ref_Voltage = device.DeviceData.SensorData.IIR_Ref_Voltage;
                        DeviceData.SensorData.IR_Status = device.DeviceData.SensorData.IR_Status;

                        DeviceData.CalibrationCoefficients.A = device.DeviceData.CalibrationCoefficients.A;
                        DeviceData.CalibrationCoefficients.B = device.DeviceData.CalibrationCoefficients.B;
                        DeviceData.CalibrationCoefficients.C = device.DeviceData.CalibrationCoefficients.C;
                        DeviceData.CalibrationCoefficients.R = device.DeviceData.CalibrationCoefficients.R;

                        DeviceData.CalibrationData.Sample = device.DeviceData.CalibrationData.Sample;
                        DeviceData.CalibrationData.GasConcentration = device.DeviceData.CalibrationData.GasConcentration;
                        DeviceData.CalibrationData.Ref = device.DeviceData.CalibrationData.Ref;
                        DeviceData.CalibrationData.Gas = device.DeviceData.CalibrationData.Gas;
                        DeviceData.CalibrationData.Ratio = device.DeviceData.CalibrationData.Ratio;
                        DeviceData.CalibrationData.Transmittance = device.DeviceData.CalibrationData.Transmittance;
                        DeviceData.CalibrationData.Absorption = device.DeviceData.CalibrationData.Absorption;
                        DeviceData.CalibrationData.PredictedAbsorption = device.DeviceData.CalibrationData.PredictedAbsorption;
                        DeviceData.CalibrationData.PredictedGasConcentration = device.DeviceData.CalibrationData.PredictedGasConcentration;
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

                        DeviceData.SensorData.Time = device.DeviceData.SensorData.Time;
                        DeviceData.SensorData.IIR_Gas_Voltage = device.DeviceData.SensorData.IIR_Gas_Voltage;
                        DeviceData.SensorData.IIR_Ref_Voltage = device.DeviceData.SensorData.IIR_Ref_Voltage;
                        DeviceData.SensorData.IR_Status = device.DeviceData.SensorData.IR_Status;
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
                    UpdateSensorPlot();
                    UpdateCalculatedGasPlot();
                });
                /*
                DeviceData.SensorData.Time = device.DeviceData.SensorData.Time;
                DeviceData.SensorData.IIR_Gas_Voltage = device.DeviceData.SensorData.IIR_Gas_Voltage;
                DeviceData.SensorData.IIR_Ref_Voltage = device.DeviceData.SensorData.IIR_Ref_Voltage;
                DeviceData.SensorData.IR_Status = device.DeviceData.SensorData.IR_Status;
                */
            }
            catch (Exception) { }
        }
    }
    public enum SampleMode
    {
        RMS,
        PP,
        AVG
    }
}
