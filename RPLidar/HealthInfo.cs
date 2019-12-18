using System;
using System.Collections.Generic;
using System.Text;

namespace RPLidar
{
    /// <summary>
    /// Health info
    /// </summary>
    public class HealthInfo
    {
        /// <summary>
        /// Health status
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Error code
        /// </summary>
        public ushort ErrorCode { get; set; }

        /// <summary>
        /// Class to string
        /// </summary>
        /// <returns>Text</returns>
        public override string ToString()
        {
            return $"Health: {Status}, Error code: {ErrorCode}";
        }
    }
}
