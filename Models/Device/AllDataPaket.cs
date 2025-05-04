using CapnoAnalyzer.Helpers;
using System;
using System.Windows;

namespace CapnoAnalyzer.Models.Device
{
    public class AllDataPaket : BindableBase
    {
        // Ortak Zaman Özelliği
        private double _time;
        public double Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }
        // GasSensorBandValue Özelliği
        private double _gasSensorBandValue;
        public double GasSensorBandValue
        {
            get => _gasSensorBandValue;
            set => SetProperty(ref _gasSensorBandValue, value);
        }

        // GasSensorMaxValue Özelliği
        private double _gasSensorMaxValue;
        public double GasSensorMaxValue
        {
            get => _gasSensorMaxValue;
            set => SetProperty(ref _gasSensorMaxValue, value);
        }

        // GasSensorMinValue Özelliği
        private double _gasSensorMinValue;
        public double GasSensorMinValue
        {
            get => _gasSensorMinValue;
            set => SetProperty(ref _gasSensorMinValue, value);
        }

        // ReferenceSensorBandValue Özelliği
        private double _referenceSensorBandValue;
        public double ReferenceSensorBandValue
        {
            get => _referenceSensorBandValue;
            set => SetProperty(ref _referenceSensorBandValue, value);
        }

        // ReferenceSensorMaxValue Özelliği
        private double _referenceSensorMaxValue;
        public double ReferenceSensorMaxValue
        {
            get => _referenceSensorMaxValue;
            set => SetProperty(ref _referenceSensorMaxValue, value);
        }

        // ReferenceSensorMinValue Özelliği
        private double _referenceSensorMinValue;
        public double ReferenceSensorMinValue
        {
            get => _referenceSensorMinValue;
            set => SetProperty(ref _referenceSensorMinValue, value);
        }

        // DataPacket_1 İçeriği
        private Visibility _dataPacket_1_Status;
        public Visibility DataPacket_1_Status
        {
            get => _dataPacket_1_Status;
            set => SetProperty(ref _dataPacket_1_Status, value);
        }
        private double _gasSensor;
        public double GasSensor
        {
            get => _gasSensor;
            set => SetProperty(ref _gasSensor, value);
        }

        private double _referenceSensor;
        public double ReferenceSensor
        {
            get => _referenceSensor;
            set => SetProperty(ref _referenceSensor, value);
        }

        private double _temperature;
        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        private double _humidity;
        public double Humidity
        {
            get => _humidity;
            set => SetProperty(ref _humidity, value);
        }

        private double _pressure;
        public double Pressure
        {
            get => _pressure;
            set => SetProperty(ref _pressure, value);
        }

        // DataPacket_2 İçeriği
        private Visibility _dataPacket_2_Status;
        public Visibility DataPacket_2_Status
        {
            get => _dataPacket_2_Status;
            set => SetProperty(ref _dataPacket_2_Status, value);
        }

        private double[] _angVoltages;
        public double[] AngVoltages
        {
            get => _angVoltages;
            set => SetProperty(ref _angVoltages, value);
        }

        private double[] _adsRawValues;
        public double[] AdsRawValues
        {
            get => _adsRawValues;
            set => SetProperty(ref _adsRawValues, value);
        }

        private double[] _adsVoltages;
        public double[] AdsVoltages
        {
            get => _adsVoltages;
            set => SetProperty(ref _adsVoltages, value);
        }

        private double[] _gainAdsVoltagesF;
        public double[] GainAdsVoltagesF
        {
            get => _gainAdsVoltagesF;
            set => SetProperty(ref _gainAdsVoltagesF, value);
        }

        private double[] _gainAdsVoltagesIIR;
        public double[] GainAdsVoltagesIIR
        {
            get => _gainAdsVoltagesIIR;
            set => SetProperty(ref _gainAdsVoltagesIIR, value);
        }

        private int _irStatus;
        public int IrStatus
        {
            get => _irStatus;
            set => SetProperty(ref _irStatus, value);
        }

        // DataPacket_3 İçeriği
        private Visibility _dataPacket_3_Status;
        public Visibility DataPacket_3_Status
        {
            get => _dataPacket_3_Status;
            set => SetProperty(ref _dataPacket_3_Status, value);
        }

        private double _ch0;
        public double Ch0
        {
            get => _ch0;
            set => SetProperty(ref _ch0, value);
        }

        private double _ch1;
        public double Ch1
        {
            get => _ch1;
            set => SetProperty(ref _ch1, value);
        }

        private int _frame;
        public int Frame
        {
            get => _frame;
            set => SetProperty(ref _frame, value);
        }

        private int _emitter;
        public int Emitter
        {
            get => _emitter;
            set => SetProperty(ref _emitter, value);
        }

        // Constructor
        public AllDataPaket()
        {
            // DataPacket_1 Varsayılan Değerler
            Time = 0.0;
            DataPacket_1_Status = Visibility.Collapsed;
            GasSensor = 0.0;
            ReferenceSensor = 0.0;
            Temperature = 0.0;
            Humidity = 0.0;
            Pressure = 0.0;

            // DataPacket_2 Varsayılan Değerler
            Time = 0.0;
            DataPacket_2_Status = Visibility.Collapsed;  
            AngVoltages = new double[3]; // 3 elemanlı sıfır dizisi
            AdsRawValues = new double[4]; // 4 elemanlı sıfır dizisi
            AdsVoltages = new double[4]; // 4 elemanlı sıfır dizisi
            GainAdsVoltagesF = new double[2]; // 2 elemanlı sıfır dizisi
            GainAdsVoltagesIIR = new double[2]; // 2 elemanlı sıfır dizisi
            IrStatus = 0;

            // DataPacket_3 Varsayılan Değerler
            Time = 0.0;
            DataPacket_3_Status = Visibility.Collapsed;
            Ch0 = 0.0;
            Ch1 = 0.0;
            Frame = 0;
            Emitter = 0;
        }
    }
}
