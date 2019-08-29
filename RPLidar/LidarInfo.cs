using System;
using System.Collections.Generic;
using System.Text;

namespace RPLidar
{
    /// <summary>
    /// Lidar info
    /// </summary>
    public class LidarInfo
    {
        /// <summary>
        /// Model number
        /// </summary>
        public byte Model { get; internal set; }

        /// <summary>
        /// Firmware number
        /// </summary>
        public string Firmware { get; internal set; }

        /// <summary>
        /// Hardwre number
        /// </summary>
        public string Hardware { get; internal set; }

        /// <summary>
        /// Serial number
        /// </summary>
        public string SerialNumber { get; internal set; }

        /// <summary>
        /// Class to string
        /// </summary>
        /// <returns>Text</returns>
        public override string ToString()
        {
            return $"Model number: {Model}, Firmware version: {Firmware}, Hardware version: {Hardware}, Serial number: {SerialNumber}";
        }
    }
}
