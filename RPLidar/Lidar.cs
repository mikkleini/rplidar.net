using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RPLidar
{
    /// <summary>
    /// RPLidar device
    /// </summary>
    public partial class Lidar
    {
        /// <summary>
        /// Command byte
        /// </summary>
        private enum Command : byte
        {
            GetInfo = 0x50,
            GetHealth = 0x52,
            GetConfig = 0x84,
            Stop = 0x25,
            Reset = 0x40,
            Scan = 0x20,
            ExpressScan = 0x82,
        }

        /// <summary>
        /// Config type word
        /// </summary>
        private enum ConfigType : uint
        {
            ScanModeCount = 0x70,
            ScanModeUsPerSample = 0x71,
            ScanModeMaxDistance = 0x74,
            ScanModeAnswerType = 0x75,
            ScanModeTypical = 0x7C,
            ScanModeName = 0x7F,
        }

        // Constants
        private const byte SyncByte = 0xA5;
        private const byte SyncByte2 = 0x5A;
        private const int DescriptorLength = 7;
        private readonly Descriptor InfoDescriptor = new Descriptor(20, true, 0x04);
        private readonly Descriptor HealthDescriptor = new Descriptor(3, true, 0x06);
        private readonly Descriptor LegacyScanDescriptor = new Descriptor(5, false, 0x81);
        private readonly Descriptor ExpressLegacyScanDescriptor = new Descriptor(84, false, 0x82);

        // Variables
        private readonly SerialPort port;

        /// <summary>
        /// Constructor
        /// </summary>
        public Lidar()
        {
            port = new SerialPort()
            {
                ReadTimeout = 1000,
                ReadBufferSize = 32768,
                BaudRate = 115200
            };
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="portName">Port name</param>
        /// <param name="baudRate">Baud rate</param>
        /// <param name="readBufferSize">Read buffer size in bytes</param>
        public Lidar(string portName = "", int baudRate = 115200, int readBufferSize = 4096)
            : this()
        {
            PortName = portName;
            Baudrate = baudRate;
            ReadBufferSize = readBufferSize;
        }

        /// <summary>
        /// Logger
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Port name
        /// </summary>
        public string PortName
        {
            get => port.PortName;
            set => port.PortName = value;
        }

        /// <summary>
        /// Baud rate
        /// </summary>
        public int Baudrate
        {
            get => port.BaudRate;
            set => port.BaudRate = value;
        }

        /// <summary>
        /// Read buffer size
        /// </summary>
        public int ReadBufferSize
        {
            get => port.ReadBufferSize;
            set => port.ReadBufferSize = value;
        }

        /// <summary>
        /// Receive timeout in milliseconds
        /// </summary>
        public int ReceiveTimeout
        {
            get => port.ReadTimeout;
            set => port.ReadTimeout = value;
        }

        /// <summary>
        /// Is port open ?
        /// </summary>
        public bool IsOpen
        {
            get
            {
                try
                {
                    return port.IsOpen;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error at checking port status.");
                    return false;
                }
            }
        }

        /// <summary>
        /// Angle offset in degrees
        /// This value is added to the measurements angle
        /// </summary>
        public float AngleOffset { get; set; }

        /// <summary>
        /// True if lidar is flipped (upside down), false it it's in upright position
        /// </summary>
        public bool IsFlipped { get; set; }

        /// <summary>
        /// Angle multiplier
        /// </summary>
        private float AngleMultiplier => IsFlipped ? -1.0f : 1.0f;

        /// <summary>
        /// Try to open lidar port
        /// </summary>
        /// <returns>true if port was opened, false if it failed</returns>
        public bool Open()
        {
            try
            {
                if (!port.IsOpen)
                {
                    port.Open();
                }

                return port.IsOpen;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error at opening port.");
                return false;
            }
        }

        /// <summary>
        /// Try to close lidar port
        /// </summary>
        /// <returns>true if port was closed, false if it failed</returns>
        public bool Close()
        {
            try
            {
                if (port.IsOpen)
                {
                    port.Close();
                }

                return !port.IsOpen;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error at closing port.");
                return false;
            }
        }

        /// <summary>
        /// Get number of bytes available for reading from port
        /// </summary>
        private bool GetBytesToRead(out int count)
        {
            try
            {
                count = port.BytesToRead;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error at checking readable bytes count.");
                count = 0;
                return false;
            }
        }

        /// <summary>
        /// Timestamp in milliseconds
        /// </summary>
        public long Timestamp => Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);

        /// <summary>
        /// Control motor via serial DTR pin
        /// </summary>
        /// <param name="onOff">true to turn on motor, false to turn off</param>
        public void ControlMotorDtr(bool onOff)
        {
            port.DtrEnable = onOff;
        }

        /// <summary>
        /// Send command
        /// </summary>
        /// <param name="command">Command</param>
        /// <param name="commandName">Name of the command</param>
        /// <returns>true if sent, false if failed</returns>
        private bool SendCommand(Command command, string commandName)
        {
            try
            {
                port.Write(new byte[2] { SyncByte, (byte)command }, 0, 2);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error at sending {commandName} command.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Send command
        /// </summary>
        /// <param name="command">Command</param>
        /// <param name="payload">Payload</param>
        /// <param name="commandName">Name of the command</param>
        /// <returns>true if sent, false if failed</returns>
        private bool SendCommand(Command command, byte[] payload, string commandName)
        {
            byte[] data = new byte[4 + payload.Length];
            byte checksum = 0;

            data[0] = SyncByte;
            data[1] = (byte)command;
            data[2] = (byte)payload.Length;
            Array.Copy(payload, 0, data, 3, payload.Length);

            for (int i = 0; i < data.Length; i++)
            {
                checksum ^= data[i];
            }

            data[3 + payload.Length] = checksum;

            try
            {
                port.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error at sending command {commandName}.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Read descriptor
        /// </summary>
        /// <param name="responseName">Name of the response</param>
        /// <returns>Descriptor if succeeded, null on failure</returns>
        private Descriptor ReadDescriptor(string responseName)
        {
            List<byte> queue = new List<byte>();
            byte[] buffer = new byte[64];
            int missingBytes;
            int bytesRead;
            long startTime = Timestamp;

            while (true)
            {
                // Try to receive as many bytes as are missing from complete packet
                missingBytes = DescriptorLength - queue.Count;
                try
                {
                    bytesRead = port.Read(buffer, 0, missingBytes);
                }
                catch (TimeoutException)
                {
                    Logger.LogError($"Timeout at receiving descriptor for {responseName}.");
                    return null;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Error at receiving descriptor for {responseName}.");
                    return null;
                }

                // Add bytes to the queue and check if we have complete descriptor
                queue.AddRange(buffer.Take(bytesRead));
                while (queue.Count >= DescriptorLength)
                {
                    if ((queue[0] == SyncByte) && (queue[1] == SyncByte2))
                    {
                        // Seems like we got our descriptor
                        // TODO Should consider with lengths above 255 ?
                        return new Descriptor(queue[2], queue[5] == 0, queue[6]);
                    }
                    else
                    {
                        // Pop first byte and check for sync bytes again
                        queue.RemoveAt(0);
                    }
                }

                // Timeout ?
                if ((Timestamp - startTime) > ReceiveTimeout)
                {
                    Logger.LogError($"Timeout at receiving descriptor for {responseName}.");
                    return null;
                }
            }
        }

        /// <summary>
        /// Check read descriptor against expected
        /// </summary>
        /// <param name="readDescriptor">Read descriptor</param>
        /// <param name="expectedDescriptor">Expected descriptor</param>
        /// <param name="responseName">Name of the response</param>
        /// <returns>true if descriptor received, false if not</returns>
        private bool CheckDescriptor(Descriptor readDescriptor, Descriptor expectedDescriptor, string responseName)
        {
            if ((expectedDescriptor.Length >= 0) && (expectedDescriptor.Length != readDescriptor.Length))
            {
                Logger.LogError($"Expected {responseName} descriptor length {expectedDescriptor.Length}, got {readDescriptor.Length}.");
                return false;
            }

            if (expectedDescriptor.IsSingle != readDescriptor.IsSingle)
            {
                Logger.LogError($"Expected {responseName} descriptor single to be {expectedDescriptor.IsSingle}, got {readDescriptor.IsSingle}.");
                return false;
            }

            if (expectedDescriptor.DataType != readDescriptor.DataType)
            {
                Logger.LogError($"Expected {responseName} descriptor data type 0x{expectedDescriptor.DataType:X2}, got 0x{readDescriptor.DataType:X2}.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Wait for descriptor
        /// </summary>
        /// <param name="expectedDescriptor">Descriptor to wait for</param>
        /// <param name="responseName">Name of the response</param>
        /// <returns>true if descriptor received, false if not</returns>
        private bool WaitForDescriptor(Descriptor expectedDescriptor, string responseName)
        {
            Descriptor readDescriptor = ReadDescriptor(responseName);
            if (readDescriptor == null)
            {
                return false;
            }

            return CheckDescriptor(readDescriptor, expectedDescriptor, responseName);
        }

        /// <summary>
        /// Read response
        /// </summary>
        /// <param name="length">Number of bytes to wait for</param>
        /// <param name="responseName">Name of the response</param>
        /// <returns>Received data bytes or empty array in case of error</returns>
        private byte[] ReadResponse(int length, string responseName)
        {
            int dataIndex = 0;
            int bytesRead;
            long startTime = Timestamp;
            byte[] data = new byte[length];

            // Get required number of data bytes
            while (dataIndex < length)
            {
                try
                {
                    bytesRead = port.Read(data, dataIndex, length - dataIndex);
                }
                catch (TimeoutException)
                {
                    Logger.LogError($"Timeout at receiving data for {responseName}.");
                    return Array.Empty<byte>();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Error at receiving data for {responseName}.");
                    return Array.Empty<byte>();
                }

                dataIndex += bytesRead;

                // Timeout ?
                if ((Timestamp - startTime) > ReceiveTimeout)
                {
                    Logger.LogError($"Timeout at receiving data for {responseName}.");
                    return Array.Empty<byte>();
                }
            }

            return data;
        }

        /// <summary>
        /// Flush input buffer
        /// </summary>
        private bool FlushInput()
        {
            try
            {
                port.DiscardInBuffer();
                port.BaseStream.Flush();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error on flushing input buffer.");
                return false;
            }
        }

        /// <summary>
        /// Reset lidar
        /// 
        /// Note: RPLidar specification says that after reset it takes only 2 ms until lidar is ready to receive
        ///       new commands, however practical tests show that it takes about 700 ms and it also sends out
        ///       it's version info which must be read or flushed to avoid disturbing following requests.
        /// </summary>
        /// <param name="waitTime">Number of milliseconds to wait until lidar has restarted</param>
        /// <returns>true if success, false if not</returns>
        public bool Reset(int waitTime = 700)
        {
            // Send reset command
            if (!SendCommand(Command.Reset, "reset"))
            {
                return false;
            }

            // Flush inputs little bit after sending reset command because of the full-duplex and asynchronous operation of the serial interface
            Task.Delay(10);
            FlushInput();
            ClearScanBuffer();

            // This is the delay to let lidar truly start up
            if (waitTime > 10)
            {
                Task.Delay(waitTime - 10);
            }

            // If there's something in the input buffer then read it all out and display it
            if (!GetBytesToRead(out int length))
            {
                return false;
            }

            if (length > 0)
            {
                byte[] data = ReadResponse(length, "reset response");
                if (data.Length == 0)
                {
                    return false;
                }

                // Convert to ASCII and replace control characters with space
                string msg = new string(data.Select(b => b < 0x20 ? ' ' : (char)b).ToArray());
                Logger.LogInformation($"Reset message: {msg}");
            }

            return true;
        }

        /// <summary>
        /// Get info
        /// </summary>
        /// <returns>Lidar info in case of success or null in case of failure</returns>
        public LidarInfo GetInfo()
        {
            if (!SendCommand(Command.GetInfo, "get info"))
            {
                return null;
            }

            if (!WaitForDescriptor(InfoDescriptor, "get info"))
            {
                return null;
            }

            byte[] data = ReadResponse(InfoDescriptor.Length, "get info");
            if (data.Length == 0)
            {
                return null;
            }

            // Decode lidar info and return it
            return new LidarInfo()
            {
                Model = data[0],
                Firmware = $"{data[2]}.{data[1]}",
                Hardware = data[3].ToString(),
                SerialNumber = BitConverter.ToString(data, 4).Replace("-", string.Empty)
            };
        }

        /// <summary>
        /// Get health
        /// </summary>
        /// <returns>Health info in case of success or null in case of failure</returns>
        public HealthInfo GetHealth()
        {
            if (!SendCommand(Command.GetHealth, "get health"))
            {
                return null;
            }

            if (!WaitForDescriptor(HealthDescriptor, "get health"))
            {
                return null;
            }

            byte[] data = ReadResponse(HealthDescriptor.Length, "get health");
            if (data.Length == 0)
            {
                return null;
            }

            // Decode health info and return it
            return new HealthInfo()
            {
                Status = (HealthStatus)data[0],
                ErrorCode = BitConverter.ToUInt16(data, 1)
            };
        }

        /// <summary>
        /// Get lidar configuration type
        /// </summary>
        /// <param name="configType">Configuration type</param>
        /// <param name="requestPayload">Extra request payload bytes</param>
        /// <param name="expectedResponseLength">Expected response payload length</param>
        /// <param name="responsePayload">Response payload bytes</param>
        /// <returns>Response payload bytes in case of success or empty array in case of failure</returns>
        private byte[] GetConfigurationType(ConfigType configType, byte[] requestPayload, int? expectedResponseLength)
        {
            string responseName = "get config";

            Descriptor expectedDescriptor = new Descriptor(-1, true, 0x20);
            if (expectedResponseLength.HasValue)
            {
                expectedDescriptor.Length = expectedResponseLength.Value + 4;
            }

            if (!SendCommand(Command.GetConfig, BitConverter.GetBytes((uint)configType).Concat(requestPayload).ToArray(), responseName))
            {
                return Array.Empty<byte>();
            }

            Descriptor readDescriptor = ReadDescriptor(responseName);
            if (readDescriptor == null)
            {
                return Array.Empty<byte>();
            }

            if (!CheckDescriptor(readDescriptor, expectedDescriptor, responseName))
            {
                return Array.Empty<byte>();
            }

            byte[] responseRaw = ReadResponse(readDescriptor.Length, responseName);
            if (responseRaw.Length == 0)
            {
                return Array.Empty<byte>();
            }

            // Verify response type
            uint responseType = BitConverter.ToUInt32(responseRaw, 0);
            if (responseType != (byte)configType)
            {
                Logger.LogError($"Expected {responseName} response type 0x{configType:X2}, got 0x{responseType:X2}.");
                return Array.Empty<byte>();
            }

            // Get response payload bytes only
            return responseRaw.Skip(4).ToArray();
        }

        /// <summary>
        /// Get lidar configuration
        /// </summary>
        /// <returns>Configuration in case of success or null in case of failure</returns>
        public Configuration GetConfiguration()
        {
            var configuration = new Configuration();

            // Get typical mode
            byte[] response = GetConfigurationType(ConfigType.ScanModeTypical, Array.Empty<byte>(), 2);
            if (response.Length == 0)
            {
                return null;
            }

            configuration.Typical = BitConverter.ToUInt16(response, 0);

            // Get number of modes
            response = GetConfigurationType(ConfigType.ScanModeCount, Array.Empty<byte>(), 2);
            if (response.Length == 0)
            {
                return null;
            }

            ushort count = BitConverter.ToUInt16(response, 0);

            // Create configurations of all modes
            configuration.Modes = new Dictionary<ushort, ScanModeConfiguration>();

            for (ushort mode = 0; mode < count; mode++)
            {
                ScanModeConfiguration modeConfiguration = new ScanModeConfiguration();
                byte[] modeBytes = BitConverter.GetBytes(mode);

                // Get mode name
                response = GetConfigurationType(ConfigType.ScanModeName, modeBytes, null);
                if (response.Length == 0)
                {
                    return null;
                }

                modeConfiguration.Name = Encoding.ASCII.GetString(response).TrimEnd('\0');

                // Get microseconds per sample
                response = GetConfigurationType(ConfigType.ScanModeUsPerSample, modeBytes, 4);
                if (response.Length == 0)
                {
                    return null;
                }

                modeConfiguration.UsPerSample = (float)BitConverter.ToUInt32(response, 0) / 256.0f;

                // Get maximum distance
                response = GetConfigurationType(ConfigType.ScanModeMaxDistance, modeBytes, 4);
                if (response.Length == 0)
                {
                    return null;
                }

                modeConfiguration.MaxDistance = (float)BitConverter.ToUInt32(response, 0) / 256.0f;

                // Ge answer type
                response = GetConfigurationType(ConfigType.ScanModeAnswerType, modeBytes, 1);
                if (response.Length == 0)
                {
                    return null;
                }

                modeConfiguration.AnswerType = response[0];

                // Add to list
                configuration.Modes.Add(mode, modeConfiguration);
            }

            return configuration;
        }

        /// <summary>
        /// Class to string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "Lidar";
        }
    }
}
