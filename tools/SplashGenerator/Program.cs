using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SplashGenerator;

/// <summary>
/// Gera packaging/velopack/splash.gif — a splash exibida pelo Setup.exe do Velopack durante a
/// instalação, reproduzindo o layout de _about/loading_tmhue.png: ícone, wordmark, barra de
/// progresso roxa com percentual e o pincel (packaging/velopack/cursor.png) avançando junto
/// com a barra. O Setup.exe não expõe o percentual real, então a animação simula uma
/// progressão fluida de 0→100% em loop.
///
/// O GifBitmapEncoder do WPF não grava loop nem delay por frame, então após codificar o
/// arquivo é reescrito em nível de blocos GIF: injeta a extensão NETSCAPE2.0 (loop infinito)
/// e uma Graphic Control Extension com o delay em cada frame.
/// </summary>
public static class Program
{
    private const int Width = 640;
    private const int Height = 380;
    private const int Frames = 50;             // 50 frames × 120 ms = ciclo de 6 s (0→100%)
    private const byte DelayCentiseconds = 12; // 12 cs = 120 ms

    // Faixa vertical que contém tudo o que anima (pincel, barra, brilho e percentual).
    private const int StripY = 126;
    private const int StripHeight = 136;

    private static readonly Color Background = Color.FromRgb(0x0C, 0x0B, 0x12);
    private static readonly Color PurpleLight = Color.FromRgb(0xA7, 0x8B, 0xFA);
    private static readonly Color Purple = Color.FromRgb(0x8B, 0x5C, 0xF6);
    private static readonly Color PurpleBlue = Color.FromRgb(0x5B, 0x8D, 0xF6);
    private static readonly Color TextDim = Color.FromRgb(0x9A, 0x97, 0xA6);
    private static readonly Color TextFaint = Color.FromRgb(0x6E, 0x6B, 0x7A);

