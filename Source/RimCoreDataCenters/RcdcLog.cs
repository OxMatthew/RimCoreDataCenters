using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Logging helper. Normal play stays quiet: only genuine problems are written to the log.
    /// Verbose diagnostics are printed only in developer mode after being switched on from the
    /// debug menu (RimCore Data Centers > Toggle verbose logging).
    /// </summary>
    internal static class RcdcLog
    {
        private const string Prefix = "[RimCore Data Centers] ";

        public static bool Verbose;

        public static void Dev(string message)
        {
            if (Prefs.DevMode && Verbose)
            {
                Log.Message(Prefix + message);
            }
        }

        /// <summary>Always-on message. Used only by the opt-in self-test harness.</summary>
        public static void Test(string message)
        {
            Log.Message(Prefix + message);
        }

        public static void Warn(string message)
        {
            Log.Warning(Prefix + message);
        }

        public static void Error(string message)
        {
            Log.Error(Prefix + message);
        }
    }
}
