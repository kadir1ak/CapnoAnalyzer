using CapnoAnalyzer.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapnoAnalyzer.Models.Device
{
    public class Sensor : BindableBase
    {

        // Constructor
        public Sensor()
        {
            _gasSensor = 0.0;
            _referenceSensor = 0.0;
            _temperature = 0.0;
            _humidity = 0.0;
            _pressure = 0.0;
            _time = 0.0;
        }
        // Time property
        private double _time;
        public double Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }

        // Gas sensor property
        private double _gasSensor;
        public double GasSensor
        {
            get => _gasSensor;
            set => SetProperty(ref _gasSensor, value);
        }

        // Reference sensor property
        private double _referenceSensor;
        public double ReferenceSensor
        {
            get => _referenceSensor;
            set => SetProperty(ref _referenceSensor, value);
        }

        // Temperature property
        private double _temperature;
        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        // Humidity property
        private double _humidity;
        public double Humidity
        {
            get => _humidity;
            set => SetProperty(ref _humidity, value);
        }

        // Pressure, property
        private double _pressure;
        public double Pressure
        {
            get => _pressure;
            set => SetProperty(ref _pressure, value);
        }
    }
}
