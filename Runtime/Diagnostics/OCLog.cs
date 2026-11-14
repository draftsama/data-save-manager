#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace OperatorConsole
{
    public enum OCLogType
    {
        Info,
        Warning,
        Error
    }

    /// <summary>One message in the Log. Identical messages logged back to back share one Log Entry and raise its <see cref="Count"/>.</summary>
    public sealed class OCLogEntry
    {
        private string? _timeText;
        private string? _preview;
        private int _previewLines;

        internal OCLogEntry(OCLogType type, string message, string stackTrace, DateTime time)
        {
            Type = type;
            Message = message;
            StackTrace = stackTrace;
            Time = time;
            Count = 1;
        }

        public OCLogType Type { get; }
        public string Message { get; }

        /// <summary>Empty when the build recorded no stack trace for this message.</summary>
        public string StackTrace { get; }

        /// <summary>Local time of the first occurrence.</summary>
        public DateTime Time { get; }

        public int Count { get; internal set; }

        public bool HasStackTrace => StackTrace.Length > 0;

        internal string TimeText => _timeText ??= Time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

        /// <summary>The message cut to its first <paramref name="maxLines"/> lines; cached for the last count asked.</summary>
        internal string Preview(int maxLines)
        {
            if (_preview != null && _previewLines == maxLines) return _preview;
            _previewLines = maxLines;
            var end = -1;
            for (var line = 0; line < maxLines; line++)
            {
                end = Message.IndexOf('\n', end + 1);
                if (end < 0) break;
            }
            _preview = (end < 0 ? Message : Message.Substring(0, end)).Replace("\r", string.Empty);
            return _preview;
        }

        internal bool IsSameAs(OCLogType type, string message, string stackTrace) =>
            Type == type
            && string.Equals(Message, message, StringComparison.Ordinal)
            && string.Equals(StackTrace, stackTrace, StringComparison.Ordinal);
    }

    /// <summary>
    /// The messages the app wrote to Unity's log, kept for the Runtime Panel's Log tab. Messages may arrive on
    /// any thread; <see cref="Changed"/> is raised on the main thread, at most once per frame.
    /// </summary>
    public sealed class OCLog
    {
        public const int DefaultCapacity = 1000;

        private readonly object _lock = new();
        private OCLogEntry[] _buffer;
        private int _start;
        private int _count;
        private int _infoCount;
        private int _warningCount;
        private int _errorCount;
        private long _errorsLogged;
        private int _version;
        private int _raisedVersion;

        public OCLog(int capacity = DefaultCapacity)
        {
            _buffer = new OCLogEntry[Math.Max(1, capacity)];
        }

        /// <summary>Raised on the main thread after the Log changed.</summary>
        public event Action? Changed;

        /// <summary>True while Unity's log messages are being kept.</summary>
        public bool IsCapturing { get; private set; }

        public int Capacity
        {
            get { lock (_lock) return _buffer.Length; }
        }

        /// <summary>Number of Log Entries kept; identical messages logged back to back count once.</summary>
        public int Count
        {
            get { lock (_lock) return _count; }
        }

        /// <summary>Info messages kept, counting repeats.</summary>
        public int InfoCount
        {
            get { lock (_lock) return _infoCount; }
        }

        public int WarningCount
        {
            get { lock (_lock) return _warningCount; }
        }

        /// <summary>Errors, exceptions and failed asserts kept, counting repeats.</summary>
        public int ErrorCount
        {
            get { lock (_lock) return _errorCount; }
        }

        /// <summary>Errors logged since the app started, counting repeats; never goes down, even on <see cref="Clear"/>.</summary>
        public long ErrorsLogged
        {
            get { lock (_lock) return _errorsLogged; }
        }

        /// <summary>Changes every time the Log changes.</summary>
        public int Version
        {
            get { lock (_lock) return _version; }
        }

        /// <summary>Replaces <paramref name="target"/>'s contents with the kept Log Entries, oldest first.</summary>
        public void CopyTo(List<OCLogEntry> target)
        {
            target.Clear();
            lock (_lock)
            {
                for (var i = 0; i < _count; i++) target.Add(_buffer[(_start + i) % _buffer.Length]);
            }
        }

        /// <summary>Errors logged after <paramref name="seenMark"/> (an earlier <see cref="ErrorsLogged"/>), but never more than the Log still holds.</summary>
        public int UnseenErrors(long seenMark)
        {
            lock (_lock)
            {
                var unseen = _errorsLogged - seenMark;
                if (unseen <= 0) return 0;
                return (int)Math.Min(unseen, _errorCount);
            }
        }

        /// <summary>Adds a message. Safe to call from any thread.</summary>
        public void Add(OCLogType type, string message, string? stackTrace, DateTime time)
        {
            message ??= string.Empty;
            stackTrace ??= string.Empty;
            lock (_lock)
            {
                if (type == OCLogType.Error) _errorsLogged++;
                AdjustCount(type, 1);
                _version++;

                if (_count > 0)
                {
                    var last = _buffer[(_start + _count - 1) % _buffer.Length];
                    if (last.IsSameAs(type, message, stackTrace))
                    {
                        last.Count++;
                        return;
                    }
                }

                var entry = new OCLogEntry(type, message, stackTrace, time);
                if (_count == _buffer.Length) Evict();
                _buffer[(_start + _count) % _buffer.Length] = entry;
                _count++;
            }
        }

        /// <summary>Removes every Log Entry, for every Runtime Panel.</summary>
        public void Clear()
        {
            lock (_lock)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _start = 0;
                _count = 0;
                _infoCount = _warningCount = _errorCount = 0;
                _version++;
            }
        }

        /// <summary>Changes how many Log Entries are kept, dropping the oldest when there are more.</summary>
        public void SetCapacity(int capacity)
        {
            capacity = Math.Max(1, capacity);
            lock (_lock)
            {
                if (capacity == _buffer.Length) return;
                while (_count > capacity) Evict();
                var resized = new OCLogEntry[capacity];
                for (var i = 0; i < _count; i++) resized[i] = _buffer[(_start + i) % _buffer.Length];
                _buffer = resized;
                _start = 0;
                _version++;
            }
        }

        internal void StartCapture()
        {
            if (IsCapturing) return;
            IsCapturing = true;
            Application.logMessageReceivedThreaded += OnLogMessage;
        }

        internal void StopCapture()
        {
            if (!IsCapturing) return;
            IsCapturing = false;
            Application.logMessageReceivedThreaded -= OnLogMessage;
        }

        /// <summary>Raises <see cref="Changed"/> when the Log changed since the last call. Main thread only.</summary>
        internal void RaiseChangedIfNeeded()
        {
            var version = Version;
            if (version == _raisedVersion) return;
            _raisedVersion = version;
            Changed?.Invoke();
        }

        internal static OCLogType FromUnity(LogType type) => type switch
        {
            LogType.Warning => OCLogType.Warning,
            LogType.Log     => OCLogType.Info,
            _                => OCLogType.Error
        };

        private void OnLogMessage(string condition, string stackTrace, LogType type) =>
            Add(FromUnity(type), condition, stackTrace, DateTime.Now);

        private void Evict()
        {
            var oldest = _buffer[_start];
            AdjustCount(oldest.Type, -oldest.Count);
            _buffer[_start] = null!;
            _start = (_start + 1) % _buffer.Length;
            _count--;
        }

        private void AdjustCount(OCLogType type, int amount)
        {
            switch (type)
            {
                case OCLogType.Info:    _infoCount += amount; break;
                case OCLogType.Warning: _warningCount += amount; break;
                default:                _errorCount += amount; break;
            }
        }
    }
}
