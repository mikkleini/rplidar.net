using System;
using System.Collections.Generic;
using System.Text;

namespace RPLidar
{
    /// <summary>
    /// Descriptor
    /// </summary>
    internal class Descriptor
    {
        public int Length { get; internal set; }
        public bool IsSingle { get; internal set; }
        public byte DataType { get; internal set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="length"></param>
        /// <param name="isSingle"></param>
        /// <param name="dataType"></param>
        public Descriptor(int length, bool isSingle, byte dataType)
        {
            Length = length;
            IsSingle = isSingle;
            DataType = dataType;
        }
    }
}
