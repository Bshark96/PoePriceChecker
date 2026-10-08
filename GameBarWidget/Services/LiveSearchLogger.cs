using System;
using System.Collections.Generic;
using System.Text;

namespace GameBarWidget.Services
{
    public static class LiveSearchLogger
    {
        private static readonly object _lock = new object();
        private static readonly List<string> _logEntries = new List<string>();
        private const int MaxLogEntries = 100;

        public static event Action<string> OnLogAdded;

        public static void Log(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string entry = $"[{timestamp}] {message}";

            lock (_lock)
            {
                _logEntries.Add(entry);
                if (_logEntries.Count > MaxLogEntries)
                {
                    _logEntries.RemoveAt(0);
                }
            }

            try
            {
                OnLogAdded?.Invoke(entry);
            }
            catch { }
        }

        public static string GetFullLog()
        {
            lock (_lock)
            {
                return string.Join(Environment.NewLine, _logEntries);
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _logEntries.Clear();
            }
            try
            {
                OnLogAdded?.Invoke(null);
            }
            catch { }
        }
    }
}
