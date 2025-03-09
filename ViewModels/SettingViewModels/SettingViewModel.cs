using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.Settings;

namespace CapnoAnalyzer.ViewModels.SettingViewModels
{
    public class SettingViewModel : BindableBase
    {
        private Setting _currentSetting = new Setting();

        public Setting CurrentSetting
        {
            get => _currentSetting;
            set => SetProperty(ref _currentSetting, value);
        }
    }
}
