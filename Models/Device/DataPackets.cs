using System;
using CapnoAnalyzer.Helpers;

namespace CapnoAnalyzer.Models.Device
{
    // Veri Paketi Modu 1
    public class DataPacket_1 : BindableBase
    {
        // Constructor
        public DataPacket_1()
        {
            Time = 0.0;
            GasSensor = 0.0;
            ReferenceSensor = 0.0;
            Temperature = 0.0;
            Humidity = 0.0;
            Pressure = 0.0;
        }

        // Özellikler
        private double _time;
        public double Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
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
    }

    // Veri Paketi Modu 2
    public class DataPacket_2 : BindableBase
    {
        // Constructor
        public DataPacket_2()
        {
            Time = 0.0;
            AngVoltages = new double[3];
            AdsRawValues = new double[4];
            AdsVoltages = new double[4];
            GainAdsVoltagesF = new double[2];
            GainAdsVoltagesIIR = new double[2];
            IrStatus = false;
        }

        // Ortak Zaman Özelliği
        private double _time;
        public double Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }

        // Özellikler
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

        private bool _irStatus;
        public bool IrStatus
        {
            get => _irStatus;
            set => SetProperty(ref _irStatus, value);
        }
    }

    // Veri Paketi Modu 3
    public class DataPacket_3 : BindableBase
    {
        // Constructor
        public DataPacket_3()
        {
            Time = 0.0;
            Ch0 = 0.0;
            Ch1 = 0.0;
            Frame = 0;
            Emitter = 0;
        }

        // Özellikler
        private double _time;
        public double Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
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
    }

    // Veri Paketi Modu 4
    public class DataPacket_4 : BindableBase
    {
        // Constructor
        public DataPacket_4()
        {
            Time = 0.0;
            // Özellikler burada tanımlanacak
        }

        // Özellikler
        private double _time;
        public double Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }

        // Placeholder özellikler
        // Örnek: private double _exampleProperty;
        // public double ExampleProperty
        // {
        //     get => _exampleProperty;
        //     set => SetProperty(ref _exampleProperty, value);
        // }
    }
}
