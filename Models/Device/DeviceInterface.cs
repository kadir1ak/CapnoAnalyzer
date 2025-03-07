using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CapnoAnalyzer.Helpers;

namespace CapnoAnalyzer.Models.Device
{
    public class DeviceInterface : BindableBase
    {
        // Gelen mesajları tutacağımız koleksiyon
        private ObservableCollection<string> _incomingMessage = new ObservableCollection<string>();
        public ObservableCollection<string> IncomingMessage
        {
            get => _incomingMessage;
            set => SetProperty(ref _incomingMessage, value);
        }

        // Kullanıcının UI'da yazıp göndereceği geçici metin
        private string _outgoingMessage;
        public string OutgoingMessage
        {
            get => _outgoingMessage;
            set => SetProperty(ref _outgoingMessage, value);
        }

        private Sensor _sensor = new Sensor();
        public Sensor Sensor
        {
            get => _sensor;
            set => SetProperty(ref _sensor, value);
        }
    }
}
