using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DmReplace
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            SetProcessDPIAware();
            CloseEarlierToasts();

            bool success = false;
            string message;
            try
            {
                string input = ReadClipboardText();
                ConvertResult result = Converter.Convert(input);
                if (result.Targets.Count > 0)
                {
                    Clipboard.SetDataObject(result.Text, true, 10, 100);
                    message = "已轉換為 " + string.Join("、", result.Targets.ToArray());
                    success = true;
                }
                else
                {
                    message = "未找到轉換目標";
                }
            }
            catch (ExternalException)
            {
                message = "無法存取剪貼簿";
            }

            Application.Run(new Toast(message, success));
        }

        // A repeat click starts a new process; without this the new toast would stack on the old one.
        private static void CloseEarlierToasts()
        {
            Process current = Process.GetCurrentProcess();
            foreach (Process other in Process.GetProcessesByName(current.ProcessName))
            {
                try
                {
                    if (other.Id != current.Id)
                    {
                        other.Kill();
                        other.WaitForExit(500);
                    }
                }
                catch (Exception)
                {
                    // It may have exited on its own already.
                }
            }
        }

        // Other apps can hold the clipboard open for a moment, so retry before giving up.
        private static string ReadClipboardText()
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
                }
                catch (ExternalException)
                {
                    if (attempt >= 10)
                    {
                        throw;
                    }
                    Thread.Sleep(100);
                }
            }
        }
    }

    internal sealed class Toast : Form
    {
        private const int HoldMs = 1800;
        private const int FadeMs = 200;
        private const int TickMs = 15;

        private readonly Bitmap image;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Stopwatch clock = new Stopwatch();

        public Toast(string message, bool success)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;

            image = RenderImage(message, success);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Bounds = new Rectangle(
                area.Left + (area.Width - image.Width) / 2,
                area.Top + image.Height,
                image.Width,
                image.Height);

            timer.Interval = TickMs;
            timer.Tick += OnTick;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOPMOST = 0x8;
                const int WS_EX_TRANSPARENT = 0x20;
                const int WS_EX_TOOLWINDOW = 0x80;
                const int WS_EX_LAYERED = 0x80000;
                const int WS_EX_NOACTIVATE = 0x8000000;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            clock.Start();
            timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            long t = clock.ElapsedMilliseconds;
            double opacity;
            if (t < FadeMs)
            {
                opacity = (double)t / FadeMs;
            }
            else if (t < FadeMs + HoldMs)
            {
                opacity = 1;
            }
            else if (t < FadeMs + HoldMs + FadeMs)
            {
                opacity = 1 - (double)(t - FadeMs - HoldMs) / FadeMs;
            }
            else
            {
                timer.Stop();
                Close();
                return;
            }
            Present((byte)(opacity * 255));
        }

        private static Bitmap RenderImage(string message, bool success)
        {
            float scale;
            using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero))
            {
                scale = screen.DpiX / 96f;
            }

            using (Font font = new Font("Microsoft JhengHei UI", 15 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

                SizeF text;
                using (Bitmap probe = new Bitmap(1, 1))
                using (Graphics g = Graphics.FromImage(probe))
                {
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    text = g.MeasureString(message, font, PointF.Empty, format);
                }

                float padX = 18 * scale;
                float gap = 10 * scale;
                float badge = 20 * scale;
                int width = (int)Math.Ceiling(padX + badge + gap + text.Width + padX);
                int height = (int)Math.Ceiling(46 * scale);

                Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    using (GraphicsPath shape = RoundedRect(0.5f, 0.5f, width - 1, height - 1, 12 * scale))
                    using (Brush fill = new SolidBrush(Color.FromArgb(245, 32, 33, 38)))
                    using (Pen edge = new Pen(Color.FromArgb(70, 255, 255, 255), 1))
                    {
                        g.FillPath(fill, shape);
                        g.DrawPath(edge, shape);
                    }

                    float badgeX = padX;
                    float badgeY = (height - badge) / 2;
                    Color accent = success ? Color.FromArgb(52, 199, 123) : Color.FromArgb(245, 166, 35);
                    using (Brush b = new SolidBrush(accent))
                    {
                        g.FillEllipse(b, badgeX, badgeY, badge, badge);
                    }
                    using (Pen mark = new Pen(Color.White, 2 * scale))
                    {
                        mark.StartCap = LineCap.Round;
                        mark.EndCap = LineCap.Round;
                        mark.LineJoin = LineJoin.Round;
                        if (success)
                        {
                            g.DrawLines(mark, new[]
                            {
                                new PointF(badgeX + badge * 0.27f, badgeY + badge * 0.52f),
                                new PointF(badgeX + badge * 0.44f, badgeY + badge * 0.68f),
                                new PointF(badgeX + badge * 0.73f, badgeY + badge * 0.34f),
                            });
                        }
                        else
                        {
                            g.DrawLine(mark, badgeX + badge * 0.5f, badgeY + badge * 0.26f, badgeX + badge * 0.5f, badgeY + badge * 0.56f);
                            g.DrawLine(mark, badgeX + badge * 0.5f, badgeY + badge * 0.76f, badgeX + badge * 0.5f, badgeY + badge * 0.76f);
                        }
                    }

                    using (Brush b = new SolidBrush(Color.White))
                    {
                        g.DrawString(message, font, b, padX + badge + gap, (height - text.Height) / 2, format);
                    }
                }
                return bitmap;
            }
        }

        private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
        {
            float d = r * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Per-pixel alpha keeps the rounded corners smooth, which a window Region cannot do.
        private void Present(byte alpha)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = image.GetHbitmap(Color.FromArgb(0));
            IntPtr previous = SelectObject(memDc, hBitmap);
            try
            {
                NativeSize size = new NativeSize { cx = image.Width, cy = image.Height };
                NativePoint source = new NativePoint { x = 0, y = 0 };
                NativePoint target = new NativePoint { x = Left, y = Top };
                BlendFunction blend = new BlendFunction
                {
                    BlendOp = 0,
                    BlendFlags = 0,
                    SourceConstantAlpha = alpha,
                    AlphaFormat = 1,
                };
                const int ULW_ALPHA = 2;
                UpdateLayeredWindow(Handle, screenDc, ref target, ref size, memDc, ref source, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                SelectObject(memDc, previous);
                DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll")]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize, IntPtr hdcSrc, ref NativePoint pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr h);
    }
}
