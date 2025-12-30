// ...existing code...
using System.IO.Ports;
using System.Text;

internal class Program
{
    private static void Main(string[] args)
    {
        string[] portNames = SerialPort.GetPortNames();
        foreach (var portName in portNames)
        {
            Console.WriteLine($"Port={portName}");
        }

        List<byte[]> data = new();
        SerialPort serialPort = new("/dev/ttyACM0", 115200);
        serialPort.Open();
        serialPort.Write("0");
        serialPort.Write("1");
        serialPort.Write("?");

        try
        {
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Escape) // Taste zum Beenden (z.B. Escape)
                        break;
                }

                byte[] buffer = new byte[256];
                int r = serialPort.Read(buffer, 0, buffer.Length);
                File.AppendAllBytes("output.bin", buffer[..r]);
                Console.WriteLine(Encoding.ASCII.GetString(buffer, 0, r));
            }
        }
        finally
        {
            serialPort.Close();
            serialPort.Dispose();
        }
    }
}
