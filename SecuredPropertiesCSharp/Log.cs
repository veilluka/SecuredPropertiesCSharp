using System;

namespace SecuredPropertiesCSharp
{
    public static class Log
    {
        public static bool Verbose { get; set; } = false;

        public static void Info(string message)
        {
            if (Verbose)
                Console.Error.WriteLine($"[LOG] {message}");
        }

        public static void Warn(string message)
        {
            if (Verbose)
                Console.Error.WriteLine($"[WARN] {message}");
        }

        public static void Error(string message)
        {
            // Always print errors
            Console.Error.WriteLine($"[ERROR] {message}");
        }

        public static void Error(string message, Exception ex)
        {
            if (Verbose)
                Console.Error.WriteLine($"[ERROR] {message}: {ex.GetType().Name}: {ex.Message}");
            else
                Console.Error.WriteLine($"[ERROR] {message}");
        }
    }
}
