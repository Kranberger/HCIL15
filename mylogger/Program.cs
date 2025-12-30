// ...existing code...
using System.IO.Ports;
using System.Text;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("MyLogger V0.1");

        string[] portNames = SerialPort.GetPortNames();
        foreach (var portName in portNames)
        {
            Console.WriteLine($"Port={portName}");
        }

        List<byte[]> data = new();
        SerialPort serialPort = new("/dev/ttyACM0", 115200);
        serialPort.Open();
        serialPort.ReadTimeout = 500;
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

                    if (key.KeyChar == '0' || key.KeyChar == '1' || key.KeyChar == '?')
                    {
                        serialPort.Write(key.KeyChar.ToString());
                    }
                }

                byte[] buffer = new byte[256];
                try
                {
                    int r = serialPort.Read(buffer, 0, buffer.Length);
                    File.AppendAllBytes("output2.bin", buffer[..r]);
                    Console.Write(Encoding.ASCII.GetString(buffer, 0, r));
                }
                catch (TimeoutException)
                {
                    continue;
                }   
                catch (IOException ioex)
                {
                    Console.WriteLine($"IO Exception: {ioex.Message}");
                    break;
                }
            }
        }
        finally
        {
            serialPort.Close();
            serialPort.Dispose();
        }
    }
}
