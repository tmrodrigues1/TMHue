using System.Runtime.InteropServices;

namespace TMHue.Windows.Native;

internal static unsafe partial class DisplayColorNative
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Source { public Luid Adapter; public uint Id, Mode, Status; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Target
    {
        public Luid Adapter;
        public uint Id, Mode, Technology, Rotation, Scaling, RefreshNumerator, RefreshDenominator;
        public uint Scanline, Available, Status;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Path { public Source Source; public Target Target; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Header { public uint Type, Size; public Luid Adapter; public uint Id; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct SourceName { public Header Header; public fixed char Name[32]; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct AdvancedColor { public Header Header; public uint Flags, Encoding, Bits; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct AdvancedColor2 { public Header Header; public uint Flags, Encoding, Bits, ActiveMode; }

    [LibraryImport("user32.dll")]
    internal static partial int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [LibraryImport("user32.dll")]
    internal static partial int QueryDisplayConfig(uint flags, ref uint paths, Path* path,
        ref uint modes, void* mode, nint topology);
    [LibraryImport("user32.dll")]
    internal static partial int DisplayConfigGetDeviceInfo(Header* header);
    [LibraryImport("gdi32.dll", EntryPoint = "CreateDCW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateDC(string driver, string device, nint output, nint initData);
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(nint dc);
    [LibraryImport("gdi32.dll", EntryPoint = "GetICMProfileW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetICMProfile(nint dc, ref uint size, char* filename);
    [LibraryImport("mscms.dll", EntryPoint = "GetStandardColorSpaceProfileW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetStandardColorSpaceProfile(nint machine, uint space, char* path, ref uint size);
}
