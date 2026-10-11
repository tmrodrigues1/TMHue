using TMHue.Windows.Native;

namespace TMHue.Windows.Display;

public sealed record DisplayColorInfo(string DeviceName, bool? AdvancedColorActive, uint? BitsPerChannel,
    string? ProfilePath);

/// <summary>Read-only display metadata. Does not change HDR, profiles or calibration. Run
/// before a session on a worker thread; unavailable metadata remains explicitly unknown.</summary>
public static unsafe class DisplayColorEnvironment
{
    public static IReadOnlyList<DisplayColorInfo> Read(MonitorLayout layout)
    {
        var colorModes = ReadModes();
        return layout.Monitors.Select(m =>
        {
            colorModes.TryGetValue(m.DeviceName, out var mode);
            return new DisplayColorInfo(m.DeviceName, mode.Active, mode.Bits, ReadProfile(m.DeviceName));
        }).ToArray();
    }

    private static Dictionary<string, (bool? Active, uint? Bits)> ReadModes()
    {
        var result = new Dictionary<string, (bool? Active, uint? Bits)>(StringComparer.OrdinalIgnoreCase);
        const uint activePaths = 2;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (DisplayColorNative.GetDisplayConfigBufferSizes(activePaths, out var paths, out var modes) != 0
                || paths is 0 or > 128 || modes > 512) return result;
            var pathData = new DisplayColorNative.Path[paths];
            var modeData = new byte[checked(modes * 64)]; // DISPLAYCONFIG_MODE_INFO, SDK union layout
            fixed (DisplayColorNative.Path* path = pathData)
            fixed (byte* mode = modeData)
            {
                var status = DisplayColorNative.QueryDisplayConfig(activePaths, ref paths, path, ref modes, mode, 0);
                if (status == 122) continue; // topology changed, bounded retry
                if (status != 0) return result;
                for (var i = 0; i < paths; i++)
                {
                    var p = pathData[i];
                    var name = new DisplayColorNative.SourceName
                    {
                        Header = new() { Type = 1, Size = (uint)sizeof(DisplayColorNative.SourceName),
                            Adapter = p.Source.Adapter, Id = p.Source.Id }
                    };
                    if (DisplayColorNative.DisplayConfigGetDeviceInfo(&name.Header) != 0) continue;
                    var device = new string(name.Name).TrimEnd('\0');
                    var info = new DisplayColorNative.AdvancedColor2
                    {
                        Header = new() { Type = 15, Size = (uint)sizeof(DisplayColorNative.AdvancedColor2),
                            Adapter = p.Target.Adapter, Id = p.Target.Id }
                    };
                    bool? enabled = null; uint? bits = null;
                    if (DisplayColorNative.DisplayConfigGetDeviceInfo(&info.Header) == 0)
                    {
                        enabled = info.ActiveMode != 0; bits = info.Bits;
                    }
                    else
                    {
                        var legacy = new DisplayColorNative.AdvancedColor
                        {
                            Header = new() { Type = 9, Size = (uint)sizeof(DisplayColorNative.AdvancedColor),
                                Adapter = p.Target.Adapter, Id = p.Target.Id }
                        };
                        if (DisplayColorNative.DisplayConfigGetDeviceInfo(&legacy.Header) == 0)
                        { enabled = (legacy.Flags & 2) != 0; bits = legacy.Bits; }
                    }
                    // Cloned outputs may share a source: never classify an advanced output as SDR.
                    if (result.TryGetValue(device, out var prior))
                        enabled = prior.Active is true || enabled is true ? true
                            : prior.Active is null || enabled is null ? null : false;
                    result[device] = (enabled, bits);
                }
                return result;
            }
        }
        return result;
    }

    public static string? ReadProfile(string device)
    {
        var dc = DisplayColorNative.CreateDC("DISPLAY", device, 0, 0);
        if (dc == 0) return null;
        try
        {
            uint count = 512;
            var buffer = new char[count];
            fixed (char* path = buffer)
                if (DisplayColorNative.GetICMProfile(dc, ref count, path)) return new string(path);
            if (count is 0 or > 8192) return null;
            buffer = new char[count];
            fixed (char* path = buffer)
                return DisplayColorNative.GetICMProfile(dc, ref count, path) ? new string(path) : null;
        }
        finally { DisplayColorNative.DeleteDC(dc); }
    }

    public static string? GetSrgbProfilePath()
    {
        uint count = 8192;
        var buffer = new char[count];
        fixed (char* path = buffer)
            return DisplayColorNative.GetStandardColorSpaceProfile(0, 0x73524742, path, ref count)
                ? new string(path) : null;
    }
}