    private static BitmapImage _brush = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        var packagingDir = args.Length > 0
            ? Path.GetDirectoryName(Path.GetFullPath(args[0]))!
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "packaging", "velopack"));
        var output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(packagingDir, "splash.gif");
        Directory.CreateDirectory(packagingDir);

        _brush = new BitmapImage();
        _brush.BeginInit();
        _brush.UriSource = new Uri(Path.Combine(packagingDir, "cursor.png"));
        _brush.CacheOption = BitmapCacheOption.OnLoad;
        _brush.EndInit();
        _brush.Freeze();

        // Frame 0 completo; os demais só a faixa animada (barra + pincel + percentual),
        // codificada como sub-retângulo GIF para reduzir drasticamente o tamanho do arquivo.
        var encoder = new GifBitmapEncoder();
        for (var i = 0; i < Frames; i++)
        {
            var frame = RenderFrame(Progress((double)i / (Frames - 1)));
            encoder.Frames.Add(i == 0
                ? BitmapFrame.Create(frame)
                : BitmapFrame.Create(new CroppedBitmap(frame, new Int32Rect(0, StripY, Width, StripHeight))));
        }

        using var memory = new MemoryStream();
        encoder.Save(memory);

        var animated = MakeAnimated(memory.ToArray());
        File.WriteAllBytes(output, animated);
        Console.WriteLine($"splash.gif gerado: {output} ({animated.Length / 1024} KB, {Frames} frames, {Width}x{Height})");
    }

    /// <summary>Curva de progresso: rápida no início, desacelera perto do fim e "respira" um
    /// instante em 100% antes de reiniciar, para a progressão parecer fluida e natural.</summary>
    private static double Progress(double t)
    {
        var eased = 1 - Math.Pow(1 - Math.Min(t / 0.92, 1), 1.8);
        return Math.Min(eased, 1);
    }

    private static RenderTargetBitmap RenderFrame(double p)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            DrawBackground(dc);
            DrawHeader(dc);
            DrawProgress(dc, p);
            DrawFeatureRow(dc);
            DrawFooter(dc);
        }

        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static void DrawBackground(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Background), null, new Rect(0, 0, Width, Height));

        // Brilho roxo difuso vindo do canto superior esquerdo, como no mockup.
        var glow = new RadialGradientBrush(
            Color.FromArgb(0x30, Purple.R, Purple.G, Purple.B),
            Color.FromArgb(0x00, Purple.R, Purple.G, Purple.B))
        {
            Center = new Point(0.18, 0.28),
            GradientOrigin = new Point(0.18, 0.28),
            RadiusX = 0.55,
            RadiusY = 0.75
        };
        dc.DrawRectangle(glow, null, new Rect(0, 0, Width, Height));
    }

    private static void DrawHeader(DrawingContext dc)
    {
        // Ícone do app: quadrado arredondado roxo com o pincel dentro.
        const double iconSize = 62;
        var iconRect = new Rect((Width - iconSize) / 2, 26, iconSize, iconSize);
        var iconBrush = new LinearGradientBrush(PurpleLight, Purple, 90);
        dc.DrawRoundedRectangle(iconBrush, null, iconRect, 15, 15);
        dc.DrawImage(_brush, new Rect(iconRect.X + 6, iconRect.Y + 4, iconSize - 10, iconSize - 10));

        // Wordmark "TMHue": TM branco + Hue em gradiente roxo→azul.
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var tm = Text("TM", typeface, 40, Brushes.White);
        var hue = Text("Hue", typeface, 40, new LinearGradientBrush(PurpleLight, PurpleBlue, 0));
        var totalWidth = tm.Width + hue.Width;
        dc.DrawText(tm, new Point((Width - totalWidth) / 2, 92));
        dc.DrawText(hue, new Point((Width - totalWidth) / 2 + tm.Width, 92));

        var subtitle = Text("Seu pick de cores", new Typeface("Segoe UI"), 15, new SolidColorBrush(TextDim));
        dc.DrawText(subtitle, new Point((Width - subtitle.Width) / 2, 146));
    }

    private static void DrawProgress(DrawingContext dc, double p)
    {
        var status = Text("Instalando TMHue...", new Typeface("Segoe UI"), 12, new SolidColorBrush(TextDim));
        dc.DrawText(status, new Point((Width - status.Width) / 2, 182));

        const double barX = 96;
        const double barWidth = 418;
        const double barY = 212;
        const double barHeight = 12;

        // Trilha
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x24, 0x22, 0x2E)), null,
            new Rect(barX, barY, barWidth, barHeight), barHeight / 2, barHeight / 2);

        // Preenchimento roxo→azul→roxo (paleta da logo)
        var fillWidth = Math.Max(barHeight, barWidth * p);
        var fill = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(PurpleLight, 0),
                new GradientStop(Purple, 0.55),
                new GradientStop(PurpleBlue, 1)
            },
            new Point(0, 0), new Point(1, 0));
        dc.DrawRoundedRectangle(fill, null, new Rect(barX, barY, fillWidth, barHeight), barHeight / 2, barHeight / 2);

        var tipX = barX + fillWidth;
        var tipY = barY + barHeight / 2;

        // Brilho na ponta do preenchimento
        var glow = new RadialGradientBrush(
            Color.FromArgb(0x66, PurpleBlue.R, PurpleBlue.G, PurpleBlue.B),
            Color.FromArgb(0x00, PurpleBlue.R, PurpleBlue.G, PurpleBlue.B));
        dc.DrawEllipse(glow, null, new Point(tipX - 4, tipY), 26, 18);

        // Pincel: a ponta das cerdas (≈ 23,5% / 73,5% do bitmap) encosta na ponta da barra.
        const double brushSize = 118;
        var brushX = tipX - brushSize * 0.235;
        var brushY = tipY - brushSize * 0.735;
        dc.DrawImage(_brush, new Rect(brushX, brushY, brushSize, brushSize));

        // Percentual à direita da barra
        var percent = Text($"{(int)Math.Round(p * 100)}%",
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            14, Brushes.White);
        dc.DrawText(percent, new Point(barX + barWidth + 14, barY + barHeight / 2 - percent.Height / 2));

        var hint = Text("Quase lá! Prepare-se para capturar qualquer cor.",
            new Typeface("Segoe UI"), 12, new SolidColorBrush(TextDim));
        dc.DrawText(hint, new Point((Width - hint.Width) / 2, 240));
    }

    private static void DrawFeatureRow(DrawingContext dc)
    {
        (string Title, string Caption)[] features =
        {
            ("Capture qualquer cor", "Do pixel ao HEX."),
            ("Copie com um clique", "HEX, RGB, HSL e mais."),
            ("Histórico inteligente", "Suas cores à mão."),
            ("Paletas personalizadas", "Organize suas cores.")
        };

        const double rowY = 282;
        const double rowHeight = 58;
        const double margin = 24;
        var panel = new Rect(margin, rowY, Width - margin * 2, rowHeight);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x2E, 0x2A, 0x28, 0x38)),
            new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0x3A, 0x37, 0x4A)), 1), panel, 12, 12);

        var cellWidth = panel.Width / features.Length;
        var titleFace = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        for (var i = 0; i < features.Length; i++)
        {
            var cellX = panel.X + i * cellWidth;

            // Marcador roxo simples no lugar dos ícones do mockup.
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Purple), 2),
                new Point(cellX + 22, rowY + rowHeight / 2), 6, 6);

            var title = Text(features[i].Title, titleFace, 10.5, Brushes.White);
            var caption = Text(features[i].Caption, new Typeface("Segoe UI"), 9.5, new SolidColorBrush(TextFaint));
            dc.DrawText(title, new Point(cellX + 38, rowY + 14));
            dc.DrawText(caption, new Point(cellX + 38, rowY + 30));
        }
    }

    private static void DrawFooter(DrawingContext dc)
    {
        var footer = Text("Instalação segura e rápida. Apenas para você.",
            new Typeface("Segoe UI"), 11, new SolidColorBrush(TextFaint));
        dc.DrawText(footer, new Point((Width - footer.Width) / 2, 352));
    }

    private static FormattedText Text(string text, Typeface typeface, double size, Brush brush) =>
        new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, size, brush, 1.0);

    /// <summary>Reescreve o GIF em nível de blocos, inserindo a NETSCAPE2.0 (loop infinito)
    /// após o cabeçalho e uma Graphic Control Extension com delay antes de cada frame.</summary>
    private static byte[] MakeAnimated(byte[] gif)
    {
        using var output = new MemoryStream();
        var pos = 0;
        var frameIndex = 0;

        // Header (6) + Logical Screen Descriptor (7)
        output.Write(gif, 0, 13);
        pos = 13;

        // Global Color Table, se presente
        var packed = gif[10];
        if ((packed & 0x80) != 0)
        {
            var gctSize = 3 * (1 << ((packed & 0x07) + 1));
            output.Write(gif, pos, gctSize);
            pos += gctSize;
        }

        // Loop infinito (NETSCAPE2.0)
        output.Write(new byte[]
        {
            0x21, 0xFF, 0x0B,
            (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E',
            (byte)'2', (byte)'.', (byte)'0',
            0x03, 0x01, 0x00, 0x00, // loop count 0 = infinito
            0x00
        });

        while (pos < gif.Length)
        {
            var marker = gif[pos];
            switch (marker)
            {
                case 0x21: // extensão existente: copia (pulando GCEs para não duplicar)
                {
                    var label = gif[pos + 1];
                    var start = pos;
                    pos += 2;
                    while (gif[pos] != 0)
                        pos += gif[pos] + 1;
                    pos++; // block terminator
                    if (label != 0xF9)
                        output.Write(gif, start, pos - start);
                    break;
                }
                case 0x2C: // Image Descriptor → precede com GCE de delay
                {
                    output.Write(new byte[] { 0x21, 0xF9, 0x04, 0x00, DelayCentiseconds, 0x00, 0x00, 0x00 });

                    // Frames recortados (todos após o primeiro): reposiciona na faixa animada.
                    if (frameIndex++ > 0)
                    {
                        gif[pos + 3] = StripY & 0xFF;
                        gif[pos + 4] = StripY >> 8;
                    }

                    var start = pos;
                    pos += 10; // separator + descriptor
                    var localPacked = gif[start + 9];
                    if ((localPacked & 0x80) != 0)
                        pos += 3 * (1 << ((localPacked & 0x07) + 1));
                    pos++; // LZW minimum code size
                    while (gif[pos] != 0)
                        pos += gif[pos] + 1;
                    pos++; // block terminator
                    output.Write(gif, start, pos - start);
                    break;
                }
                case 0x3B: // trailer
                    output.WriteByte(0x3B);
                    pos = gif.Length;
                    break;
                default:
                    throw new InvalidDataException($"Bloco GIF inesperado 0x{marker:X2} em {pos}.");
            }
        }

        return output.ToArray();
    }
}
