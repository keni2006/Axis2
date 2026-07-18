using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using Axis2.WPF.Models; // Import the models

namespace Axis2.WPF.Services
{
    public static class Logger
    {
        private static readonly string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "axis2_wpf_debug.log");

        // Log writes used to open/append/flush/close the file on every call, under a global lock,
        // on the calling (usually UI) thread. With per-frame logging in the animation/art paths that
        // blocked the UI thread on disk I/O and starved the GC. Now callers only enqueue; a single
        // background thread batches the writes to disk.
        private static readonly ConcurrentQueue<string> _queue = new();
        private static readonly AutoResetEvent _signal = new(false);

        public static event Action<LogEntry> OnLogMessage;

        static Logger()
        {
            var writer = new Thread(WriterLoop) { IsBackground = true, Name = "Axis2.Logger" };
            writer.Start();
        }

        public static void Init()
        {
            try
            {
                File.WriteAllText(logFilePath, string.Empty); // Clear log on start
            }
            catch { /* Ignore */ }
        }

        public static void Log(string message)
        {
            Log(LogSource.Unknown, message);
        }


        public static void Log(LogSource source, string message)
        {
            try
            {
                var logEntry = new LogEntry(source, message);
                _queue.Enqueue(logEntry.FormattedMessage);
                _signal.Set();
                OnLogMessage?.Invoke(logEntry);
            }
            catch { /* Ignore */ }
        }

        private static void WriterLoop()
        {
            var sb = new StringBuilder();
            while (true)
            {
                _signal.WaitOne(1000);
                sb.Clear();
                int count = 0;
                while (count < 10000 && _queue.TryDequeue(out var line))
                {
                    sb.Append(line).Append('\n');
                    count++;
                }
                if (sb.Length == 0) continue;
                try
                {
                    File.AppendAllText(logFilePath, sb.ToString());
                }
                catch { /* Ignore */ }
            }
        }
    }
}
