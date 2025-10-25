using System;

namespace Fellowship_overlay.Core;

public interface IDebugLog
{
    void Log(DateTimeOffset timestamp, string source, string message);
}