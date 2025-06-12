using System;

namespace Infrastructure.Scaling.Utils
{
    public static class Helper
    {
        public static string GenerateRandomId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
} 