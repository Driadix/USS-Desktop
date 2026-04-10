using USS.Desktop.Domain;

namespace USS.Desktop.Application;

public interface ISerialPortService
{
    IReadOnlyList<ConnectedSerialPort> ListPorts();
}
