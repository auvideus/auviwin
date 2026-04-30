// One-off generator: renders the SVG path (icon.svg, same folder) on a yellow background
// at multiple sizes and packs them into AuviWin/icon.ico.
//
// Usage:  dotnet run --project tools/IconGen
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

int[] sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var outPath = Path.Combine(repoRoot, "AuviWin", "icon.ico");

var pngs = new List<byte[]>();
foreach (var size in sizes)
    pngs.Add(RenderPng(size));

WriteIco(outPath, sizes, pngs);
Console.WriteLine($"Wrote {outPath}");
return;

static byte[] RenderPng(int size)
{
    // SVG viewBox is 24x24 — scale uniformly to target size.
    double scale = size / 24.0;
    var geometry = Geometry.Parse(PathData);

    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        // Yellow background with rounded corners (radius scales with size)
        double radius = size * 0.18;
        var bgRect = new Rect(0, 0, size, size);
        dc.DrawRoundedRectangle(Brushes.Gold, null, bgRect, radius, radius);

        // Translate to center coordinate system, then scale
        dc.PushTransform(new ScaleTransform(scale, scale));

        // Stroke matches SVG: round caps/joins, width 1.5 (in viewBox units)
        var pen = new Pen(Brushes.Black, 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        dc.DrawGeometry(null, pen, geometry);
        dc.Pop();
    }

    var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(visual);

    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bmp));
    using var ms = new MemoryStream();
    encoder.Save(ms);
    return ms.ToArray();
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
