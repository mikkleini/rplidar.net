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
        // Constants
        private const int MeasurementsInExpressLegacyScanPacket = 32;

        // Variables
        private readonly List<Measurement> bufferedMeasurements = new List<Measurement>();
        private readonly List<Measurement> bufferedExpressMeasurements = new List<Measurement>();
        private ScanMode? activeMode = null;
        private float? lastExpressScanStartAngle = null;
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
                    if (!SendCommand(Command.Scan, "scan")) return false;
                    if (!WaitForDescriptor(LegacyScanDescriptor, "scan")) return false;
                    activeMode = ScanMode.Legacy;
                    return true;

                case ScanMode.ExpressLegacy:
                    if (!SendCommand(Command.ExpressScan, new byte[5] { 0, 0, 0, 0, 0 }, "express legacy scan")) return false;
                    if (!WaitForDescriptor(ExpressLegacyScanDescriptor, "express legacy scan")) return false;
                    activeMode = ScanMode.ExpressLegacy;
                    return true;

                case ScanMode.ExpressExtended:
                    logger.Error("Express extended scan not yet supported.");
                    return false;

                default:
                    logger.Fatal("Invalid scan mode, could be a bug.");
                    return false;
            }
        }

        /// <summary>
        /// Stop scan
        /// </summary>
        /// <returns>true if success, false if not</returns>
        public bool StopScan()
        {
            if (!SendCommand(Command.Stop, "stop")) return false;
            Thread.Sleep(10); // Spec requires 1 ms but leave some time for serial port to act

            // Flush inputs
            FlushInput();
            ClearScanBuffer();

            return true;
        }

        /// <summary>
        /// Clear scan buffer
        /// </summary>
        private void ClearScanBuffer()
        {
            activeMode = null;
            bufferedMeasurements.Clear();
            bufferedExpressMeasurements.Clear();
            bufferedMeasurementsIndex = 0;
            lastExpressScanStartAngle = null;
        }

        /// <summary>
        /// Get scan data.
        /// Poll this function until full scan object is returned.
        /// It can only return one 360 degrees scan at once.
        /// </summary>
        /// <param name="scan">If scan is ready then returns Scan object, otherwise null</param>
        /// <returns>true of operation succeeded, false if not</returns>
        /// <remarks>Do not use this function while using GetMeasurements or GetMeasurementsUntilNew !</remarks>
        public bool GetScan(out Scan scan)
        {
            scan = null;

            // Get all the received measurements
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

            return true;
        }

        /// <summary>
        /// Get measurements until the new scan.
        /// This is for quick reception of measurement without waiting for the whole 360 scan but compared to
        /// the GetMeasurements it doesn't mix up previous and new scan measurements.
        /// </summary>
        /// <param name="measurements">List which will be updated</param>
        /// <param name="isLastChunk">Returns true if this is the last chunk of measurements before new scan starts</param>
        /// <returns>true if operation succeeded, false if something failed</returns>
        /// <remarks>Operation can succeed even if no new measurements are added to the list</remarks>
        /// <remarks>Do not use this function while using GetScan or GetMeasurements!</remarks>
        public bool GetMeasurementsUntilNew(List<Measurement> measurements, out bool isLastChunk)
        {
            isLastChunk = false;

            // Get all the received measurements
            if (!GetMeasurements(bufferedMeasurements)) return false;

            // Look for new measurements
            for (; bufferedMeasurementsIndex < bufferedMeasurements.Count; bufferedMeasurementsIndex++)
            {
                // If it's new and not first measurement then it means one scan has finished.
                if ((bufferedMeasurementsIndex > 0) && (bufferedMeasurements[bufferedMeasurementsIndex].IsNewScan))
                {
                    isLastChunk = true;

                    // Remove already returned measurements from buffer
                    bufferedMeasurements.RemoveRange(0, bufferedMeasurementsIndex);
                    bufferedMeasurementsIndex = 0;
                    break;
                }
                else
                {
                    measurements.Add(bufferedMeasurements[bufferedMeasurementsIndex]);
                }
            }

            return true;
        }

        /// <summary>
        /// Get measurements.
        /// This is for quick reception of measurement without waiting for the whole 360 scan.
        /// </summary>
        /// <param name="measurements">List which will be updated</param>
        /// <returns>true if operation succeeded, false if something failed</returns>
        /// <remarks>Operation can succeed even if no new measurements are added to the list</remarks>
        /// <remarks>Do not use this function while using GetScan or GetMeasurementsUntilNew!</remarks>
        public bool GetMeasurements(List<Measurement> measurements)
        {
            // Check port buffer utilization and give warning if it's too high
            if (!GetBytesToRead(out int bytesToRead)) return false;
            int usage = (100 * bytesToRead) / ReadBufferSize;
            if (usage > 50)
            {
                logger.Warn($"Receive buffer is {usage}% full, should read measurements faster.");
            }

            // Do the read based on mode
            switch (activeMode)
            {
                case null:
                    logger.Error("No scan mode active.");
                    return false;

                case ScanMode.Legacy:
                    return GetLegacyMeasurements(measurements);

                case ScanMode.ExpressLegacy:
                    return GetExpressLegacyMeasurements(measurements);

                case ScanMode.ExpressExtended:
                    logger.Error("Express extended scan not yet supported.");
                    return false;

                default:
                    throw new Exception("Invalid scan mode, could be a bug");
            }
        }

        /// <summary>
        /// Get legacy measurements
        /// </summary>
        /// <param name="measurements">Measurements destination list which gets updated</param>
        /// <returns>true if measurements received, false if something failed</returns>
        private bool GetLegacyMeasurements(List<Measurement> measurements)
        {
            // Read all fully available 5 byte packets
            if (!GetBytesToRead(out int bytesToRead)) return false;
            if (!ReadResponse((bytesToRead / 5) * 5, out byte[] buffer, "legacy scan")) return false;

            // Parse all packets as 5 byte chunks
            for (int i = 0; i < buffer.Length; i += 5)
            {
                bool isNewScan = (buffer[i] & 1) != 0;
                bool isNewScan2 = (buffer[i] & 2) != 0;

                // Scan flags are inverted ?
                if (isNewScan == isNewScan2)
                {
                    logger.Error("Receieved invalid scan data (start flags not inverted).");
                    return false;
                }

                // Check bit set ?
                if ((buffer[i + 1] & 1) != 1)
                {
                    logger.Error("Receieved invalid scan data (check bit not set).");
                    return false;
                }

                // Get angle, distance and quality
                float angle = ((buffer[i + 2] << 7) | (buffer[i + 1] >> 1)) / 64.0f;
                float distance = (((buffer[i + 4] << 8) | buffer[i + 3]) / 4.0f) / 1000.0f;
                int quality = buffer[i] >> 2;

                // Do user angular offset calculation
                angle = (angle * AngleMultiplier + AngleOffset) % 360.0f;

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
        private bool GetExpressLegacyMeasurements(List<Measurement> measurements)
        {
            // Read and parse if at least two scan packets are available because
            // the next packet start angle is used to calculate absolute angle of previous scan samples
            while (true)
            {
                if (!GetBytesToRead(out int bytesToRead)) return false;
                if (bytesToRead < ExpressLegacyScanDescriptor.Length) return true;
                if (!ReadResponse(ExpressLegacyScanDescriptor.Length, out byte[] buffer, "express legacy scan")) return false;
                if (!ParseExpressLegacyMeasurementsPacket(buffer, bufferedExpressMeasurements, out float startAngle)) return false;

                // Previous start angle available ?
                if (lastExpressScanStartAngle.HasValue)
                {
                    // Get angular difference between this packet start angle and previous packet start angle
                    // and then calculate the each measurement fraction of that
                    float diffAngle = (startAngle - lastExpressScanStartAngle.Value + 360.0f) % 360.0f;
                    float angleFraction = diffAngle / (float)MeasurementsInExpressLegacyScanPacket;

                    // Sanity check
                    if (bufferedExpressMeasurements.Count != MeasurementsInExpressLegacyScanPacket * 2)
                    {
                        throw new Exception("Bug in express scan logic");
                    }

                    // Calculate real angles of previous packet measurements
                    // Also add user angular offset
                    for (int i = 0; i < MeasurementsInExpressLegacyScanPacket; i++)
                    {
                        Measurement measurement = new Measurement();

                        // Full rotation done ?
                        if ((i == 0) && (startAngle < lastExpressScanStartAngle.Value))
                        {
                            measurement.IsNewScan = true;
                        }

                        // Calculate absolute angle
                        measurement.Angle = ((lastExpressScanStartAngle.Value + angleFraction * i - bufferedExpressMeasurements[i].Angle) * AngleMultiplier + AngleOffset) % 360.0f;

                        // Copy the distance
                        measurement.Distance = bufferedExpressMeasurements[i].Distance;

                        // Update measurement
                        bufferedExpressMeasurements[i] = measurement;
                    }

                    // Move previous packet measurements to return list
                    measurements.AddRange(bufferedExpressMeasurements.Take(MeasurementsInExpressLegacyScanPacket));
                    bufferedExpressMeasurements.RemoveRange(0, MeasurementsInExpressLegacyScanPacket);
                }

                // Remember this packets angle
                lastExpressScanStartAngle = startAngle;
            }
        }

        /// <summary>
        /// Parse express legacy mode scan packet
        /// Note: it does not return measurements with absolute angle!
        /// </summary>
        /// <param name="buffer">Packet payload</param>
        /// <param name="measurements">Measurements destination list which gets updated</param>
        /// <param name="startAngle">Start angle of this packet measurements</param>
        /// <returns>true if measurements received, false if something failed</returns>
        private bool ParseExpressLegacyMeasurementsPacket(byte[] buffer, List<Measurement> measurements, out float startAngle)
        {
            int i;
            startAngle = 0.0f;

            // Verify sync bits
            if (((buffer[0] >> 4) != 0xA) || ((buffer[1] >> 4) != 0x5))
            {
                logger.Error("Received invalid scan packet (invalid sync).");
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
                logger.Error("Received invalid scan packet (invalid checksum).");
                return false;
            }

            // Is it a new scan ?
            // This is only set on first packet after stable start and
            // when stable operation is restored after some motor instability
            bool newScan = (buffer[3] >> 7) == 1;

            // Parse start angle
            startAngle = (buffer[2] | ((buffer[3] & 0x7F) << 8)) / 64.0f;

            // Parse 16 cabins (32 measurements)
            for (i = 4; i < buffer.Length; i += 5)
            {
                // Get distance in meters
                float dist1 = ((buffer[i + 0] >> 2) | (buffer[i + 1] << 6)) / 1000.0f;
                float dist2 = ((buffer[i + 2] >> 2) | (buffer[i + 3] << 6)) / 1000.0f;

                // Get compensation angles (absolute angles will be calculated later)
                float da1 = ((buffer[i + 4] & 0xF) | ((buffer[i + 0] & 0x3) << 4)) / 8.0f;
                float da2 = ((buffer[i + 4] >> 4) | ((buffer[i + 2] & 0x3) << 4)) / 8.0f;

                // Store par of measurements
                measurements.Add(new Measurement(newScan, da1, dist1));
                measurements.Add(new Measurement(false, da2, dist2));

                // Report new scan only on first measurement
                newScan = false;
            }

            return true;
        }
    }
}
