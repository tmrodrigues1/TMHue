using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TMHue.Windows.Native;

internal static unsafe partial class IccNative
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Profile { public uint Type; public nint Data; public uint Size; }
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct Color
    {
        [FieldOffset(0)] public ushort Red;
        [FieldOffset(2)] public ushort Green;
        [FieldOffset(4)] public ushort Blue;
    }
    [LibraryImport("mscms.dll", EntryPoint = "OpenColorProfileW", SetLastError = true)]
    internal static partial ProfileHandle OpenColorProfile(ref Profile profile, uint access, uint share, uint creation);
    [LibraryImport("mscms.dll", SetLastError = true)]
    internal static partial TransformHandle CreateMultiProfileTransform(nint* profiles, uint count,
        uint* intents, uint intentCount, uint flags, uint preferredCmm);
    [LibraryImport("mscms.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TranslateColors(TransformHandle transform, Color* input, uint count,
        uint inputType, Color* output, uint outputType);
    [LibraryImport("mscms.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseColorProfile(nint profile);
    [LibraryImport("mscms.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteColorTransform(nint transform);
}

internal sealed class ProfileHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public ProfileHandle() : base(true) { }
    protected override bool ReleaseHandle() => IccNative.CloseColorProfile(handle);
}

internal sealed class TransformHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public TransformHandle() : base(true) { }
    protected override bool ReleaseHandle() => IccNative.DeleteColorTransform(handle);
}
