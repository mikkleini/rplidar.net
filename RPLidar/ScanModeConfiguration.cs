using System;
using System.Collections.Generic;
using System.Text;

namespace RPLidar
{
    /// <summary>
    /// Scan mode configuration
    /// </summary>
    public class ScanModeConfiguration
    {
        /// <summary>
        /// Scan mode name
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Microseconds per sample
        /// </summary>
        public float UsPerSample { get; internal set; }

        /// <summary>
        /// Distance in meters
        /// </summary>
        public float MaxDistance { get; internal set; }

        /// <summary>
        /// Answer type
        /// </summary>
        public byte AnswerType { get; internal set; }

        /// <summary>
        /// Scan mode to string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Name: {Name}, Ts: {UsPerSample:f2} us, Max distance: {MaxDistance:f2} m, Answer type: 0x{AnswerType:X2}";
        }
    }
}
