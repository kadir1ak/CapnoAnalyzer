using System.Collections.ObjectModel;
using System.Windows;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.PlotModels;
using System;
using System.Collections.Generic;

namespace CapnoAnalyzer.Models.Device
{
    public class DeviceInterface : BindableBase
    {
        public DeviceInterface()
        {
            SampleMode = SampleMode.RMS;

            // Plot modellerini oluştur
            SensorPlot = new SensorChartModel(timeWindowSeconds: 10, maxFps: 40);
            CalculatedGasPlot = new CalculatedGasChartModel();

            Data = new AllDataPaket();
            DeviceData = new DeviceDataType();
            IncomingMessage = new ObservableCollection<string>();
        }

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
            }
            catch { }
        }
    }

    public enum SampleMode { RMS, PP, AVG }
}
