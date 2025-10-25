using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Fellowship_overlay.Core
{
    public sealed class LogWatcher : IDisposable
    {
        private readonly string _logDir;
        private readonly FileSystemWatcher _fsw;
        private FileStream? _fs;
        private StreamReader? _sr;
        private long _lastLen;

        public event Action<string>? Line;

        public LogWatcher(string logDir)
        {
            _logDir = logDir;
            _fsw = new FileSystemWatcher(_logDir, "*.log")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };
            _fsw.Created += (_, e) => OpenNewest();
            _fsw.Changed += (_, e) => Pump();
            _fsw.EnableRaisingEvents = true;

            OpenNewest();
        }

        private void OpenNewest()
        {
            CloseCurrent();
            var newest = new DirectoryInfo(_logDir)
                .GetFiles("*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (newest == null) return;

            _fs = new FileStream(newest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _sr = new StreamReader(_fs, Encoding.UTF8);
            _lastLen = _fs.Length;
            _fs.Seek(_lastLen, SeekOrigin.Begin); // tail from end
        }

        private void Pump()
        {
            if (_fs == null || _sr == null) return;
            string? line;
            while ((line = _sr.ReadLine()) != null)
                Line?.Invoke(line);
        }

        public void Tick() => Pump();

        private void CloseCurrent()
        {
            try { _sr?.Dispose(); } catch { }
            try { _fs?.Dispose(); } catch { }
            _sr = null; _fs = null; _lastLen = 0;
        }

        public void Dispose()
        {
            _fsw.Dispose();
            CloseCurrent();
        }
    }
}
