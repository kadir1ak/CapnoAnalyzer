using System;
using System;
using System.Threading.Tasks;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.Models.Patient;

namespace CapnoAnalyzer.Services
{
    /// <summary>
    /// Varsayılan cihaz iletişim servisi.
    /// Gerçek seri port akışını Device üzerinden soyutlar.
    /// </summary>
    public class DeviceCommunicator : IDeviceCommunicator
    {
        public Task SendPatientAsync(Device device, Patient patient)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));
            if (patient == null) throw new ArgumentNullException(nameof(patient));

            // Komut göndermeyi UI thread'inde senkron yapıyoruz.
            // SerialPortsManager zaten arka planda okuma/yazma döngülerini yönetiyor.
            device.SendPatient(patient);
            return Task.CompletedTask;
        }
    }
}

