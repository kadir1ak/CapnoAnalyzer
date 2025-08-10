using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CapnoAnalyzer.Services
{
    public sealed class SerialPortErrorEventArgs : EventArgs
    {
        public string PortName { get; }
        public Exception Exception { get; }
        public string Operation { get; }
        public SerialPortErrorEventArgs(string portName, string operation, Exception ex)
        {
            PortName = portName;
            Operation = operation;
            Exception = ex;
        }
    }
    public sealed class SerialMessageEventArgs : EventArgs
    {
        public string PortName { get; }
        public string Message { get; }
        public DateTime TimestampUtc { get; }
        public SerialMessageEventArgs(string portName, string message)
        {
            PortName = portName;
            Message = message;
            TimestampUtc = DateTime.UtcNow;
        }
    }

    public sealed class SerialPortsManager : IDisposable
    {
        private readonly object _connectLock = new();
        private readonly Encoding _encoding;
        private readonly string _lineSeparator;
        private readonly int _readBufferSize;
        private readonly int _maxLineLength;
        private readonly Dispatcher _dispatcher;

        private ManagementEventWatcher _creationWatcher;
        private ManagementEventWatcher _deletionWatcher;

        public ObservableCollection<string> AvailablePorts { get; } = new();
        public ObservableCollection<SerialPort> ConnectedPorts { get; } = new();

        private readonly ConcurrentDictionary<string, Channel<string>> _portChannels = new();
        private readonly ConcurrentDictionary<string, Task> _portConsumerTasks = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _portCancellation = new();
        private readonly ConcurrentDictionary<string, Task> _portReadLoops = new();
        private readonly ConcurrentDictionary<string, StringBuilder> _lineBuffers = new();

        // GERİYE DÖNÜK UYUMLULUK: Eski event (Action<string,string>)
        public event Action<string, string> MessageReceived;

        // Yeni, zengin event
        public event EventHandler<SerialMessageEventArgs> MessageReceivedEx;

        public event Action<string> SerialPortAdded;
        public event Action<string> SerialPortRemoved;
        public event Action<string> DeviceDisconnected;
        public event EventHandler<SerialPortErrorEventArgs> ErrorOccurred;

        public SerialPortsManager(
            Dispatcher dispatcher = null,
            Encoding encoding = null,
            string lineSeparator = "\n",
            int readBufferSize = 4096,
            int maxLineLength = 32 * 1024
        )
        {
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
            _encoding = encoding ?? Encoding.ASCII;
            _lineSeparator = lineSeparator;
            _readBufferSize = readBufferSize;
            _maxLineLength = maxLineLength;

            InitializeWatchers();
            ScanSerialPorts();
        }

        #region Public API

        public void ConnectToPort(string portName, int baudRate = 9600)
        {
            lock (_connectLock)
            {
                if (ConnectedPorts.Any(p => p.PortName.Equals(portName, StringComparison.OrdinalIgnoreCase)))
                    return;

                SerialPort sp = null;
                try
                {
                    sp = new SerialPort(portName, baudRate)
                    {
                        DataBits = 8,
                        Parity = Parity.None,
                        StopBits = StopBits.One,
                        Handshake = Handshake.None,
                        ReadTimeout = 500,
                        WriteTimeout = 1000,
                        ReadBufferSize = _readBufferSize,
                        WriteBufferSize = 2048,
                        NewLine = _lineSeparator
                    };

                    sp.Open();

                    InvokeOnUi(() =>
                    {
                        if (!ConnectedPorts.Contains(sp))
                            ConnectedPorts.Add(sp);
                    });

                    StartPortPipelines(sp);
                }
                catch (Exception ex)
                {
                    sp?.Dispose();
                    RaiseError(portName, "Connect", ex);
                }
            }
        }

        public void DisconnectFromPort(string portName)
        {
            lock (_connectLock)
            {
                var sp = ConnectedPorts.FirstOrDefault(p => p.PortName.Equals(portName, StringComparison.OrdinalIgnoreCase));
                if (sp == null) return;

                try
                {
                    StopPortPipelines(portName);

                    if (sp.IsOpen)
                        sp.Close();

                    InvokeOnUi(() => ConnectedPorts.Remove(sp));
                    sp.Dispose();
                }
                catch (Exception ex)
                {
                    RaiseError(portName, "Disconnect", ex);
                }
                finally
                {
                    DeviceDisconnected?.Invoke(portName);
                }
            }
        }

        public void SendMessage(string portName, string message, bool appendLineSeparator = true)
        {
            var sp = ConnectedPorts.FirstOrDefault(p => p.PortName.Equals(portName, StringComparison.OrdinalIgnoreCase));
            if (sp == null || !sp.IsOpen) return;

            try
            {
                if (appendLineSeparator)
                {
                    sp.Write(message);
                    sp.Write(_lineSeparator);
                }
                else
                {
                    sp.Write(message);
                }
            }
            catch (Exception ex)
            {
                RaiseError(portName, "SendMessage", ex);
            }
        }

        public IEnumerable<string> GetConnectedPorts() =>
            ConnectedPorts.Select(p => p.PortName).ToArray();

        public void ScanSerialPorts()
        {
            try
            {
                var current = SerialPort.GetPortNames().OrderBy(n => n).ToList();
                var existing = AvailablePorts.ToList();

                foreach (var added in current.Except(existing))
                {
                    InvokeOnUi(() => AvailablePorts.Add(added));
                    SerialPortAdded?.Invoke(added);
                }

                foreach (var removed in existing.Except(current))
                {
                    InvokeOnUi(() => AvailablePorts.Remove(removed));
                    SerialPortRemoved?.Invoke(removed);

                    if (ConnectedPorts.Any(p => p.PortName == removed))
                    {
                        DisconnectFromPort(removed);
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseError(null, "ScanSerialPorts", ex);
            }
        }

        public void NotifyDeviceIfPortRemoved(string portName)
        {
            if (!AvailablePorts.Contains(portName))
            {
                DeviceDisconnected?.Invoke(portName);
            }
        }

        #endregion

        #region Pipelines

        private void StartPortPipelines(SerialPort sp)
        {
            var portName = sp.PortName;

            var cts = new CancellationTokenSource();
            _portCancellation[portName] = cts;

            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
            _portChannels[portName] = channel;

            var consumerTask = Task.Run(() => ConsumeMessagesAsync(portName, channel.Reader, cts.Token), cts.Token);
            _portConsumerTasks[portName] = consumerTask;

            var readLoopTask = Task.Run(() => ReadLoopAsync(sp, channel.Writer, cts.Token), cts.Token);
            _portReadLoops[portName] = readLoopTask;
        }

        private async Task ReadLoopAsync(SerialPort sp, ChannelWriter<string> writer, CancellationToken ct)
        {
            var portName = sp.PortName;
            var lineBuffer = _lineBuffers.GetOrAdd(portName, _ => new StringBuilder(256));
            byte[] rawBuffer = new byte[_readBufferSize];
            try
            {
                while (!ct.IsCancellationRequested && sp.IsOpen)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await sp.BaseStream
                                            .ReadAsync(rawBuffer.AsMemory(0, rawBuffer.Length), ct)
                                            .ConfigureAwait(false);

                        if (bytesRead == 0)
                        {
                            await Task.Delay(10, ct).ConfigureAwait(false);
                            continue;
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (TimeoutException) { continue; }

                    var text = _encoding.GetString(rawBuffer, 0, bytesRead);
                    lineBuffer.Append(text);

                    if (lineBuffer.Length > _maxLineLength)
                    {
                        lineBuffer.Clear();
                        RaiseError(portName, "LineBufferOverflow",
                            new InvalidOperationException("Maksimum satır uzunluğu aşıldı."));
                        continue;
                    }

                    // Bir seferde çıkarılacak satırlar için lokal liste
                    List<string> completedLines = null;

                    while (true)
                    {
                        int idx = lineBuffer.ToString().IndexOf(_lineSeparator, StringComparison.Ordinal);
                        if (idx < 0) break;

                        string line = lineBuffer.ToString(0, idx);
                        lineBuffer.Remove(0, idx + _lineSeparator.Length);

                        string clean = line.Trim();
                        if (clean.Length > 0)
                        {
                            completedLines ??= new List<string>();
                            completedLines.Add(clean);
                        }
                    }

                    if (completedLines != null)
                    {
                        foreach (var line in completedLines)
                        {
                            if (!writer.TryWrite(line))
                                await writer.WriteAsync(line, ct).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseError(portName, "ReadLoop", ex);
            }
            finally
            {
                writer.TryComplete();
                if (!ct.IsCancellationRequested)
                {
                    InvokeOnUi(() =>
                    {
                        var found = ConnectedPorts.FirstOrDefault(p => p.PortName == portName);
                        if (found != null)
                        {
                            ConnectedPorts.Remove(found);
                            try { found.Close(); found.Dispose(); } catch { }
                        }
                    });
                    DeviceDisconnected?.Invoke(portName);
                }
            }
        }

        private async Task ConsumeMessagesAsync(string portName, ChannelReader<string> reader, CancellationToken ct)
        {
            try
            {
                await foreach (var msg in reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    InvokeOnUi(() => RaiseMessage(portName, msg));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                RaiseError(portName, "ConsumeMessages", ex);
            }
        }

        private void StopPortPipelines(string portName)
        {
            if (_portCancellation.TryRemove(portName, out var cts))
            {
                try { cts.Cancel(); } catch { }
                cts.Dispose();
            }

            if (_portChannels.TryRemove(portName, out var channel))
                channel.Writer.TryComplete();

            if (_portReadLoops.TryRemove(portName, out var readTask))
                SafeWait(readTask, portName, "ReadLoopStop");

            if (_portConsumerTasks.TryRemove(portName, out var consumer))
                SafeWait(consumer, portName, "ConsumerStop");

            _lineBuffers.TryRemove(portName, out _);
        }

        private void SafeWait(Task task, string portName, string op)
        {
            try
            {
                if (task == null) return;
                if (!task.IsCompleted)
                    task.Wait(500);
            }
            catch (Exception ex)
            {
                RaiseError(portName, op, ex);
            }
        }

        #endregion

        #region WMI

        private void InitializeWatchers()
        {
            try
            {
                _creationWatcher = CreateWatcher(
                    "SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_SerialPort'",
                    OnPortCreated);

                _deletionWatcher = CreateWatcher(
                    "SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_SerialPort'",
                    OnPortDeleted);
            }
            catch (Exception ex)
            {
                RaiseError(null, "InitializeWatchers", ex);
            }
        }

        private ManagementEventWatcher CreateWatcher(string query, EventArrivedEventHandler handler)
        {
            var scope = new ManagementScope("root\\CIMV2");
            var watcher = new ManagementEventWatcher(scope, new WqlEventQuery(query));
            watcher.EventArrived += handler;
            watcher.Start();
            return watcher;
        }

        private void OnPortCreated(object sender, EventArrivedEventArgs e) => InvokeOnUi(ScanSerialPorts);
        private void OnPortDeleted(object sender, EventArrivedEventArgs e) => InvokeOnUi(ScanSerialPorts);

        #endregion

        #region Helpers

        // TEK NOKTA: Mesaj yayınlama (hem eski hem yeni event)
        private void RaiseMessage(string portName, string message)
        {
            try
            {
                MessageReceived?.Invoke(portName, message);                 // Eski API
                MessageReceivedEx?.Invoke(this, new SerialMessageEventArgs(portName, message)); // Yeni API
            }
            catch { /* Event handler hatası yutulur */ }
        }

        private void InvokeOnUi(Action action)
        {
            if (_dispatcher == null || _dispatcher.CheckAccess()) action();
            else _dispatcher.BeginInvoke(action);
        }

        private void RaiseError(string portName, string operation, Exception ex)
        {
            try
            {
                ErrorOccurred?.Invoke(this, new SerialPortErrorEventArgs(portName, operation, ex));
            }
            catch { }
        }

        #endregion

        #region Dispose

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                foreach (var port in ConnectedPorts.Select(p => p.PortName).ToList())
                    DisconnectFromPort(port);

                DisposeWatcher(_creationWatcher, OnPortCreated);
                DisposeWatcher(_deletionWatcher, OnPortDeleted);
            }
            catch (Exception ex)
            {
                RaiseError(null, "Dispose", ex);
            }
        }

        private void DisposeWatcher(ManagementEventWatcher watcher, EventArrivedEventHandler handler)
        {
            if (watcher == null) return;
            try
            {
                watcher.EventArrived -= handler;
                watcher.Stop();
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                RaiseError(null, "DisposeWatcher", ex);
            }
        }

        #endregion
    }
}