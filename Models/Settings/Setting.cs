using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.Device;

namespace CapnoAnalyzer.Models.Settings
{
    public class Setting : BindableBase
    {
        public Setting()
        {
            PlotTime = 10; // Varsayılan 10 saniye
            SampleTime = 10; // Varsayılan 10 saniye
            BaudRate = 921600; // Varsayılan 921600
            TestMode = 1; // Varsayılan 1
        }

        private int _plotTime;
        public int PlotTime
        {
            get => _plotTime;
            set => SetProperty(ref _plotTime, value);
        }

        private int _sampleTime;
        public int SampleTime
        {
            get => _sampleTime;
            set => SetProperty(ref _sampleTime, value);
        }

        private int _baudRate;
        public int BaudRate
        {
            get => _baudRate;
            set => SetProperty(ref _baudRate, value);
        }

        private int _testMode;
        public int TestMode
        {
            get => _testMode;
            set => SetProperty(ref _testMode, value);
        }
    }
}
