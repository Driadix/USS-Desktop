using System.IO.Ports;
using USS.Desktop.Application;
using USS.Desktop.Domain;

namespace USS.Desktop.Infrastructure;

public sealed class SerialPortService : ISerialPortService
{
    private readonly Dictionary<string, int> _firstSeenOrder = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();
    private int _nextOrder;

    public IReadOnlyList<ConnectedSerialPort> ListPorts()
    {
        var currentPorts = SerialPort.GetPortNames();

        lock (_syncRoot)
        {
            foreach (var port in currentPorts)
            {
                if (!_firstSeenOrder.ContainsKey(port))
                {
                    _firstSeenOrder[port] = _nextOrder++;
                }
            }

            return currentPorts
                .OrderBy(port => _firstSeenOrder[port])
                .ThenBy(port => port, StringComparer.OrdinalIgnoreCase)
                .Select(port => new ConnectedSerialPort(port, port))
                .ToArray();
        }
    }
}
