using CapnoAnalyzer.Views.DevicesViews.DevicesControl;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CapnoAnalyzer.Services
{
    public class SerialPortsManager : IDisposable
    {
        private ManagementEventWatcher _serialPortsRemovedWatcher;
        private ManagementEventWatcher _serialPortsAddedWatcher;

        public ObservableCollection<string> AvailablePorts { get; } = new ObservableCollection<string>();
        public ObservableCollection<SerialPort> ConnectedPorts { get; } = new ObservableCollection<SerialPort>();

        private readonly ConcurrentDictionary<string, BlockingCollection<string>> _portDataQueues = new();
        private readonly ConcurrentDictionary<string, Task> _portProcessingTasks = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _portCancellationTokens = new();

        public event Action<string> SerialPortAdded;
        public event Action<string> SerialPortRemoved;
        public event Action<string> DeviceDisconnected; 
        public event Action<string, string> MessageReceived;

        public SerialPortsManager()
        {
            InitializeEventWatchers();
            ScanSerialPorts();
        }

        public void ConnectToPort(string portName, int baudRate = 9600)
        {
            try
            {
                if (ConnectedPorts.Any(p => p.PortName == portName))
                    throw new InvalidOperationException($"Port {portName} is already connected.");

                var serialPort = new SerialPort
                {
                    PortName = portName,
                    BaudRate = baudRate,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 5000,
                    WriteTimeout = 5000
                };

                serialPort.DataReceived += (s, e) => OnDataReceived(serialPort);
                serialPort.Open();

                // ConnectedPorts koleksiyonunu güncelle
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!ConnectedPorts.Any(p => p.PortName == portName))
                    {
                        ConnectedPorts.Add(serialPort);
                    }
                });
                StartProcessingPortData(serialPort);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to port {portName}: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void DisconnectFromPort(string portName)
        {
            try
            {
                var serialPort = ConnectedPorts.FirstOrDefault(p => p.PortName == portName);
                if (serialPort == null) return;

                StopProcessingPortData(portName);
                serialPort.DataReceived -= (s, e) => OnDataReceived(serialPort);
                serialPort.Close();
                ConnectedPorts.Remove(serialPort);         
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error disconnecting from port {portName}: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SendMessage(string portName, string message)
        {
            // Cihaza veri göndermek için eklediğimiz metot:
            var serialPort = ConnectedPorts.FirstOrDefault(p => p.PortName == portName);
            if (serialPort != null && serialPort.IsOpen)
            {
                // Protokol gerektiriyorsa \r\n ekleyebilirsiniz: e.g. serialPort.WriteLine(message);
                serialPort.WriteLine(message);
            }
            else
            {
                // Port kapalıysa uyarı verebilir veya Exception fırlatabilirsiniz
                // throw new InvalidOperationException($"Port {portName} not connected.");
            }
        }

        public IEnumerable<string> GetConnectedPorts()
        {
            return ConnectedPorts.Select(p => p.PortName);
        }

        private void OnDataReceived(SerialPort serialPort)
        {
            try
            {
                string data = serialPort.ReadExisting();
                if (!string.IsNullOrEmpty(data))
                {
                    if (_portDataQueues.TryGetValue(serialPort.PortName, out var queue))
                    {
                        queue.Add(data);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading data from port {serialPort.PortName}: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartProcessingPortData(SerialPort serialPort)
        {
            var portName = serialPort.PortName;
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var dataQueue = new BlockingCollection<string>();
            _portDataQueues[portName] = dataQueue;
            _portCancellationTokens[portName] = cancellationTokenSource;

            var processingTask = Task.Run(() =>
            {
                try
                {
                    foreach (var data in dataQueue.GetConsumingEnumerable(cancellationToken))
                    {
                        ProcessData(portName, data);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error processing data for port {portName}: {ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }, cancellationToken);

            _portProcessingTasks[portName] = processingTask;
        }

        private void StopProcessingPortData(string portName)
        {
            if (_portCancellationTokens.TryRemove(portName, out var cancellationTokenSource))
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }

            if (_portDataQueues.TryRemove(portName, out var dataQueue))
            {
                dataQueue.CompleteAdding();
            }

            if (_portProcessingTasks.TryRemove(portName, out var processingTask))
            {
                processingTask.Wait();
            }
        }

        private void ProcessData(string portName, string data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Olayı tetikleyelim
                MessageReceived?.Invoke(portName, data);
            });
        }

        private void InitializeEventWatchers()
        {
            try
            {
                _serialPortsRemovedWatcher = CreateEventWatcher(
                    "SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3",
                    OnSerialPortRemoved
                );

                _serialPortsAddedWatcher = CreateEventWatcher(
                    "SELECT * FROM __InstanceOperationEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_SerialPort'",
                    OnSerialPortAdded
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing event watchers: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ManagementEventWatcher CreateEventWatcher(string query, EventArrivedEventHandler eventHandler)
        {
            var watcher = new ManagementEventWatcher(new ManagementScope("root\\CIMV2"), new WqlEventQuery(query));
            watcher.EventArrived += eventHandler;
            watcher.Start();
            return watcher;
        }

        private void OnSerialPortRemoved(object sender, EventArrivedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                ScanSerialPorts(); // Port listesini güncelle
            }));
        }

        // Port çıkarıldığında, ilgili cihaza haber veriyoruz.
        public void NotifyDeviceIfPortRemoved(string portName)
        {
            if (!AvailablePorts.Contains(portName)) // Eğer port artık mevcut değilse
            {
                DeviceDisconnected?.Invoke(portName); // Cihazın bağlantısının koptuğunu bildir.
            }
        }

        private void OnSerialPortAdded(object sender, EventArrivedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                ScanSerialPorts(); // Port listesini güncelle
            }));           
        }

        public void ScanSerialPorts()
        {
            try
            {
                var existingPorts = AvailablePorts.ToList();
                var currentPorts = SerialPort.GetPortNames().ToList();

                foreach (var port in currentPorts.Except(existingPorts))
                {
                    AvailablePorts.Add(port);
                    SerialPortAdded?.Invoke(port);
                }

                foreach (var port in existingPorts.Except(currentPorts))
                {
                    AvailablePorts.Remove(port);
                    SerialPortRemoved?.Invoke(port);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error scanning serial ports: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            foreach (var portName in ConnectedPorts.Select(p => p.PortName).ToList())
            {
                DisconnectFromPort(portName);
            }
            DisposeWatcher(_serialPortsRemovedWatcher);
            DisposeWatcher(_serialPortsAddedWatcher);
        }

        private void DisposeWatcher(ManagementEventWatcher watcher)
        {
            try
            {
                watcher?.Stop();
                watcher?.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error disposing watcher: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
