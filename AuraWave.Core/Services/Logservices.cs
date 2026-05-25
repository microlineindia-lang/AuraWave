using AuraWave.Core.Enums;
using AuraWave.Core.Interfaces;
using AuraWave.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AuraWave.Core.Services
{
    public sealed class LogService : ILogService
    {
        private readonly ObservableCollection<LogEntry> _entries = new();
        private readonly object _lock = new();
        private const int MaxEntries = 5000;

        public IReadOnlyList<LogEntry> Entries => _entries;
        public event EventHandler<LogEntry>? EntryAdded;

        public void Log(LogSeverity severity, string source, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Severity = severity,
                Source = source,
                Message = message
            };

            lock (_lock)
            {
                if (_entries.Count >= MaxEntries)
                    _entries.RemoveAt(0);
                _entries.Add(entry);
            }

            // Mirror to Serilog
            switch (severity)
            {
                case LogSeverity.Debug: Serilog.Log.Debug("[{Source}] {Message}", source, message); break;
                case LogSeverity.Info: Serilog.Log.Information("[{Source}] {Message}", source, message); break;
                case LogSeverity.Warning: Serilog.Log.Warning("[{Source}] {Message}", source, message); break;
                case LogSeverity.Error: Serilog.Log.Error("[{Source}] {Message}", source, message); break;
                case LogSeverity.Critical: Serilog.Log.Fatal("[{Source}] {Message}", source, message); break;
                case LogSeverity.Scpi: Serilog.Log.Verbose("[SCPI][{Source}] {Message}", source, message); break;
            }

            EntryAdded?.Invoke(this, entry);
        }

        public void Debug(string source, string message) => Log(LogSeverity.Debug, source, message);
        public void Info(string source, string message) => Log(LogSeverity.Info, source, message);
        public void Warning(string source, string message) => Log(LogSeverity.Warning, source, message);
        public void Error(string source, string message) => Log(LogSeverity.Error, source, message);
        public void Scpi(string source, string message) => Log(LogSeverity.Scpi, source, message);

        public void Clear()
        {
            lock (_lock) { _entries.Clear(); }
        }
    }
}