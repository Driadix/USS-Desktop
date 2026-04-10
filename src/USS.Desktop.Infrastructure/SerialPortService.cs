using System.IO.Ports;
using USS.Desktop.Application;
using USS.Desktop.Domain;

namespace USS.Desktop.Infrastructure;

public sealed class SerialPortService : ISerialPortService
{
    public IReadOnlyList<ConnectedSerialPort> ListPorts() =>
        SerialPort.GetPortNames()
            .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
            .Select(port => new ConnectedSerialPort(port, port))
            .ToArray();
}
