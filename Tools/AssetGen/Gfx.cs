using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AssetGen
{
    /// <summary>Small 2D drawing helpers. Everything is drawn 4x supersampled and downscaled for clean edges.</summary>
    internal static class Gfx
    {
        public const int Supersample = 4;

        public static Color C(int r, int g, int b, int a = 255)
        {
            return Color.FromArgb(a, r, g, b);
        }

        /// <summary>Creates a transparent sprite by drawing in 1x units on a supersampled canvas.</summary>
        public static Bitmap Sprite(int w, int h, Action<Graphics> draw)
        {
            using (Bitmap big = new Bitmap(w * Supersample, h * Supersample))
            {
                using (Graphics g = Graphics.FromImage(big))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.ScaleTransform(Supersample, Supersample);
                    draw(g);
                }
                Bitmap result = new Bitmap(w, h);
                using (Graphics g = Graphics.FromImage(result))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(big, new Rectangle(0, 0, w, h));
                }
                return result;
            }
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            GraphicsPath p = new GraphicsPath();
            if (d <= 0.01f)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color top, Color bottom, float angle = 90f)
        {
            using (GraphicsPath p = Round(r, radius))
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X - 0.5f, r.Y - 0.5f, r.Width + 1f, r.Height + 1f), top, bottom, angle))
            {
                g.FillPath(b, p);
            }
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color solid)
        {
            using (GraphicsPath p = Round(r, radius))
            using (SolidBrush b = new SolidBrush(solid))
            {
                g.FillPath(b, p);
            }
        }

        public static void StrokeRound(Graphics g, RectangleF r, float radius, Color color, float width)
        {
            using (GraphicsPath p = Round(r, radius))
            using (Pen pen = new Pen(color, width))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawPath(pen, p);
            }
        }

        public static void Rect(Graphics g, float x, float y, float w, float h, Color fill)
        {
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillRectangle(b, x, y, w, h);
            }
        }

        public static void RectOutline(Graphics g, float x, float y, float w, float h, Color color, float width)
        {
            using (Pen p = new Pen(color, width))
            {
                g.DrawRectangle(p, x, y, w, h);
            }
        }

        public static void Line(Graphics g, float x1, float y1, float x2, float y2, Color color, float width)
        {
            using (Pen p = new Pen(color, width))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, x1, y1, x2, y2);
            }
        }

        public static void Ellipse(Graphics g, float cx, float cy, float rx, float ry, Color fill)
        {
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillEllipse(b, cx - rx, cy - ry, rx * 2f, ry * 2f);
            }
        }

        public static void EllipseOutline(Graphics g, float cx, float cy, float rx, float ry, Color color, float width)
        {
            using (Pen p = new Pen(color, width))
            {
                g.DrawEllipse(p, cx - rx, cy - ry, rx * 2f, ry * 2f);
            }
        }

        /// <summary>Soft drop shadow under a rounded rectangle, drawn as a few expanding translucent layers.</summary>
        public static void SoftShadow(Graphics g, RectangleF r, float radius, float spread, int maxAlpha)
        {
            const int steps = 6;
            for (int i = steps; i >= 1; i--)
            {
                float grow = spread * i / steps;
                RectangleF rr = new RectangleF(r.X - grow, r.Y - grow + spread * 0.35f, r.Width + grow * 2f, r.Height + grow * 2f);
                int alpha = (int)(maxAlpha * (1f - (float)(i - 1) / steps) / steps * 1.4f);
                FillRound(g, rr, radius + grow, C(0, 0, 0, Math.Max(2, Math.Min(255, alpha))));
            }
        }

        /// <summary>Highlight along the top/left and shade along the bottom/right of a rounded body for a subtle bevel.</summary>
        public static void Bevel(Graphics g, RectangleF r, float radius, int alpha = 70)
        {
            RectangleF inner = new RectangleF(r.X + 1.2f, r.Y + 1.2f, r.Width - 2.4f, r.Height - 2.4f);
            using (GraphicsPath p = Round(inner, Math.Max(0f, radius - 1f)))
            {
                Region old = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                using (Pen hi = new Pen(C(255, 255, 255, alpha), 2.2f))
                {
                    g.DrawLine(hi, inner.Left + radius, inner.Top + 1f, inner.Right - radius, inner.Top + 1f);
                    g.DrawLine(hi, inner.Left + 1f, inner.Top + radius, inner.Left + 1f, inner.Bottom - radius);
                }
                using (Pen lo = new Pen(C(0, 0, 0, alpha), 2.2f))
                {
                    g.DrawLine(lo, inner.Left + radius, inner.Bottom - 1f, inner.Right - radius, inner.Bottom - 1f);
                    g.DrawLine(lo, inner.Right - 1f, inner.Top + radius, inner.Right - 1f, inner.Bottom - radius);
                }
                g.Clip = old;
            }
        }
    }
}
