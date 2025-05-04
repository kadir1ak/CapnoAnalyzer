using System;
using CapnoAnalyzer.Helpers;
using Microsoft.WindowsAPICodePack.Sensors;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;

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
            IrStatus = 0;
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

        private int _irStatus;
        public int IrStatus
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

    // Cihaz Veri Paketi 
    public class DeviceData : BindableBase
    {
        // Constructor
        public DeviceData()
        {
            SensorDataType SensorData = new SensorDataType();
            CalibrationDataType CalibrationData = new CalibrationDataType();
            CoefficientsType CalibrationCoefficients = new CoefficientsType();
        }

        // Özellikler
        private SensorDataType _sensorData;
        public SensorDataType SensorData
        {
            get => _sensorData;
            set => SetProperty(ref _sensorData, value);
        }

        private CalibrationDataType _calibrationData;
        public CalibrationDataType CalibrationData
        {
            get => _calibrationData;
            set => SetProperty(ref _calibrationData, value);
        }
            
        private CoefficientsType _calibrationCoefficients;
        public CoefficientsType CalibrationCoefficients
        {
            get => _calibrationCoefficients;
            set => SetProperty(ref _calibrationCoefficients, value);
        }
        // Veri model sınıfı
        public class SensorDataType : BindableBase
        {
            public SensorDataType()
            {
                Time = 0.0;
                IIR_Gas_Voltage = 0.0;
                IIR_Ref_Voltage = 0.0;
                IR_Status = 0.0;
            }

            private double _time;
            public double Time
            {
                get => _time;
                set => SetProperty(ref _time, value);
            }

            private double _iir_Gas_Voltage;
            public double IIR_Gas_Voltage
            {
                get => _iir_Gas_Voltage;
                set => SetProperty(ref _iir_Gas_Voltage, value);
            }

            private double _iir_Ref_Voltage;
            public double IIR_Ref_Voltage
            {
                get => _iir_Ref_Voltage;
                set => SetProperty(ref _iir_Ref_Voltage, value);
            }

            private double _ir_Status;
            public double IR_Status
            {
                get => _ir_Status;
                set => SetProperty(ref _ir_Status, value);
            }
        }


        // Veri model sınıfı
        public class CalibrationDataType : BindableBase
        {
            public CalibrationDataType()
            {
                Sample = string.Empty;
                GasConcentration = 0.0;
                Ref = 0.0;
                Gas = 0.0;
                Ratio = null;
                Transmittance = null;
                Absorption = null;
                PredictedAbsorption = null;
                PredictedGasConcentration = null;
            }

            private string _sample;
            public string Sample
            {
                get => _sample;
                set
                {
                    _sample = value;
                    OnPropertyChanged();
                }
            }

            private double _gasConcentration;
            public double GasConcentration
            {
                get => _gasConcentration;
                set
                {
                    _gasConcentration = value;
                    OnPropertyChanged();
                }
            }

            private double _ref;
            public double Ref
            {
                get => _ref;
                set
                {
                    _ref = value;
                    OnPropertyChanged();
                }
            }

            private double _gas;
            public double Gas
            {
                get => _gas;
                set
                {
                    _gas = value;
                    OnPropertyChanged();
                }
            }

            private double? _ratio;
            public double? Ratio
            {
                get => _ratio;
                set
                {
                    _ratio = value;
                    OnPropertyChanged();
                }
            }

            private double? _transmittance;
            public double? Transmittance
            {
                get => _transmittance;
                set
                {
                    _transmittance = value;
                    OnPropertyChanged();
                }
            }

            private double? _absorption;
            public double? Absorption
            {
                get => _absorption;
                set
                {
                    _absorption = value;
                    OnPropertyChanged();
                }
            }

            private double? _predictedAbsorption;
            public double? PredictedAbsorption
            {
                get => _predictedAbsorption;
                set
                {
                    _predictedAbsorption = value;
                    OnPropertyChanged();
                }
            }

            private double? _predictedGasConcentration;
            public double? PredictedGasConcentration
            {
                get => _predictedGasConcentration;
                set
                {
                    _predictedGasConcentration = value;
                    OnPropertyChanged();
                }
            }
        }

        // Katsayılar sınıfı
        public class CoefficientsType : BindableBase
        {
            public CoefficientsType()
            {
                A = 0.0;
                B = 0.0;
                C = 0.0;
                R = 0.0;
            }   

            private double _a;
            public double A
            {
                get => _a;
                set
                {
                    _a = value;
                    OnPropertyChanged();
                }
            }

            private double _b;
            public double B
            {
                get => _b;
                set
                {
                    _b = value;
                    OnPropertyChanged();
                }
            }

            private double _c;
            public double C
            {
                get => _c;
                set
                {
                    _c = value;
                    OnPropertyChanged();
                }
            }

            private double _r;
            public double R
            {
                get => _r;
                set
                {
                    _r = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
