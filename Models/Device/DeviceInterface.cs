using System.Collections.ObjectModel;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.PlotModels;

namespace CapnoAnalyzer.Models.Device
{
    public class DeviceInterface : BindableBase
    {
        /// <summary>
        /// Constructor: Her cihaz için ayrı `DevicePlot` ve `Sensor` nesneleri oluşturur.
        /// </summary>
        public DeviceInterface()
        {
            MyPlot = new DevicePlot(); // **Her cihaz için yeni bir `PlotModel`**
            Sensor = new Sensor();
            IncomingMessage = new ObservableCollection<string>();
        }

        /// <summary>
        /// Gelen mesajları tutmak için ObservableCollection.
        /// </summary>
        private ObservableCollection<string> _incomingMessage;
        public ObservableCollection<string> IncomingMessage
        {
            get => _incomingMessage;
            set => SetProperty(ref _incomingMessage, value);
        }

        /// <summary>
        /// UI üzerinden gönderilecek geçici metin.
        /// </summary>
        private string _outgoingMessage;
        public string OutgoingMessage
        {
            get => _outgoingMessage;
            set => SetProperty(ref _outgoingMessage, value);
        }

        /// <summary>
        /// Sensor nesnesi (veri kaynağı).
        /// </summary>
        private Sensor _sensor;
        public Sensor Sensor
        {
            get => _sensor;
            set => SetProperty(ref _sensor, value);
        }

        /// <summary>
        /// Grafik modeli (Her cihaz için ayrı bir `DevicePlot` oluşturuluyor).
        /// </summary>
        private DevicePlot _myPlot;
        public DevicePlot MyPlot
        {
            get => _myPlot;
            set
            {
                if (_myPlot != value)
                {
                    _myPlot = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gelen mesajları listeye ekler.
        /// </summary>
        public void AddIncomingMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                IncomingMessage.Add(message);
                if (IncomingMessage.Count > 10)
                {
                    IncomingMessage.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Kullanıcının yazdığı mesajı gönderir.
        /// </summary>
        public void SendOutgoingMessage()
        {
            if (!string.IsNullOrWhiteSpace(OutgoingMessage))
            {
                // Mesajı gönder (örneğin bir cihazla iletişim kur)
                AddIncomingMessage($"Sent: {OutgoingMessage}");
                OutgoingMessage = string.Empty; // Mesaj gönderildikten sonra temizle
            }
        }

        /// <summary>
        /// Sensör verilerini grafik modeline ekler.
        /// </summary>
        public void UpdatePlot()
        {
            if (MyPlot != null)
            {
                MyPlot.AddDataPoint(Sensor.Time, Sensor.GasSensor, Sensor.ReferenceSensor);
            }
        }
    }
}
