// ...existing code...
using System.IO.Ports;
using System.Text;

internal class Program
{
    static string logfile = "~/ow/log.txt";
    static string logfile1 = "~/ow/log1.txt";
    static string logfile2 = "~/ow/log2.txt";
    static string dumpfile = "~/ow/buffer_dump.bin";
    private static void Main(string[] args)
    {
        SerialPort? serialPort;
        byte[] buffer = new byte[4096];
        StringBuilder sb = new();

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        logfile = logfile.Replace("~", home);
        logfile1 = logfile1.Replace("~", home);
        logfile2 = logfile2.Replace("~", home);
        dumpfile = dumpfile.Replace("~", home);

        if (File.Exists(logfile))   
        {
            if (File.Exists(logfile2))
            {
                File.Delete(logfile2);
            }
            if (File.Exists(logfile1))  
            {
                File.Move(logfile1, logfile2);
            }

            File.Move(logfile, logfile1);
        }

        ConsoleWriteLine("MyLogger V0.43");

        DateTime today = DateTime.Now;
        string csvfile = $"{home}/ow/ow_{today:yyyyMMdd}.csv";

        List<string> preList = [
            "28-40-c8-eb-03-00-00-4e", "Heizung Vorlauf Pufferspeicher",
            "28-20-b8-67-04-00-00-51", "Zuluft vor Kühler",
            "28-a0-da-eb-03-00-00-98", "Rücklauf Warmwasser",
            "28-48-b9-eb-03-00-00-4f", "Heizung Vorlauf Mischer",
            "28-f4-b1-eb-03-00-00-0b", "Warmwasser ab (Trinkwasser)",
            "28-92-b3-eb-03-00-00-f9", "Pufferspeicher Fühler oben",
            "28-72-47-7f-04-00-00-fc", "Fortluft",
            "28-f6-57-80-04-00-00-5d", "Sole im Kühler",
            "28-2e-97-7f-04-00-00-f1", "Abluft",
            "28-91-2a-67-04-00-00-55", "Zuluft hinter Kühler",
            "28-a9-2a-67-04-00-00-19", "Außenluft hinter EWT",
            "28-69-be-1b-03-00-00-ee", "ExtraFühler Puffer oben",
            "28-65-b8-67-04-00-00-cf", "Vorlauf Fußbodenheizung",
            "28-8d-b7-eb-03-00-00-99", "Rücklauf Zirkulation",
            "28-e3-bf-67-04-00-00-c6", "Rücklauf Fußbodenheizung",
            "28-bb-a8-eb-03-00-00-d5", "Vorlauf Warmwasser (Pufferspeicher)",
            "28-17-ce-eb-03-00-00-79", "Sole Rücklauf",
            "28-7f-c2-eb-03-00-00-3a", "Sole Vorlauf",
            "28-4a-be-eb-03-00-00-70", "Testpunkt Sub1",
            ];
        Dictionary<string, string> romNamesDict = [];

        for (int n = 0; n < preList.Count; n += 2)
        {
            romNamesDict[preList[n]] = preList[n + 1];
        }

        string[] portNames = SerialPort.GetPortNames();
        foreach (var portName in portNames)
        {
            ConsoleWriteLine($"Port={portName}");
        }

        serialPort = new("/dev/ttyACM0", 115200);
        serialPort.Open();
        //serialPort.WriteTimeout = 200;
        serialPort.ReadTimeout = 200;
        
        SerialPortSendKey(serialPort, new ConsoleKeyInfo('1', ConsoleKey.D1, false, false, false));
        SerialPortSendKey(serialPort, new ConsoleKeyInfo('?', ConsoleKey.D1, false, false, false));

        try
        {
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'X') // X-Taste zum Beenden
                    {
                        break;
                    }

                    if (key.KeyChar == 'S')
                    {
                        File.AppendAllText(csvfile, sb.ToString());
                        sb.Clear();
                        ConsoleWriteLine("Saved CSV data to file.");
                        continue;
                    }

                    if (key.KeyChar == '1' || key.KeyChar == '?')
                    {
                        SerialPortSendKey(serialPort, key);
                    }
                    else if (key.KeyChar == '0')
                    {
                        SerialPortSendKey(serialPort, key);
                    }
                    else if (key.KeyChar == '#')
                    {
                        SerialPortSendKey(serialPort, key);

                        if (binaryMode == false)
                        {
                            ConsoleWriteLine("Clear serial input buffer");
                            serialPort.DiscardInBuffer();

                            binaryMode = true;
                        }
                    }
                }

