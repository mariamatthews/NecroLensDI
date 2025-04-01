using System;

namespace NecroLensDI.Utility
{
    internal static class Guard
    {
        public static void AgainstNull<T>(T argument, string argumentName) where T : class
        {
            if (argument == null)
            {
                throw new ArgumentNullException(argumentName);
            }
        }
    }
}
