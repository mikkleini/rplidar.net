using System;
using System.Collections.Generic;
using System.Text;

namespace RPLidar
{
    /// <summary>
    /// General configuration
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// Typical scan mode
        /// </summary>
        public ushort Typical { get; internal set; }

        /// <summary>
        /// Scan modes
        /// Dictionary key is the mode ID
        /// </summary>
        public Dictionary<ushort, ScanModeConfiguration> Modes { get; internal set; }
    }
}
