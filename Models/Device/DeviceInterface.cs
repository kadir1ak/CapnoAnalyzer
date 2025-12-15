using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.PlotModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace CapnoAnalyzer.Models.Device
{
    public class DeviceInterface : BindableBase
    {

        #region Dual Channel Real Time Plot
        public DualChannelRealTimePlot CustomDualPlot { get; private set; }
        public List<DataPacket2Source> AvailableSources { get; } = System.Enum.GetValues(typeof(DataPacket2Source)).Cast<DataPacket2Source>().ToList();

        private DataPacket2Source _selectedChannel1 = DataPacket2Source.GainAdsVoltageIIR1;
        public DataPacket2Source SelectedChannel1
        {
            get => _selectedChannel1;
            set
            {
                if (SetProperty(ref _selectedChannel1, value))
                    UpdateCustomPlotTitles();
            }
        }

        private DataPacket2Source _selectedChannel2 = DataPacket2Source.GainAdsVoltageIIR2;
        public DataPacket2Source SelectedChannel2
        {
            get => _selectedChannel2;
            set
            {
                if (SetProperty(ref _selectedChannel2, value))
                    UpdateCustomPlotTitles();
            }
        }
        private void UpdateCustomPlotTitles()
        {
            CustomDualPlot?.SetTitles(SelectedChannel1.ToString(), SelectedChannel2.ToString());
        }

        public double GetDataValue(DataPacket_2 packet, DataPacket2Source source)
        {
            if (packet == null) return 0;
            switch (source)
            {
                case DataPacket2Source.CO2: return packet.CO2Value.Length > 0 ? packet.CO2Value[0] : 0;
                case DataPacket2Source.Temperature: return packet.BMEValue.Length > 0 ? packet.BMEValue[0] : 0;
                case DataPacket2Source.Humidity: return packet.BMEValue.Length > 1 ? packet.BMEValue[1] : 0;
                case DataPacket2Source.Pressure: return packet.BMEValue.Length > 2 ? packet.BMEValue[2] : 0;
                case DataPacket2Source.AngVoltage1: return packet.AngVoltages.Length > 0 ? packet.AngVoltages[0] : 0;
                case DataPacket2Source.AngVoltage2: return packet.AngVoltages.Length > 1 ? packet.AngVoltages[1] : 0;
                case DataPacket2Source.AdsRaw1: return packet.AdsRawValues.Length > 0 ? packet.AdsRawValues[0] : 0;
                case DataPacket2Source.AdsRaw2: return packet.AdsRawValues.Length > 1 ? packet.AdsRawValues[1] : 0;
                case DataPacket2Source.AdsVoltage1: return packet.AdsVoltages.Length > 0 ? packet.AdsVoltages[0] : 0;
                case DataPacket2Source.AdsVoltage2: return packet.AdsVoltages.Length > 1 ? packet.AdsVoltages[1] : 0;
                case DataPacket2Source.GainAdsVoltageF1: return packet.GainAdsVoltagesF.Length > 0 ? packet.GainAdsVoltagesF[0] : 0;
                case DataPacket2Source.GainAdsVoltageF2: return packet.GainAdsVoltagesF.Length > 1 ? packet.GainAdsVoltagesF[1] : 0;
                case DataPacket2Source.GainAdsVoltageIIR1: return packet.GainAdsVoltagesIIR.Length > 0 ? packet.GainAdsVoltagesIIR[0] : 0;
                case DataPacket2Source.GainAdsVoltageIIR2: return packet.GainAdsVoltagesIIR.Length > 1 ? packet.GainAdsVoltagesIIR[1] : 0;
                default: return 0;
            }
        }
        #endregion

        public DeviceInterface()
        {
            SampleMode = SampleMode.RMS;

            // Plot modellerini oluştur
            SensorPlot = new SensorChartModel(timeWindowSeconds: 10, maxFps: 40);
            CalculatedGasPlot = new CalculatedGasChartModel();

            // --- YENİ KOD: İki grafiği birbirine bağla ---
            SensorPlot.SyncedGasPlot = CalculatedGasPlot;
            // ---------------------------------------------

            Data = new AllDataPaket();
            DeviceData = new DeviceDataType();
            IncomingMessage = new ObservableCollection<string>();

            CustomDualPlot = new DualChannelRealTimePlot(timeWindowSeconds: 10);
            UpdateCustomPlotTitles();
        }

        #region Cihaz Ayarları Modeli
        /// <summary>
        /// Tüm cihaz ayarlarını tutan ana nesne. XAML bu nesneye bağlanır.
        /// </summary>
        public DeviceChannelSettings ChannelSettings { get; } = new DeviceChannelSettings();
        public DeviceFilterSettings FilterSettings { get; } = new DeviceFilterSettings();

        // ComboBox'ların içini dolduracak seçenek listeleri
        public List<string> GainOptions { get; } = new List<string>
        {
            "1", "2", "4", "8", "16", "32", "64", "128"
        };

        public List<string> SPSOptions { get; } = new List<string>
        {
            "20", "45", "90", "175", "330", "600", "1000"
        };

        public List<string> HpFilterOptions { get; } = new List<string>
        {
            "0.1", "0.2", "0.5", "1.0", "2.0", "2.5"
        };

        public List<string> LpFilterOptions { get; } = new List<string>
        {
            "6", "7", "8", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20"
        };


        #endregion

        public ObservableCollection<string> IncomingMessage
        {
            get => _incomingMessage;
            set => SetProperty(ref _incomingMessage, value);
        }
        private ObservableCollection<string> _incomingMessage;

        public string OutgoingMessage
        {
            get => _outgoingMessage;
            set => SetProperty(ref _outgoingMessage, value);
        }
        private string _outgoingMessage;

        public AllDataPaket Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }
        private AllDataPaket _data;

        public DeviceDataType DeviceData
        {
            get => _deviceData;
            set => SetProperty(ref _deviceData, value);
        }
        private DeviceDataType _deviceData;

        // Plot referansları
        public SensorChartModel SensorPlot
        {
            get => _sensorPlot;
            set { if (_sensorPlot != value) { _sensorPlot = value; OnPropertyChanged(); } }
        }
        private SensorChartModel _sensorPlot;

        public CalculatedGasChartModel CalculatedGasPlot
        {
            get => _calculatedGasPlot;
            set { if (_calculatedGasPlot != value) { _calculatedGasPlot = value; OnPropertyChanged(); } }
        }
        private CalculatedGasChartModel _calculatedGasPlot;

        // UI kontrolleri
        public IEnumerable<SampleMode> SampleModes => Enum.GetValues(typeof(SampleMode)) as IEnumerable<SampleMode>;

        public SampleMode SampleMode
        {
            get => _sampleMode;
            set { if (_sampleMode != value) { _sampleMode = value; OnPropertyChanged(nameof(SampleMode)); } }
        }
        private SampleMode _sampleMode;

        public int MaxValueSamplingTime
        {
            get => _maxValueSamplingTime;
            set => SetProperty(ref _maxValueSamplingTime, value);
        }
        private int _maxValueSamplingTime = 10;

        public int SampleTime
        {
            get => _sampleTime;
            set => SetProperty(ref _sampleTime, value);
        }
        private int _sampleTime = 10;

        public int TimeCount
        {
            get => _timeCount;
            set => SetProperty(ref _timeCount, value);
        }
        private int _timeCount = 0;

        public int SampleTimeCount
        {
            get => _sampleTimeCount;
            set => SetProperty(ref _sampleTimeCount, value);
        }
        private int _sampleTimeCount = 0;

        public int SampleTimeProgressBar
        {
            get => _sampleTimeProgressBar;
            set => SetProperty(ref _sampleTimeProgressBar, value);
        }
        private int _sampleTimeProgressBar = 0;

        public bool IsInputEnabled
        {
            get => _isInputEnabled;
            set => SetProperty(ref _isInputEnabled, value);
        }
        private bool _isInputEnabled = true;

        public void AddIncomingMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                IncomingMessage.Add(message);
                if (IncomingMessage.Count > 10) IncomingMessage.RemoveAt(0);
            }
        }

        public void SendOutgoingMessage()
        {
            if (!string.IsNullOrWhiteSpace(OutgoingMessage))
            {
                AddIncomingMessage($"Sent: {OutgoingMessage}");
                OutgoingMessage = string.Empty;
            }
        }

        /// <summary>UI tarafındaki alanları cihazdan çek (plot yok).</summary>
        public void SyncWithDevice(Device device)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (device == null) return;

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

                        UpdateCalibrationData(DeviceData, Data.GasSensor, Data.ReferenceSensor);
                    }
                    else if (device.Properties.DataPacketType == "2")
                    {
                        Data.Time = device.DataPacket_2.Time;
                        Data.GasSensor = device.DataPacket_2.GainAdsVoltagesIIR[0];
                        Data.ReferenceSensor = device.DataPacket_2.GainAdsVoltagesIIR[1];

                        Data.CO2Value = device.DataPacket_2.CO2Value;
                        Data.Temperature = device.DataPacket_2.BMEValue[0];
                        Data.Humidity = device.DataPacket_2.BMEValue[1];
                        Data.Pressure = device.DataPacket_2.BMEValue[2];

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

                        UpdateCalibrationData(DeviceData, Data.GasSensor, Data.ReferenceSensor);
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

                        UpdateCalibrationData(DeviceData, Data.GasSensor, Data.ReferenceSensor);
                    }

                    // Görünürlük
                    Data.DataPacket_1_Status = device.Properties.DataPacketType == "1" ? Visibility.Visible : Visibility.Collapsed;
                    Data.DataPacket_2_Status = device.Properties.DataPacketType == "2" ? Visibility.Visible : Visibility.Collapsed;
                    Data.DataPacket_3_Status = device.Properties.DataPacketType == "3" ? Visibility.Visible : Visibility.Collapsed;
                });
            }
            catch { }
        }

        public void UpdateCalibrationData(DeviceDataType deviceData, double gasSensor, double referenceSensor)
        {
            try
            {
                if (referenceSensor == 0 || deviceData?.CalibrationData == null ||
                    deviceData?.CalibrationCoefficients == null || deviceData.CalibrationData.Zero == 0)
                    return;

                var calData = deviceData.CalibrationData;
                var coeff = deviceData.CalibrationCoefficients;

                calData.Gas = gasSensor;
                calData.Ref = referenceSensor;

                calData.Ratio = gasSensor / referenceSensor;
                calData.Transmittance = calData.Ratio / calData.Zero;
                calData.Absorption = 1 - calData.Transmittance;

                if (calData.GasConcentration > 0)
                    calData.PredictedAbsorption = coeff.A * (1 - Math.Exp(-coeff.B * Math.Pow(calData.GasConcentration, coeff.C)));
                else
                    calData.PredictedAbsorption = 0;

                calData.PredictedGasConcentration = double.NaN;
                if (calData.Absorption > 0 && calData.Absorption < coeff.A)
                {
                    calData.PredictedGasConcentration = Math.Pow(
                        -Math.Log(1 - (calData.Absorption / coeff.A)) / coeff.B,
                        1 / coeff.C);
                }

                calData.GasConcentration = calData.PredictedGasConcentration;

                // ✅ PC tarafında hesaplanan gaz konsantrasyonunu grafiğe ekle
                if (!double.IsNaN(calData.PredictedGasConcentration))
                {
                    CalculatedGasPlot?.AddPcCalculatedData(deviceData.SensorData.Time, calData.PredictedGasConcentration);
                }
            }
            catch { }
        }
    }

    public enum SampleMode { RMS, PP, AVG }

    /// <summary>
    /// Tek bir kanalın (Ch0 veya Ch1) ayarlarını temsil eder.
    /// Bu sınıf, kanal bazlı yapılandırma verilerini tutar.
    /// </summary>
    public class ChannelSettings : BindableBase
    {
        public ChannelSettings()
        {
            // UI kontrolleri için varsayılan değerleri ayarla
            Gain = "16";
            HpFilter = "2.0";
            SPS = "90";
            LpFilter = "10";
        }

        private string _gain;
        /// <summary>
        /// Kanalın kazanç (Gain) ayarı.
        /// </summary>
        public string Gain
        {
            get => _gain;
            set => SetProperty(ref _gain, value);
        }

        private string _hpFilter;
        /// <summary>
        /// Kanalın Yüksek Geçiren Filtre (High-Pass Filter) ayarı.
        /// </summary>
        public string HpFilter
        {
            get => _hpFilter;
            set => SetProperty(ref _hpFilter, value);
        }

        private string _sps;
        /// <summary>
        /// Kanalın Geçirgenlik (SPS) ayarı.
        /// </summary>
        public string SPS
        {
            get => _sps;
            set => SetProperty(ref _sps, value);
        }

        private string _lpFilter;
        /// <summary>
        /// Kanalın Alçak Geçiren Filtre (Low-Pass Filter) ayarı.
        /// </summary>
        public string LpFilter
        {
            get => _lpFilter;
            set => SetProperty(ref _lpFilter, value);
        }
    }

    /// <summary>
    /// Cihazın tüm yapılandırılabilir ayarlarını (kanallar ve emitter) bir arada tutan ana model sınıfı.
    /// Bu sınıf, UI'daki "Cihaz Ayarları" panelinin veri bağlamı (DataContext) olarak kullanılır.
    /// </summary>
    public class DeviceChannelSettings : BindableBase
    {
        public DeviceChannelSettings()
        {
            // Varsayılanlar
            EmitterOnTime = 50;
            EmitterOffTime = 50;
            EmitterSettings = 10;

            // Kanallar
            Ch0 = new ChannelSettings();
            Ch1 = new ChannelSettings();

            // Değişiklikleri dinle ve eşitle
            Ch0.PropertyChanged += OnCh0Changed;
            Ch1.PropertyChanged += OnCh1Changed;
        }

        private bool _syncing;

        // EŞİTLEME: Ch0 → Ch1
        private void OnCh0Changed(object? sender, PropertyChangedEventArgs e)
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                if (e.PropertyName == nameof(ChannelSettings.Gain))
                {
                    if (Ch1.Gain != Ch0.Gain)
                        Ch1.Gain = Ch0.Gain;
                }
                else if (e.PropertyName == nameof(ChannelSettings.SPS)) // SPS
                {
                    if (Ch1.SPS != Ch0.SPS)
                        Ch1.SPS = Ch0.SPS;
                }
            }
            finally { _syncing = false; }
        }

        // EŞİTLEME: Ch1 → Ch0
        private void OnCh1Changed(object? sender, PropertyChangedEventArgs e)
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                if (e.PropertyName == nameof(ChannelSettings.Gain))
                {
                    if (Ch0.Gain != Ch1.Gain)
                        Ch0.Gain = Ch1.Gain;
                }
                else if (e.PropertyName == nameof(ChannelSettings.SPS)) // SPS
                {
                    if (Ch0.SPS != Ch1.SPS)
                        Ch0.SPS = Ch1.SPS;
                }
            }
            finally { _syncing = false; }
        }

        // --- mevcut alanlar ---
        public ChannelSettings Ch0 { get; set; }
        public ChannelSettings Ch1 { get; set; }

        private double _emitterSetting;
        public double EmitterSettings
        {
            get => _emitterSetting;
            set => SetProperty(ref _emitterSetting, value);
        }

        private double _emitterOnTime;
        public double EmitterOnTime
        {
            get => _emitterOnTime;
            set => SetProperty(ref _emitterOnTime, value);
        }

        private double _emitterOffTime;
        public double EmitterOffTime
        {
            get => _emitterOffTime;
            set => SetProperty(ref _emitterOffTime, value);
        }
    }

    public class DeviceFilterSettings : BindableBase
    {
        public DeviceFilterSettings()
        {
            // Varsayılanlar
            RmsWindowSize = 10;
            MavFilterSize = 10;
        }
        private int _rmsWindowSize;
        public int RmsWindowSize
        {
            get => _rmsWindowSize;
            set => SetProperty(ref _rmsWindowSize, value);
        }

        private int _mavFilterSize;
        public int MavFilterSize
        {
            get => _mavFilterSize;
            set => SetProperty(ref _mavFilterSize, value);
        }
    }

    #region ComboBox'lar için kaynak listesi
    // Veri kaynaklarını tanımlayan Enum
    public enum DataPacket2Source
    {
        None,
        CO2,
        Temperature,
        Humidity,
        Pressure,
        AngVoltage1,
        AngVoltage2,
        AdsRaw1,
        AdsRaw2,
        AdsVoltage1,
        AdsVoltage2,
        GainAdsVoltageF1,
        GainAdsVoltageF2,
        GainAdsVoltageIIR1,
        GainAdsVoltageIIR2
    }

    #endregion
}
