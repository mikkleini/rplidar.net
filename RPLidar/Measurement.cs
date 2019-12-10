using System;
using System.Collections.Generic;
using System.Text;

namespace RPLidar
{
    /// <summary>
    /// Single measurement
    /// </summary>
    public struct Measurement
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
        /// Reflected signal quality
        /// Only available on legacy scan mode
        /// </summary>
        public int? Quality { get; internal set; }

        /// <summary>
        /// Measurement
        /// </summary>
        /// <param name="isNewScan">Is new scan ?</param>
        /// <param name="angle">Angle in degrees</param>
        /// <param name="distance">Distance in meters</param>
        /// <param name="quality">Reflected signal quality</param>
        public Measurement(bool isNewScan, float angle, float distance, int? quality = null)
        {
            IsNewScan = isNewScan;
            Angle = angle;
            Distance = distance;
            Quality = quality;
        }

        /// <summary>
        /// Equals ?
        /// </summary>
        /// <param name="obj">Other object</param>
        /// <returns>true if equals to another object</returns>
        public override bool Equals(object obj)
        {
            if (obj is Measurement other)
            {
                return
                    IsNewScan.Equals(other.IsNewScan) &&
                    Angle.Equals(other.Angle) &&
                    Distance.Equals(other.Distance) &&
                    Quality.Equals(other.Quality);
            }

            return false;
        }

        /// <summary>
        /// Get has code
        /// </summary>
        /// <returns>integer hask</returns>
        public override int GetHashCode()
        {
            return (IsNewScan ? 31223123 : 0) ^
                (int)(Angle * 123123) ^
                (int)(Distance * -645654) ^
                (Quality.HasValue ? Quality.Value - 42342355 : 233);
        }

        /// <summary>
        /// Comparison operation
        /// </summary>
        /// <param name="left">Left operand</param>
        /// <param name="right">Right operand</param>
        /// <returns>true if operands are equal</returns>
        public static bool operator ==(Measurement left, Measurement right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Non-comparison operation
        /// </summary>
        /// <param name="left">Left operand</param>
        /// <param name="right">Right operand</param>
        /// <returns>true if operands are not equal</returns>
        public static bool operator !=(Measurement left, Measurement right)
        {
            return !left.Equals(right);
        }
    }
}
