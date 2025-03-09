using System.Collections.ObjectModel;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.PlotModels;

namespace CapnoAnalyzer.Models.Device
{
    public class DeviceInterface : BindableBase
    {

        /// <summary>
        /// Gelen mesajları tutmak için ObservableCollection.
        /// </summary>
        private ObservableCollection<string> _incomingMessage = new ObservableCollection<string>();
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
        private Sensor _sensor = new Sensor();
        public Sensor Sensor
        {
            get => _sensor;
            set => SetProperty(ref _sensor, value);
        }

        /// <summary>
        /// Grafik modeli (DevicePlot).
        /// </summary>
        // DeviceInterface.cs (Gerekli kısım, değişiklik yok, sadece hatırlatma)
        public DevicePlot MyPlot { get; set; } = new DevicePlot();


        /// <summary>
        /// Gelen mesajları listeye ekler.
        /// </summary>
        /// <param name="message">Eklenen mesaj.</param>
        public void AddIncomingMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                IncomingMessage.Add(message);
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
    }
}
