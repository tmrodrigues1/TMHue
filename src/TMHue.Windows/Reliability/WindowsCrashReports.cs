using System.Diagnostics.Eventing.Reader;
using System.Globalization;

namespace TMHue.Windows.Reliability;

/// <summary>Reads what Windows itself recorded about crashes of an executable. A native crash
/// (access violation in interop code, stack overflow, fail-fast) ends the process before any
/// managed handler runs, but the system still writes the faulting module and exception code to
/// the Application event log. Read-only; needs no elevation.</summary>
public static class WindowsCrashReports
{
    private const int MaxMessageLength = 4000;

    /// <summary>Most recent first. Returns an empty list when nothing matches or the event log
    /// cannot be read.</summary>
    public static IReadOnlyList<string> ReadSince(DateTimeOffset since, string executableName, int maxEntries = 3)
    {
        var reports = new List<string>();
        var sinceUtc = since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var query = new EventLogQuery("Application", PathType.LogName,
            "*[System[Provider[@Name='.NET Runtime' or @Name='Application Error']"
            + $" and TimeCreated[@SystemTime>='{sinceUtc}']]]")
        {
            ReverseDirection = true
        };

        try
        {
            using var reader = new EventLogReader(query);
            while (reports.Count < maxEntries && reader.ReadEvent() is { } record)
            {
                using (record)
                {
                    var message = record.FormatDescription();
                    if (message is null || !message.Contains(executableName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (message.Length > MaxMessageLength) message = message[..MaxMessageLength];
                    reports.Add($"{record.TimeCreated:yyyy-MM-dd HH:mm:ss} · {record.ProviderName} · evento {record.Id}"
                        + Environment.NewLine + message.Trim());
                }
            }
        }
        catch (Exception error) when (error is EventLogException or UnauthorizedAccessException or InvalidOperationException)
        {
            // The event log is a best-effort source; whatever was read so far is still useful.
        }

        return reports;
    }
}
