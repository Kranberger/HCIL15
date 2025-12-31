// ...existing code...
using System.IO.Ports;
using System.Text;

internal class Program
{
    // typedef enum
    // {
    //     DEBUG_LOG    = 0xd0, // LEN + ASCII-String ()
    //     POLL_REQ     = 0xe0, // No data, similar to POLL message, but for single Sub
    //     REFRESH_ALL  = 0xe1, // No data
    //     SEND_UINT8   = 0xf0, // ID+UINT8
    //     SEND_UINT16  = 0xf1, // ID+UINT16
    // } e_FUN;

    const byte DEBUG_LOG   = 0xd0;
    const byte POLL_REQ    = 0xe0;
    const byte REFRESH_ALL = 0xe1;
    const byte SEND_UINT8  = 0xf0;
    const byte SEND_UINT16 = 0xf1;

    
    // CRC 8 lookup table
    static byte[] CRC_8_TABLE =
    {
            0, 94,188,226, 97, 63,221,131,194,156,126, 32,163,253, 31, 65,
        157,195, 33,127,252,162, 64, 30, 95,  1,227,189, 62, 96,130,220,
            35,125,159,193, 66, 28,254,160,225,191, 93,  3,128,222, 60, 98,
        190,224,  2, 92,223,129, 99, 61,124, 34,192,158, 29, 67,161,255,
            70, 24,250,164, 39,121,155,197,132,218, 56,102,229,187, 89,  7,
        219,133,103, 57,186,228,  6, 88, 25, 71,165,251,120, 38,196,154,
        101, 59,217,135,  4, 90,184,230,167,249, 27, 69,198,152,122, 36,
        248,166, 68, 26,153,199, 37,123, 58,100,134,216, 91,  5,231,185,
        140,210, 48,110,237,179, 81, 15, 78, 16,242,172, 47,113,147,205,
            17, 79,173,243,112, 46,204,146,211,141,111, 49,178,236, 14, 80,
        175,241, 19, 77,206,144,114, 44,109, 51,209,143, 12, 82,176,238,
            50,108,142,208, 83, 13,239,177,240,174, 76, 18,145,207, 45,115,
        202,148,118, 40,171,245, 23, 73,  8, 86,180,234,105, 55,213,139,
            87,  9,235,181, 54,104,138,212,149,203, 41,119,244,170, 72, 22,
        233,183, 85, 11,136,214, 52,106, 43,117,151,201, 74, 20,246,168,
        116, 42,200,150, 21, 75,169,247,182,232, 10, 84,215,137,107, 53
    };

    static byte OnwWireCrc8(byte[] DataArray, int Length)
    {
        int i;
        byte CRC;

        CRC = 0;

        for (i=0; i<Length; i++)
            CRC = CRC_8_TABLE[CRC ^ DataArray[i]];

        return CRC;
    }

    private static void Main(string[] args)
    {
        Console.WriteLine("MyLogger V0.34");

        string[] portNames = SerialPort.GetPortNames();
        foreach (var portName in portNames)
        {
            Console.WriteLine($"Port={portName}");
        }

        bool binaryMode = false;
        List<byte[]> data = new();
        SerialPort serialPort = new("/dev/ttyACM0", 115200);
        serialPort.Open();
        serialPort.ReadTimeout = 500;
        // serialPort.Write("0");
        // serialPort.Write("1");
        // serialPort.Write("?");

        try
        {
            byte[] buffer = new byte[256];
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
                    else if (key.KeyChar == '#')
                    {
                        serialPort.Write(key.KeyChar.ToString());

                        Console.WriteLine("Read all pending data");
                        int all = 0;
                        try
                        {
                            while (true)
                            {
                                all += serialPort.Read(buffer, 0, buffer.Length);
                                Console.WriteLine($"Read {all} bytes so far...");
                            }
                        }
                        catch (TimeoutException)
                        {
                            Console.WriteLine("No more data");
                        }

                        binaryMode = true;
                    }
                }

                try
                {
                    if (binaryMode)
                    {
                        const int minLength = 3;
                        int r = serialPort.Read(buffer, 0, minLength);
                        // ADDR
                        // LEN
                        // FUN / CRC8
                        int len = buffer[1];
                        if (len == 0)
                        {
                            
                        }
                        else if (len > 0)
                        {
                            // Read missing bytes
                            r = serialPort.Read(buffer, 3, len);
                            if (r != len)
                            {
                                Console.WriteLine($"Error: Expected {len} bytes, but got {r} bytes.");
                                break;
                            }

                            int fullLength = len + 3;
                            byte crc = OnwWireCrc8(buffer, fullLength - 1);
                            if (crc == buffer[fullLength - 1])
                            {
                                int fun = buffer[2];
                                if (fun == DEBUG_LOG)
                                {
                                    Console.Write(Encoding.ASCII.GetString(buffer, 3, len - 1));
                                }
                                else
                                {
                                    File.AppendAllBytes("output.bin", buffer[..(len + 3)]);
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Error: CRC mismatch. Calculated {crc}, but received {buffer[fullLength - 1]}.");
                                break;
                            }

                        }
                        else
                        {
                            Console.WriteLine($"Error: Invalid length {len}.");
                            break;
                        }

                    }
                    else
                    {
                        int r = serialPort.Read(buffer, 0, buffer.Length);
                        File.AppendAllBytes("output.txt", buffer[..r]);
                        Console.Write(Encoding.ASCII.GetString(buffer, 0, r));
                    }

                    continue;
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
