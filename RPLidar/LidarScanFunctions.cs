using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace RPLidar
{
    /// <summary>
    /// This file contains lidar scanning functions
    /// </summary>
    public partial class Lidar
    {
        // Variables
        private readonly List<Measurement> bufferedMeasurements = new List<Measurement>();
        private ScanMode? activeMode = null;
        private int lastExpressScanAngle = 0;
        private int bufferedMeasurementsIndex = 0;
        private long? lastScanTimestamp = null;

        /// <summary>
        /// Start chosen scan mode
        /// </summary>
        /// <param name="mode">Scan mode</param>
        /// <returns>true if success, false if not</returns>
        /// <remarks>Check configuration if scan mode is supported</remarks>
        public bool StartScan(ScanMode mode)
        {
            switch (mode)
            {
                case ScanMode.Legacy:
                    if (!SendCommand(Command.Scan)) return false;
                    if (!WaitForDescriptor(LegacyScanDescriptor)) return false;
                    activeMode = ScanMode.Legacy;
                    return true;

                case ScanMode.ExpressLegacy:
                    if (!SendCommand(Command.ExpressScan, new byte[5] { 0, 0, 0, 0, 0 })) return false;
                    if (!WaitForDescriptor(ExpressLegacyScanDescriptor)) return false;
                    activeMode = ScanMode.ExpressLegacy;
                    return true;

                case ScanMode.ExpressExtended:
                    throw new NotSupportedException("Express extended scan not yet supported");

                default:
                    throw new Exception("Invalid scan mode, could be a bug");
            }
        }

        /// <summary>
        /// Stop scan
        /// </summary>
        /// <returns>true if success, false if not</returns>
        public bool StopScan()
        {
            if (!SendCommand(Command.Stop)) return false;
            Thread.Sleep(1);

            FlushInput();
            ClearScanBuffer();
            activeMode = null;
            return true;
        }

        /// <summary>
        /// Clear scan buffer
        /// </summary>
        private void ClearScanBuffer()
        {
            bufferedMeasurements.Clear();
            bufferedMeasurementsIndex = 0;
        }

        /// <summary>
        /// Get scan
        /// </summary>
        /// <param name="scan">If scan is ready then returns measurements, otherwise null</param>
        /// <returns>true of operation succeedef, false if not</returns>
        /// <remarks>Do not use this function when using GetMeasurements</remarks>
        public bool GetScan(out Scan scan)
        {
            scan = null;

            // Get all the new measurements
            if (!GetMeasurements(bufferedMeasurements)) return false;

            // Look for new measurements
            for (; bufferedMeasurementsIndex < bufferedMeasurements.Count; bufferedMeasurementsIndex++)
            {
                // If it's new and not first measurement then it means the scan has finished
                if ((bufferedMeasurementsIndex > 0) && (bufferedMeasurements[bufferedMeasurementsIndex].IsNewScan))
                {
                    scan = new Scan();

                    // Calculate scan timestamp
                    // Well, it's accuracy depends on the scanning rate
                    long timestampNow = Timestamp;
                    if (lastScanTimestamp.HasValue)
                    {
                        scan.Duration = (int)(timestampNow - lastScanTimestamp);
                    }
                    lastScanTimestamp = timestampNow;

                    // Move buffered measurements to scan
                    scan.Measurements.AddRange(bufferedMeasurements.Take(bufferedMeasurementsIndex));
                    bufferedMeasurements.RemoveRange(0, bufferedMeasurementsIndex);
                    bufferedMeasurementsIndex = 0;
                    return true;
                }
            }

            // Scan not yet ready
            return true;
        }

        /// <summary>
        /// Get new measurements
        /// </summary>
        /// <param name="measurements">List which will be updated</param>
        /// <returns>true if operation succeeded, false if something failed</returns>
        /// <remarks>Operation succeeds even if no new measurements are added to the buffer</remarks>
        /// <remarks>Do not use this function when using GetScan !</remarks>
        public bool GetMeasurements(IList<Measurement> measurements)
        {
            // Check port buffer utilization and give warning if it's too high
            int usage = (100 * BytesToRead) / ReadBufferSize;
            if (usage > 50)
            {
                Log($"Receive buffer is {usage}% full, should read measurements faster", Severity.Warning);
            }

            // Do the read based on mode
            switch (activeMode)
            {
                case null:
                    Log("No scan mode active", Severity.Error);
                    return false;

                case ScanMode.Legacy:
                    return GetLegacyMeasurements(measurements);

                case ScanMode.ExpressLegacy:
                    return GetExpressLegacyMeasurements(measurements);

                case ScanMode.ExpressExtended:
                    throw new NotSupportedException("Express extended scan not yet supported");

                default:
                    throw new Exception("Invalid scan mode, could be a bug");
            }
        }

        /// <summary>
        /// Get legacy measurements
        /// </summary>
        /// <param name="measurements">Measurements destination list which gets updated</param>
        /// <returns>true if measurements received, false if something failed</returns>
        private bool GetLegacyMeasurements(IList<Measurement> measurements)
        {
            // Read all 5 byte packets
            if (!ReadResponse((BytesToRead / 5) * 5, out byte[] buffer)) return false;

            // Parse all packets as 5 byte chunks
            for (int i = 0; i < buffer.Length; i += 5)
            {
                bool isNewScan = (buffer[i] & 1) != 0;
                bool isNewScan2 = (buffer[i] & 2) != 0;

                // Scan flags are inverted ?
                if (isNewScan == isNewScan2)
                {
                    Log("Receieved invalid scan data (start flags not inverted)", Severity.Error);
                    return false;
                }

                // Check bit set ?
                if ((buffer[i + 1] & 1) != 1)
                {
                    Log("Receieved invalid scan data (check bit not set)", Severity.Error);
                    return false;
                }

                // Get angle, distance and quality
                float angle = ((buffer[i + 2] << 7) | (buffer[i + 1] >> 1)) / 64.0f;
                float distance = (((buffer[i + 4] << 8) | buffer[i + 3]) / 4.0f) * 1000.0f;
                int quality = buffer[i] >> 2;

                // Add measurement
                measurements.Add(new Measurement(isNewScan, angle, distance, quality));
            }

            return true;
        }

        /// <summary>
        /// Get express legacy measurements
        /// </summary>
        /// <param name="measurements">Measurements destination list which gets updated</param>
        /// <returns>true if measurements received, false if something failed</returns>
        private bool GetExpressLegacyMeasurements(IList<Measurement> measurements)
        {
            // Read and parse as many packets as are available
            while (BytesToRead >= ExpressLegacyScanDescriptor.Length)
            {
                if (!ReadResponse(ExpressLegacyScanDescriptor.Length, out byte[] buffer)) return false;
                if (!ParseExpressLegacyMeasurementsPacket(buffer, measurements)) return false;
            }

            return true;
        }

        /// <summary>
        /// Parse express legacy mode scan packet
        /// </summary>
        /// <param name="buffer">Packet payload</param>
        /// <param name="measurements">Measurements destination list which gets updated</param>
        /// <returns>true if measurements received, false if something failed</returns>
        private bool ParseExpressLegacyMeasurementsPacket(byte[] buffer, IList<Measurement> measurements)
        {
            int i;

            // Verify sync bits
            if (((buffer[0] >> 4) != 0xA) || ((buffer[1] >> 4) != 0x5))
            {
                Log("Received invalid scan packet (invalid sync)", Severity.Error);
                return false;
            }

            // Calculate and verify checksum
            byte checksum = 0;
            for (i = 2; i < buffer.Length; i++)
            {
                checksum ^= buffer[i];
            }

            if (checksum != ((buffer[0] & 0x0F) | ((buffer[1] & 0x0F) << 4)))
            {
                Log("Received invalid scan packet (invalid checksum)", Severity.Error);
                return false;
            }

            // Is it a new scan ?
            // This is actually not working properly - it's only set once. A bug in RPLidar firmware ?
            bool newScan = (buffer[3] >> 7) == 1;

            // Parse start angle
            int startAngleq6 = buffer[2] | ((buffer[3] & 0x7F) << 8);

            // Parse 16 cabins
            for (i = 4; i < buffer.Length; i += 5)
            {
                float dist1 = ((buffer[i + 0] >> 2) | (buffer[i + 1] << 6)) / 1000.0f;
                float dist2 = ((buffer[i + 2] >> 2) | (buffer[i + 3] << 6)) / 1000.0f;

                // Count absolute angular position in fixed point math to avoid accumulating error which would happen with float
                int da1q6 = (buffer[i + 4] >> 4) | ((buffer[i + 0] & 0x3) << 4);
                int da2q6 = (buffer[i + 4] & 0x3) | ((buffer[i + 2] & 0x3) << 4);

                startAngleq6 += da1q6 * 8;
                float angle1 = startAngleq6 / 64.0f;
                startAngleq6 += da2q6 * 8;
                float angle2 = startAngleq6 / 64.0f;

                // Store measurements
                measurements.Add(new Measurement(newScan, angle1, dist1));
                measurements.Add(new Measurement(false, angle2, dist2));

                // Report it only for first measurement
                newScan = false;
            }

            return true;
        }
    }
}
