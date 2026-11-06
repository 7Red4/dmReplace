// Draws dmReplace.ico: two opposing arrows (one link swapped for another) on a rounded tile.
// Usage: MakeIcon <output.ico> [preview.png]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class MakeIcon
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: MakeIcon <output.ico> [preview.png]");
            return 1;
        }

        var images = new List<byte[]>();
        foreach (int size in Sizes)
        {
            using (Bitmap bitmap = Draw(size))
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                images.Add(stream.ToArray());
                if (size == 256 && args.Length > 1)
                {
                    bitmap.Save(args[1], ImageFormat.Png);
                }
            }
        }

        using (BinaryWriter w = new BinaryWriter(File.Create(args[0])))
        {
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)Sizes.Length);
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                w.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                w.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((short)1);
                w.Write((short)32);
                w.Write(images[i].Length);
                w.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] image in images)
            {
                w.Write(image);
            }
        }
        return 0;
    }

    private static Bitmap Draw(int size)
    {
        Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float s = size;
            float d = s * 0.44f;
            using (GraphicsPath tile = new GraphicsPath())
            using (LinearGradientBrush fill = new LinearGradientBrush(
                new PointF(0, 0), new PointF(s, s), Color.FromArgb(37, 99, 235), Color.FromArgb(124, 58, 237)))
            {
                tile.AddArc(0, 0, d, d, 180, 90);
                tile.AddArc(s - d, 0, d, d, 270, 90);
                tile.AddArc(s - d, s - d, d, d, 0, 90);
                tile.AddArc(0, s - d, d, d, 90, 90);
                tile.CloseFigure();
                g.FillPath(fill, tile);
            }

            float stroke = Math.Max(1.5f, s * 0.095f);
            Arrow(g, Color.White, stroke, s, 0.20f, 0.35f, 0.80f, 1);
            Arrow(g, Color.FromArgb(253, 224, 71), stroke, s, 0.80f, 0.65f, 0.20f, -1);
        }
        return bitmap;
    }

    private static void Arrow(Graphics g, Color color, float stroke, float s, float fromX, float y, float toX, int direction)
    {
        using (Pen pen = new Pen(color, stroke))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawLine(pen, fromX * s, y * s, toX * s, y * s);
            float headX = (toX - direction * 0.15f) * s;
            g.DrawLines(pen, new[]
            {
                new PointF(headX, (y - 0.15f) * s),
                new PointF(toX * s, y * s),
                new PointF(headX, (y + 0.15f) * s),
            });
        }
    }
}
