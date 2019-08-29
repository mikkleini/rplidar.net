using System;
using System.Collections.Generic;
using System.Text;

namespace RPLidar
{
    /// <summary>
    /// Logging event arguments
    /// </summary>
    public class LogEventArgs : EventArgs
    {
        /// <summary>
        /// Log message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Message severity
        /// </summary>
        public Severity Severity { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="severity">Message severity</param>
        public LogEventArgs(string message, Severity severity)
        {
            Message = message;
            Severity = severity;
        }
    }
}