                try
                {
                    if (binaryMode)
                    {
                        if (sb.Length > 10000)
                        {
                            File.AppendAllText(csvfile, sb.ToString());
                            sb.Clear();
                        }

                        const int minLength = 3;
                        int r = serialPort.Read(buffer, 0, minLength);
                        while (r != 3)
                        {
                            int r2 = serialPort.Read(buffer, r, minLength - r);
                            r += r2;
                        }

                        // ADDR
                        // LEN
                        // FUN / CRC8
                        int len = buffer[1];
                        if (len == 0)
                        {
                            // Own POLL received
                            // ...
                        }
                        else if (len > 0)
                        {
                            // Read missing bytes
                            r = serialPort.Read(buffer, 3, len);
                            while (r != len)
                            {
                                int r2 = serialPort.Read(buffer, 3 + r, len - r);
                                r += r2;
                            }

                            int fullLength = len + 3;
                            byte crc = OnwWireCrc8(buffer, fullLength - 1);
                            if (crc == buffer[fullLength - 1])
                            {
                                ulong ticks = ((ulong)DateTime.Now.Ticks) >> 16; // ~10ms ticks
                                int fun = buffer[2];
                                if (fun == DEBUG_LOG)
                                {
                                    ConsoleWrite(Encoding.ASCII.GetString(buffer, 3, len - 1));
                                }
                                else if (fun == ROM_PRES)
                                {
                                    if (len == 0x0a)
                                    {
                                        string rom = BitConverter.ToString(buffer, 3, 8).ToLower();
                                        string connected = "disconnected";
                                        if (buffer[11] > 0)
                                        {
                                            connected = $"connected{buffer[11]}";
                                        }
                                        string name = rom;
                                        if (romNamesDict.ContainsKey(rom))
                                        {
                                            name = $"{romNamesDict[rom]}";
                                        }

                                        ConsoleWriteLine($"{name} is {connected}");


                                        string csv = $"{ticks:X};{rom};{connected};//;{connected};{name}";
                                        sb.AppendLine(csv);
                                    }
                                    else
                                    {
                                        ConsoleWriteLine($"Error: ROM_PRES with invalid LEN={len}.");
                                    }
                                }
                                else if (fun == ROM_INT16)
                                {
                                    if (len == 0x0b)
                                    {
                                        string rom = BitConverter.ToString(buffer, 3, 8).ToLower();
                                        short raw = (short)(buffer[12] | (buffer[11] << 8));
                                        string value = (raw / 16.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                                        string name = rom;
                                        if (romNamesDict.ContainsKey(rom))
                                        {
                                            name = $"{romNamesDict[rom]}";
                                        }

                                        ConsoleWriteLine($"{name}, T={value,5} °C");

                                        string csv = $"{ticks:X};{rom};{raw:X4};//;{value,5};{name}";
                                        sb.AppendLine(csv);
                                    }
                                    else
                                    {
                                        ConsoleWriteLine($"Error: ROM_INT16 with invalid LEN={len}.");
                                    }
                                }
                                else
                                {
                                    ConsoleWriteLine($"Info: Received FUN={fun:X2} with LEN={len}.");
                                }

                                DateTime now = DateTime.Now;
                                if (now.Day != today.Day)
                                {
                                    // New day, switch file
                                    File.AppendAllText(csvfile, sb.ToString());
                                    sb.Clear();
                                    today = now;
                                    csvfile = $"{home}/ow/ow_{today:yyyyMMdd}.csv";
                                }
                            }
                            else
                            {
                                ConsoleWriteLine($"Error: CRC mismatch. Calculated {crc}, but received {buffer[fullLength - 1]}.");

                                ConsoleWriteLine("Clear serial input buffer");
                                serialPort.DiscardInBuffer();
                            }

                            SendPollForSubs(serialPort);
                        }
                        else
                        {
                            ConsoleWriteLine($"Error: Invalid length {len}.");
                            break;
                        }

                    }
                    else
                    {
                        int r = serialPort.Read(buffer, 0, buffer.Length);
                        File.AppendAllBytes("output.txt", buffer[..r]);
                        ConsoleWrite(Encoding.ASCII.GetString(buffer, 0, r));
                    }
                }
                catch (TimeoutException)
                {
                }   
                catch (IOException ioex)
                {
                    ConsoleWriteLine($"IO Exception: {ioex.Message}");
                    break;
                }

                SendPollForSubs(serialPort);
            }
        }
        finally
        {
            File.AppendAllText(csvfile, sb.ToString());
            File.WriteAllBytes(dumpfile, buffer);

            serialPort.Close();
            serialPort.Dispose();
        }
    }

    static void ConsoleWrite(string message)
    {
        Console.Write(message);
        File.AppendAllText(logfile, message);
    }

    static void ConsoleWriteLine(string message)
    {
        string s = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
        Console.WriteLine(s);
        File.AppendAllText(logfile, s + Environment.NewLine);
    }

    static void SerialPortSendKey(SerialPort serialPort, ConsoleKeyInfo key)
    {
        if (binaryMode)
        {
            SendPollForSubs(serialPort);

            byte[] sendkey = {ADDR_TO_SUB1, 0x02, DEBUG_LOG, 0x00, 0xff};
            sendkey[3] = (byte)key.KeyChar;
            sendkey[sendkey.Length - 1] = OnwWireCrc8(sendkey, sendkey.Length - 1);
            serialPort.Write(sendkey, 0, sendkey.Length);
        }
        else
        {
            serialPort.Write(key.KeyChar.ToString());
        }
    }

    // typedef enum
    // {
    //     DEBUG_LOG    = 0xd0, // LEN + ASCII-String ()
    //     POLL_REQ     = 0xe0, // No data, similar to POLL message, but for single Sub
    //     REFRESH_ALL  = 0xe1, // No data
    //     ROM_PRES   = 0xf0, // ID+UINT8
    //     ROM_INT16  = 0xf1, // ID+INT16
    // } e_FUN;

    const byte DEBUG_LOG   = 0xd0;
    const byte POLL_REQ    = 0xe0;
    const byte REFRESH_ALL = 0xe1;
    const byte ROM_PRES  = 0xf0;
    const byte ROM_INT16 = 0xf1;

