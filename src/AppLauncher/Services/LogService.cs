using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AppLauncher.Services
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Command
    }

    public sealed class LogEntry
    {
        public DateTime Timestamp { get; init; }
        public LogLevel Level { get; init; }
        public string Message { get; init; } = "";

        public override string ToString()
        {
            string timestamp = Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            return $"[{timestamp}] {Message}";
        }
    }

    public sealed class LogService
    {
        private readonly string _logFilePath;
        private readonly object _sync = new();
        private readonly LinkedList<LogEntry> _buffer = new();
        private const int MaxBufferEntries = 2000;

        public event Action<LogEntry>? LogAppended;

        public LogService(string logFilePath)
        {
            _logFilePath = logFilePath;
        }

        public void Info(string message) => Write(LogLevel.Info, message);
        public void Success(string message) => Write(LogLevel.Success, message);
        public void Warning(string message) => Write(LogLevel.Warning, message);
        public void Error(string message) => Write(LogLevel.Error, message);
        public void Command(string message) => Write(LogLevel.Command, message);

        public void Write(LogLevel level, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };

            lock (_sync)
            {
                _buffer.AddLast(entry);
                while (_buffer.Count > MaxBufferEntries)
                {
                    _buffer.RemoveFirst();
                }

                try
                {
                    File.AppendAllText(_logFilePath, entry + Environment.NewLine);
                }
                catch
                {
                    // Ignore disk write failures so UI logging still works.
                }
            }

            LogAppended?.Invoke(entry);
        }

        public IReadOnlyList<LogEntry> GetBufferSnapshot()
        {
            lock (_sync)
            {
                return new List<LogEntry>(_buffer);
            }
        }

        public string GetRecentText(int maxChars = 48000)
        {
            lock (_sync)
            {
                var sb = new StringBuilder();
                foreach (var entry in _buffer)
                {
                    sb.AppendLine(entry.ToString());
                }

                string text = sb.ToString();
                if (text.Length <= maxChars)
                {
                    return text;
                }

                return text[^maxChars..];
            }
        }

        public string ReadLogFileTail(int maxChars = 48000)
        {
            try
            {
                if (!File.Exists(_logFilePath))
                {
                    return GetRecentText(maxChars);
                }

                using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length <= maxChars)
                {
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }

                stream.Seek(-maxChars, SeekOrigin.End);
                using var tailReader = new StreamReader(stream);
                string content = tailReader.ReadToEnd();
                int firstNewline = content.IndexOf('\n');
                return firstNewline >= 0 ? content[(firstNewline + 1)..] : content;
            }
            catch
            {
                return GetRecentText(maxChars);
            }
        }

        public void ClearBuffer()
        {
            lock (_sync)
            {
                _buffer.Clear();
            }
        }
    }
}
