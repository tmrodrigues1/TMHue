using System.IO;

namespace TMHue.Windows.Cursor;

/// <summary>A native monochrome precision cursor with a known physical hotspot and hollow
/// center. Native cursor composition does not enter the GDI sample.</summary>
internal static class PrecisionCursor
{
    public static System.Windows.Input.Cursor Create()
    {
        const int side = 32, header = 22, dibHeader = 40, pixelBytes = side * side * 4, maskBytes = side * 4;
        var bytes = new byte[header + dibHeader + pixelBytes + maskBytes];
        using var stream = new MemoryStream(bytes, writable: true);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0); writer.Write((ushort)2); writer.Write((ushort)1);
        writer.Write((byte)side); writer.Write((byte)side); writer.Write((ushort)0);
        writer.Write((ushort)16); writer.Write((ushort)16); // selected pixel
        writer.Write(dibHeader + pixelBytes + maskBytes); writer.Write(header);
        writer.Write(dibHeader); writer.Write(side); writer.Write(side * 2);
        writer.Write((ushort)1); writer.Write((ushort)32);
        writer.Write(0); writer.Write(pixelBytes); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        Array.Fill(bytes, (byte)255, header + dibHeader + pixelBytes, maskBytes);
        for (var y = 0; y < side; y++)
        for (var x = 0; x < side; x++)
        {
            var dx = Math.Abs(x - 16); var dy = Math.Abs(y - 16);
            var arm = (dx <= 1 && dy is >= 4 and <= 13) || (dy <= 1 && dx is >= 4 and <= 13);
            if (!arm) continue;
            var i = header + dibHeader + ((side - 1 - y) * side + x) * 4;
            var value = (byte)(dx == 0 || dy == 0 ? 255 : 0);
            bytes[i] = bytes[i + 1] = bytes[i + 2] = value; bytes[i + 3] = 255;
            bytes[header + dibHeader + pixelBytes + (side - 1 - y) * 4 + x / 8] &= (byte)~(0x80 >> (x % 8));
        }
        stream.Position = 0;
        return new System.Windows.Input.Cursor(stream);
    }
}
