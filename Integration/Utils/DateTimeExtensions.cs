using System;

namespace Integration.Utils
{
    public static class DateTimeExtensions
    {
        public static DateTime? SafeDateTime(this DateTime? dt)
        {
            if (!dt.HasValue || dt.Value == DateTime.MinValue || dt.Value.Year < 1753) // SQL Server min date
            {
                return null;
            }
            return dt.Value;
        }

        public static DateTime? SafeDateTime(this DateTime dt) // Overload for non-nullable DateTime
        {
            if (dt == DateTime.MinValue || dt.Year < 1753) // SQL Server min date
            {
                return null;
            }
            return dt;
        }
    }
} 