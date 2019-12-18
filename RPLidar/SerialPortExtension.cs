using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace RPLidar
{
    /// <summary>
    /// Serial port extension class
    /// Unfortunately the timeout and cancellation does not work reliably with SerialPort.BaseStream asynchronous functions on Windows
    /// so this extension is a workaround to have asynchronous wrapper on top of blocking functions.
    /// 
    /// See more:
    /// https://github.com/dotnet/corefx/issues/36040
    /// </summary>
    public static class SerialPortExtension
    {
        /// <summary>
        /// Asynchronous read with working timeout functionality.
        /// </summary>
        /// <param name="port">Port</param>
        /// <param name="buffer">Buffer</param>
        /// <param name="offset">Offset in buffer</param>
        /// <param name="count">Count of bytes to read</param>
        /// <returns>Number of actually read bytes</returns>
        public static Task<int> ReadAsync(this SerialPort port, byte[] buffer, int offset, int count)
        {
            return Task.Run(() =>
            {
                return port.Read(buffer, offset, count);
            });
        }

        /// <summary>
        /// Asynchronous write with working timeout functionality.
        /// </summary>
        /// <param name="port">Port</param>
        /// <param name="buffer">Buffer</param>
        /// <param name="offset">Offset in buffer</param>
        /// <param name="count">Count of bytes to write</param>
        /// <returns>None</returns>
        public static Task WriteAsync(this SerialPort port, byte[] buffer, int offset, int count)
        {
            return Task.Run(() =>
            {
                port.Write(buffer, offset, count);
            });
        }
    }
}
