using System.Threading.Tasks;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.Models.Patient;

namespace CapnoAnalyzer.Services
{
    /// <summary>
    /// Cihaza hasta bilgisini gönderen soyut arayüz.
    /// Testlerde gerçek seri port yerine sahte implementasyon kullanılabilir.
    /// </summary>
    public interface IDeviceCommunicator
    {
        Task SendPatientAsync(Device device, Patient patient);
    }
}

