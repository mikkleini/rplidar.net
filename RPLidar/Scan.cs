using System;
using System.Collections.Generic;
using System.Text;

namespace RPLidar
{
    /// <summary>
    /// Scan (collection of 360 degrees of measurements)
    /// </summary>
    public class Scan
    {
        /// <summary>
        /// Measurements
        /// </summary>
        public List<Measurement> Measurements { get; } = new List<Measurement>();

        /// <summary>
        /// Scan duration in milliseconds
        /// </summary>
        public int Duration { get; internal set; } = 0;

        /// <summary>
        /// Scan count in a second
        /// </summary>
        public float ScanRate { get; internal set; } = 0;
    }
}
