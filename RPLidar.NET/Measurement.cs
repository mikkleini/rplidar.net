using System;
using System.Collections.Generic;
using System.Text;

namespace RPLidar.NET
{
    /// <summary>
    /// Single measurement
    /// </summary>
    public class Measurement
    {
        /// <summary>
        /// Is scan new ?
        /// </summary>
        public bool IsNewScan { get; internal set; }

        /// <summary>
        /// Angle in degrees
        /// </summary>
        public float Angle { get; internal set; }

        /// <summary>
        /// Distance in meters
        /// </summary>
        public float Distance { get; internal set; }

        /// <summary>
        /// Measurement
        /// </summary>
        /// <param name="isNewScan">Is new scan ?</param>
        /// <param name="angle">Angle in degrees</param>
        /// <param name="distance">Distance in meters</param>
        public Measurement(bool isNewScan, float angle, float distance)
        {
            IsNewScan = isNewScan;
            Angle = angle;
            Distance = distance;
        }
    }
}
