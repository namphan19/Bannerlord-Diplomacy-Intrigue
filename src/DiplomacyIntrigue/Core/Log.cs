using System;
using System.IO;
using System.Text;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Core
{
    public enum LogLevel { Trace, Debug, Info, Warn, Error }

    /// <summary>
    /// File + in-game logger. Deliberately dependency-free and exception-proof:
    /// logging must never be the thing that crashes a campaign.
    /// </summary>
    public static class Log
    {
        private static readonly object Gate = new object();
        private static string _logFile;
        private static bool _initialized;

        public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        public static string LogDirectory { get; private set; }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                LogDirectory = Path.Combine(docs, "Mount and Blade II Bannerlord", "DiplomacyIntrigue", "Logs");
                Directory.CreateDirectory(LogDirectory);

                _logFile = Path.Combine(LogDirectory, "diplomacy-intrigue-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                Write(LogLevel.Info, "Log", "Diplomacy & Intrigue " + SubModule.ModuleVersion + " log opened.");
                PruneOldLogs();
            }
            catch
            {
                _logFile = null;
            }
        }

        public static void Trace(string source, string message) => Write(LogLevel.Trace, source, message);
        public static void Debug(string source, string message) => Write(LogLevel.Debug, source, message);
        public static void Info(string source, string message) => Write(LogLevel.Info, source, message);
        public static void Warn(string source, string message) => Write(LogLevel.Warn, source, message);

        public static void Error(string source, string message, Exception ex = null)
        {
            var sb = new StringBuilder(message);
            if (ex != null)
            {
                sb.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
                sb.AppendLine().Append(ex.StackTrace);
            }
            Write(LogLevel.Error, source, sb.ToString());
        }

        public static void Write(LogLevel level, string source, string message)
        {
            if (level < MinimumLevel) return;
            var line = string.Concat(
                DateTime.Now.ToString("HH:mm:ss.fff"), " [", level.ToString().ToUpperInvariant(), "] ",
                "(", source, ") ", message);
            try
            {
                lock (Gate)
                {
                    if (_logFile != null)
                        File.AppendAllText(_logFile, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { /* disk full, locked file, whatever - never propagate */ }
        }

        /// <summary>Writes to the campaign message log. Safe to call before a campaign exists.</summary>
        public static void Notify(string message, Color? color = null)
        {
            Info("Notify", message);
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(message, color ?? Colors.White));
            }
            catch { /* no UI yet */ }
        }

        private static void PruneOldLogs()
        {
            try
            {
                var files = new DirectoryInfo(LogDirectory).GetFiles("diplomacy-intrigue-*.log");
                if (files.Length <= 10) return;
                Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (var i = 10; i < files.Length; i++) files[i].Delete();
            }
            catch { }
        }
    }
}