// typedef enum
// {
//     ADDR_BROADCAST_TO_SUB = 0x29,
//     // ADDR_TO_MASTER = 0x33,
//     // ADDR_FROM_MASTER = 0xb3,
//     ADDR_TO_SUB1 = 0x65,
//     ADDR_FROM_SUB1 = 0xe5,
//     ADDR_TO_SUB2 = 0x66,
//     ADDR_FROM_SUB2 = 0xe6,
//     ADDR_TO_SUB3 = 0x67,
//     ADDR_FROM_SUB3 = 0xe7,
//     ADDR_TO_SUB4 = 0x68,
//     ADDR_FROM_SUB4 = 0xe8,
//     ADDR_TO_SUB5 = 0x69,
//     ADDR_FROM_SUB5 = 0xe9,
//     ADDR_TO_SUB6 = 0x6a,
//     ADDR_FROM_SUB6 = 0xea,
//     ADDR_TO_SUB7 = 0x6b,
//     ADDR_FROM_SUB7 = 0xeb,
//     ADDR_TO_SUB8 = 0x6c,
//     ADDR_FROM_SUB8 = 0xec,
//     ADDR_TO_SUB9 = 0x6d,
//     ADDR_FROM_SUB9 = 0xed,
//     ADDR_TO_SUB10 = 0x6e,
//     ADDR_FROM_SUB10 = 0xef,
// } e_SerMsgAddress;
    const byte ADDR_BROADCAST_TO_SUB = 0x29;
    const byte ADDR_TO_SUB1 = 0x65;
    const byte ADDR_FROM_SUB1 = 0xe5;
    const byte ADDR_TO_SUB2 = 0x66;
    const byte ADDR_FROM_SUB2 = 0xe6;
    const byte ADDR_TO_SUB3 = 0x67;
    const byte ADDR_FROM_SUB3 = 0xe7;
    const byte ADDR_TO_SUB4 = 0x68;
    const byte ADDR_FROM_SUB4 = 0xe8;
    const byte ADDR_TO_SUB5 = 0x69;
    const byte ADDR_FROM_SUB5 = 0xe9;
    const byte ADDR_TO_SUB6 = 0x6a;
    const byte ADDR_FROM_SUB6 = 0xea;
    const byte ADDR_TO_SUB7 = 0x6b;
    const byte ADDR_FROM_SUB7 = 0xeb;
    const byte ADDR_TO_SUB8 = 0x6c;
    const byte ADDR_FROM_SUB8 = 0xec;
    const byte ADDR_TO_SUB9 = 0x6d;
    const byte ADDR_FROM_SUB9 = 0xed;
    const byte ADDR_TO_SUB10 = 0x6e;
    const byte ADDR_FROM_SUB10 = 0xef; 
    
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

    static byte[] poll = {ADDR_BROADCAST_TO_SUB, 0x00, 0x73};
    static void SendPollForSubs(SerialPort serialPort)
    {
        // byte testCrc = OnwWireCrc8(poll, poll.Length - 1);
        // poll[2] = testCrc;
        serialPort.Write(poll, 0, poll.Length);
    }

    static bool binaryMode = true;

}
