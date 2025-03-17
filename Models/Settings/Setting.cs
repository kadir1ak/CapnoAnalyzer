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
    }
}
