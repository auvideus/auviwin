// Renders the SVG path (icon.svg, same folder) on a yellow background.
//
// Usage:
//   dotnet run --project tools/IconGen
//       → writes App/icon.ico (multi-resolution)
//   dotnet run --project tools/IconGen -- --assets <dir>
//       → writes MSIX logo PNGs to <dir>
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

const string PathData =
    "M 19.114 5.636 a 9 9 0 0 1 0 12.728 " +
    "M 16.463 8.288 a 5.25 5.25 0 0 1 0 7.424 " +
    "M 6.75 8.25 l 4.72 -4.72 a 0.75 0.75 0 0 1 1.28 0.53 v 15.88 " +
    "a 0.75 0.75 0 0 1 -1.28 0.53 l -4.72 -4.72 H 4.51 " +
    "c -0.88 0 -1.704 -0.507 -1.938 -1.354 A 9.009 9.009 0 0 1 2.25 12 " +
    "c 0 -0.83 0.112 -1.633 0.322 -2.396 C 2.806 8.756 3.63 8.25 4.51 8.25 H 6.75 Z";

// ── Mode dispatch ─────────────────────────────────────────────────────────────

if (args.Length >= 2 && args[0] == "--assets")
{
    var dir = args[1];
    Directory.CreateDirectory(dir);

    // Required MSIX logo sizes
    SavePng(RenderSquare(44),  Path.Combine(dir, "Square44x44Logo.png"));
    SavePng(RenderSquare(50),  Path.Combine(dir, "StoreLogo.png"));
    SavePng(RenderSquare(150), Path.Combine(dir, "Square150x150Logo.png"));
    SavePng(RenderWide(310, 150), Path.Combine(dir, "Wide310x150Logo.png"));

    Console.WriteLine($"Wrote MSIX assets to {dir}");
    return;
}

// Default: generate ICO
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var outPath = Path.Combine(repoRoot, "App", "icon.ico");

var pngs = new List<byte[]>();
foreach (var size in sizes)
    pngs.Add(RenderSquare(size));

WriteIco(outPath, sizes, pngs);
Console.WriteLine($"Wrote {outPath}");
return;

// ── Rendering helpers ─────────────────────────────────────────────────────────

static byte[] RenderSquare(int size)
{
    // SVG viewBox is 24x24 — scale uniformly to target size.
    double scale = size / 24.0;
    var geometry = Geometry.Parse(PathData);

    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        double radius = size * 0.18;
        dc.DrawRoundedRectangle(Brushes.Gold, null, new Rect(0, 0, size, size), radius, radius);
        dc.PushTransform(new ScaleTransform(scale, scale));
        var pen = new Pen(Brushes.Black, 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap   = PenLineCap.Round,
            LineJoin      = PenLineJoin.Round
        };
        dc.DrawGeometry(null, pen, geometry);
        dc.Pop();
    }

    return BitmapToBytes(visual, size, size);
}

/// <summary>Icon centred on a wide background (for Wide310x150Logo).</summary>
static byte[] RenderWide(int width, int height)
{
    int iconSize = (int)(height * 0.75); // icon occupies 75 % of the height
    double scale = iconSize / 24.0;
    var geometry = Geometry.Parse(PathData);

    double offsetX = (width - iconSize) / 2.0;
    double offsetY = (height - iconSize) / 2.0;

    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        double radius = height * 0.12;
        dc.DrawRoundedRectangle(Brushes.Gold, null, new Rect(0, 0, width, height), radius, radius);
        dc.PushTransform(new TranslateTransform(offsetX, offsetY));
        dc.PushTransform(new ScaleTransform(scale, scale));
        var pen = new Pen(Brushes.Black, 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap   = PenLineCap.Round,
            LineJoin      = PenLineJoin.Round
        };
        dc.DrawGeometry(null, pen, geometry);
        dc.Pop();
        dc.Pop();
    }

    return BitmapToBytes(visual, width, height);
}

static byte[] BitmapToBytes(DrawingVisual visual, int width, int height)
{
    var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(visual);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bmp));
    using var ms = new MemoryStream();
    encoder.Save(ms);
    return ms.ToArray();
}

static void SavePng(byte[] png, string path)
{
    File.WriteAllBytes(path, png);
    Console.WriteLine($"  {path}");
}

static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
{
    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);

    // ICONDIR: reserved=0, type=1 (icon), count
    bw.Write((ushort)0);
    bw.Write((ushort)1);
    bw.Write((ushort)sizes.Length);

    int dataOffset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        int s = sizes[i];
        // ICONDIRENTRY (16 bytes)
        bw.Write((byte)(s == 256 ? 0 : s));   // width  (0 = 256)
        bw.Write((byte)(s == 256 ? 0 : s));   // height (0 = 256)
        bw.Write((byte)0);                    // colour count (0 = >=256)
        bw.Write((byte)0);                    // reserved
        bw.Write((ushort)1);                  // colour planes
        bw.Write((ushort)32);                 // bits per pixel
        bw.Write((uint)pngs[i].Length);       // data size
        bw.Write((uint)dataOffset);           // data offset
        dataOffset += pngs[i].Length;
    }
    foreach (var data in pngs) bw.Write(data);
}
