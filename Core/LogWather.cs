using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Fellowship_overlay.Core
{
    public sealed class LogWatcher : IDisposable
    {
        private static readonly string[] WatchedExtensions = new[] { ".txt", ".log" };

        private readonly string _logDir;
        private readonly FileSystemWatcher _fsw;
        private readonly object _sync = new();
        private FileStream? _fs;
        private string? _currentPath;
        private long _lastLen;
        private Decoder _decoder = Encoding.UTF8.GetDecoder();
        private readonly StringBuilder _pending = new();
        private readonly byte[] _buffer = new byte[64 * 1024];
        private readonly char[] _charBuffer = new char[64 * 1024];

        public event Action<string>? Line;

        public LogWatcher(string logDir)
        {
            _logDir = logDir;
            _fsw = new FileSystemWatcher(_logDir)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _fsw.InternalBufferSize = 128 * 1024;
            _fsw.Created += OnFileChanged;
            _fsw.Changed += OnFileChanged;
            _fsw.Renamed += OnRenamed;
            _fsw.Deleted += OnDeleted;
            _fsw.Error += OnWatcherError;
            _fsw.EnableRaisingEvents = true;

            OpenNewest();
        }

        private void OnFileChanged(object? sender, FileSystemEventArgs e)
        {
            if (!IsLogFile(e.FullPath))
            {
                return;
            }

            List<string>? lines;
            lock (_sync)
            {
                EnsureCurrentFile(e.FullPath);
                lines = PumpLocked();
            }

            EmitLines(lines);
        }

        private void OnRenamed(object? sender, RenamedEventArgs e)
        {
            if (!IsLogFile(e.FullPath) && !IsLogFile(e.OldFullPath))
            {
                return;
            }

            List<string>? lines;
            lock (_sync)
            {
                if (_currentPath != null && string.Equals(_currentPath, e.OldFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    OpenFileLocked(e.FullPath, tailExisting: true);
                }
                else
                {
                    EnsureCurrentFile(e.FullPath);
                }

                lines = PumpLocked();
            }

            EmitLines(lines);
        }

        private void OnDeleted(object? sender, FileSystemEventArgs e)
        {
            if (_currentPath == null || !string.Equals(_currentPath, e.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lock (_sync)
            {
                CloseCurrentLocked();
                OpenNewestLocked();
            }
        }

        private void OnWatcherError(object? sender, ErrorEventArgs e)
        {
            lock (_sync)
            {
                OpenNewestLocked();
            }
        }

        public void Tick()
        {
            List<string>? lines;
            lock (_sync)
            {
                lines = PumpLocked();
            }

            EmitLines(lines);
        }

        private void EnsureCurrentFile(string candidatePath)
        {
            if (_currentPath == null)
            {
                OpenNewestLocked();
                return;
            }

            if (!string.Equals(_currentPath, candidatePath, StringComparison.OrdinalIgnoreCase))
            {
                var newest = GetNewestFile();
                if (newest != null && !string.Equals(_currentPath, newest.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    OpenFileLocked(newest.FullName, tailExisting: true);
                }
            }
        }

        private void OpenNewest()
        {
            lock (_sync)
            {
                OpenNewestLocked();
            }
        }

        private void OpenNewestLocked()
        {
            var newest = GetNewestFile();
            if (newest == null)
            {
                CloseCurrentLocked();
                return;
            }

            if (_currentPath != null && string.Equals(_currentPath, newest.FullName, StringComparison.OrdinalIgnoreCase))
            {
                if (_fs == null)
                {
                    OpenFileLocked(_currentPath, tailExisting: true);
                }
                return;
            }

            OpenFileLocked(newest.FullName, tailExisting: true);
        }

        private void OpenFileLocked(string path, bool tailExisting, long? resumePosition = null)
        {
            CloseCurrentLocked();
            try
            {
                _fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                _currentPath = path;
                _decoder = Encoding.UTF8.GetDecoder();
                _pending.Clear();
                if (resumePosition.HasValue)
                {
                    _lastLen = Math.Max(0, resumePosition.Value);
                    if (_lastLen > _fs.Length)
                    {
                        _lastLen = _fs.Length;
                    }
                }
                else if (tailExisting)
                {
                    _lastLen = _fs.Length;
                }
                else
                {
                    _lastLen = 0;
                }

                _fs.Seek(_lastLen, SeekOrigin.Begin);
            }
            catch
            {
                CloseCurrentLocked();
            }
        }

        private void CloseCurrentLocked()
        {
            try { _fs?.Dispose(); } catch { }
            _fs = null;
            _currentPath = null;
            _lastLen = 0;
            _pending.Clear();
            _decoder = Encoding.UTF8.GetDecoder();
        }

        private List<string>? PumpLocked()
        {
            if (_fs == null)
            {
                return null;
            }

            var emitted = new List<string>();

            try
            {
                var len = _fs.Length;
                if (len < _lastLen)
                {
                    _fs.Seek(0, SeekOrigin.Begin);
                    _lastLen = 0;
                    _decoder = Encoding.UTF8.GetDecoder();
                    _pending.Clear();
                }

                while (_lastLen < len)
                {
                    var toRead = (int)Math.Min(_buffer.Length, len - _lastLen);
                    _fs.Seek(_lastLen, SeekOrigin.Begin);
                    var read = _fs.Read(_buffer, 0, toRead);
                    if (read <= 0)
                    {
                        break;
                    }

                    _lastLen += read;
                    var chars = _decoder.GetChars(_buffer, 0, read, _charBuffer, 0, false);
                    AppendChars(emitted, chars);
                }
            }
            catch (IOException)
            {
                if (_currentPath != null)
                {
                    var resume = _lastLen;
                    OpenFileLocked(_currentPath, tailExisting: false, resumePosition: resume);
                }
            }
            catch (ObjectDisposedException)
            {
                return emitted.Count > 0 ? emitted : null;
            }

            return emitted.Count > 0 ? emitted : null;
        }

        private void AppendChars(List<string> emitted, int charCount)
        {
            for (var i = 0; i < charCount; i++)
            {
                var ch = _charBuffer[i];
                switch (ch)
                {
                    case '\r':
                        continue;
                    case '\n':
                        emitted.Add(_pending.ToString());
                        _pending.Clear();
                        break;
                    default:
                        _pending.Append(ch);
                        break;
                }
            }
        }

        private void EmitLines(List<string>? lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            foreach (var line in lines)
            {
                if (line.Length == 0)
                {
                    continue;
                }
                Line?.Invoke(line);
            }
        }

        private FileInfo? GetNewestFile()
        {
            try
            {
                var dir = new DirectoryInfo(_logDir);
                if (!dir.Exists)
                {
                    return null;
                }

                return dir
                    .EnumerateFiles()
                    .Where(f => WatchedExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsLogFile(string path)
            => !string.IsNullOrEmpty(path) && WatchedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        public void Dispose()
        {
            _fsw.Created -= OnFileChanged;
            _fsw.Changed -= OnFileChanged;
            _fsw.Renamed -= OnRenamed;
            _fsw.Deleted -= OnDeleted;
            _fsw.Error -= OnWatcherError;
            _fsw.Dispose();

            lock (_sync)
            {
                CloseCurrentLocked();
            }
        }
    }
}