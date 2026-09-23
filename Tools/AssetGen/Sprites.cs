using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace AssetGen
{
    /// <summary>
    /// Original top-down building and item sprites. Each "South" drawing has its front facing the
    /// bottom of the image; the other rotations are derived by rotating it.
    /// </summary>
    internal static class Sprites
    {
        private static Color C(int r, int g, int b, int a = 255) { return Gfx.C(r, g, b, a); }

        // ------------------------------------------------------------------ Server rack (128x128)
        public static Bitmap ServerRack()
        {
            return Gfx.Sprite(128, 128, delegate (Graphics g)
            {
                RectangleF body = new RectangleF(11, 9, 106, 110);
                Gfx.SoftShadow(g, body, 9, 3.5f, 60);
                Gfx.FillRound(g, body, 9, C(88, 100, 116), C(46, 53, 65));

                // top surface: ventilation slots in three rows
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 6; col++)
                    {
                        float x = 27 + col * 13.2f;
                        float y = 24 + row * 13f;
                        Gfx.FillRound(g, new RectangleF(x, y, 9f, 5f), 1.6f, C(22, 27, 34));
                        Gfx.Rect(g, x + 0.8f, y + 3.6f, 7.4f, 0.9f, C(70, 82, 98, 150));
                    }
                }
                // data-glow strip along the back edge
                Gfx.FillRound(g, new RectangleF(22, 14.5f, 84, 3.2f), 1.6f, C(64, 190, 245, 230));
                Gfx.FillRound(g, new RectangleF(22, 14.5f, 84, 1.4f), 0.7f, C(180, 236, 255, 200));
                // asset plate
                Gfx.FillRound(g, new RectangleF(20, 68, 20, 7), 1.5f, C(190, 198, 208));
                Gfx.Line(g, 23, 71.5f, 36, 71.5f, C(90, 100, 112), 1.2f);

                // front face (south edge)
                RectangleF front = new RectangleF(11, 81, 106, 38);
                using (GraphicsPath clip = Gfx.Round(body, 9))
                {
                    Region old = g.Clip;
                    g.SetClip(clip, CombineMode.Intersect);
                    Gfx.Rect(g, front.X, front.Y, front.Width, front.Height, C(34, 40, 50));
                    Gfx.Rect(g, front.X, front.Y, front.Width, 1.6f, C(12, 15, 20));
                    g.Clip = old;
                }
                // drive bays
                for (int i = 0; i < 6; i++)
                {
                    float x = 19f + i * 16.4f;
                    Gfx.FillRound(g, new RectangleF(x, 86f, 13.5f, 11f), 1.5f, C(20, 24, 30));
                    Gfx.StrokeRound(g, new RectangleF(x, 86f, 13.5f, 11f), 1.5f, C(72, 84, 100), 0.9f);
                    Gfx.Rect(g, x + 2f, 89f, 6f, 1.2f, C(58, 68, 82));
                    Gfx.Rect(g, x + 2f, 92f, 4f, 1.2f, C(58, 68, 82));
                    Gfx.Ellipse(g, x + 10.5f, 91.5f, 1f, 1f, C(120, 130, 145));
                }
                // LED sockets (the game draws the lit LEDs on top of these)
                float[] ledX = { 29f, 64f, 99f };
                foreach (float lx in ledX)
                {
                    Gfx.FillRound(g, new RectangleF(lx - 8.5f, 104.5f, 17f, 11f), 2f, C(8, 10, 13));
                    Gfx.StrokeRound(g, new RectangleF(lx - 8.5f, 104.5f, 17f, 11f), 2f, C(82, 94, 110), 1f);
                }

                Gfx.StrokeRound(g, body, 9, C(16, 20, 26), 2.4f);
                Gfx.Bevel(g, body, 9, 55);
            });
        }

        // ------------------------------------------------------------------ Network core (256x128)
        public static Bitmap NetworkCore()
        {
            return Gfx.Sprite(256, 128, delegate (Graphics g)
            {
                RectangleF body = new RectangleF(8, 10, 240, 108);
                Gfx.SoftShadow(g, body, 10, 3.5f, 60);
                Gfx.FillRound(g, body, 10, C(70, 100, 118), C(34, 50, 62));

                // two rows of switch ports
                for (int row = 0; row < 2; row++)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        float x = 27f + i * 17.2f;
                        float y = 22f + row * 15f;
                        Gfx.FillRound(g, new RectangleF(x, y, 12f, 9f), 1.4f, C(14, 20, 26));
                        Gfx.StrokeRound(g, new RectangleF(x, y, 12f, 9f), 1.4f, C(96, 118, 134), 0.8f);
                        Gfx.Rect(g, x + 2.5f, y + 5.2f, 7f, 1.6f, C(214, 170, 60));
                        Gfx.Ellipse(g, x + 10.2f, y + 1.9f, 0.9f, 0.9f, ((i + row) % 3 == 0) ? C(90, 255, 140) : C(70, 200, 240));
                    }
                }
                // central status display with a little topology graph
                RectangleF disp = new RectangleF(70, 56, 116, 28);
                Gfx.FillRound(g, disp, 3, C(8, 16, 22));
                Gfx.StrokeRound(g, disp, 3, C(112, 150, 170), 1.2f);
                PointF[] nodes = { new PointF(84, 74), new PointF(104, 64), new PointF(128, 76), new PointF(152, 64), new PointF(172, 74) };
                for (int i = 0; i < nodes.Length - 1; i++)
                {
                    Gfx.Line(g, nodes[i].X, nodes[i].Y, nodes[i + 1].X, nodes[i + 1].Y, C(60, 200, 240, 200), 1.3f);
                }
                Gfx.Line(g, nodes[1].X, nodes[1].Y, nodes[3].X, nodes[3].Y, C(60, 200, 240, 120), 1f);
                foreach (PointF n in nodes)
                {
                    Gfx.Ellipse(g, n.X, n.Y, 3.2f, 3.2f, C(150, 240, 255));
                    Gfx.Ellipse(g, n.X, n.Y, 1.6f, 1.6f, C(20, 90, 130));
                }

                // front edge: patch cables
                Color[] cable = { C(236, 196, 60), C(70, 150, 230), C(230, 120, 60), C(90, 200, 120) };
                for (int i = 0; i < 12; i++)
                {
                    float x = 33f + i * 17.2f;
                    Gfx.Line(g, x, 96f, x + ((i % 2 == 0) ? 1.5f : -1.5f), 110f, cable[i % 4], 3f);
                    Gfx.Line(g, x, 96f, x + ((i % 2 == 0) ? 1.5f : -1.5f), 110f, C(255, 255, 255, 60), 0.9f);
                }
                Gfx.FillRound(g, new RectangleF(20, 91, 216, 3.4f), 1.6f, C(20, 28, 36));

                // brand-neutral label plate and status pips
                Gfx.FillRound(g, new RectangleF(24, 100, 26, 6), 1.5f, C(196, 208, 218));
                for (int i = 0; i < 3; i++)
                {
                    Gfx.Ellipse(g, 214f + i * 8f, 103f, 2.2f, 2.2f, C(80, 220, 255));
                }
                Gfx.StrokeRound(g, body, 10, C(14, 22, 30), 2.4f);
                Gfx.Bevel(g, body, 10, 55);
            });
        }

        // ------------------------------------------------------------------ Precision cooling unit (256x128)
        public static Bitmap PrecisionCooler()
        {
            return Gfx.Sprite(256, 128, delegate (Graphics g)
            {
                RectangleF body = new RectangleF(8, 8, 240, 112);
                Gfx.SoftShadow(g, body, 10, 3.5f, 60);
                Gfx.FillRound(g, body, 10, C(226, 232, 238), C(150, 160, 172));

                // exhaust band (north/back edge) - warm tinted vent
                Gfx.FillRound(g, new RectangleF(18, 15, 220, 10), 3, C(120, 96, 84));
                for (int i = 0; i < 22; i++)
                {
                    Gfx.Rect(g, 22f + i * 9.8f, 17f, 5.5f, 6f, C(66, 50, 44));
                }

                // two fan wells
                float[] fx = { 74f, 182f };
                foreach (float cx in fx)
                {
                    Gfx.Ellipse(g, cx, 62f, 41f, 41f, C(70, 80, 92));
                    Gfx.Ellipse(g, cx, 62f, 38f, 38f, C(34, 40, 48));
                    for (int i = 0; i < 7; i++)
                    {
                        double a0 = i * Math.PI * 2 / 7;
                        PointF[] blade =
                        {
                            new PointF(cx, 62),
                            new PointF(cx + (float)Math.Cos(a0) * 33f, 62 + (float)Math.Sin(a0) * 33f),
                            new PointF(cx + (float)Math.Cos(a0 + 0.62) * 33f, 62 + (float)Math.Sin(a0 + 0.62) * 33f)
                        };
                        using (SolidBrush b = new SolidBrush(C(112, 124, 138)))
                        {
                            g.FillPolygon(b, blade);
                        }
                    }
                    Gfx.EllipseOutline(g, cx, 62f, 34f, 34f, C(150, 162, 176), 1.6f);
                    Gfx.EllipseOutline(g, cx, 62f, 22f, 22f, C(150, 162, 176, 110), 1f);
                    Gfx.Ellipse(g, cx, 62f, 8f, 8f, C(58, 66, 78));
                    Gfx.Ellipse(g, cx, 62f, 3.4f, 3.4f, C(170, 184, 198));
                    // grille cross bars
                    Gfx.Line(g, cx - 38f, 62f, cx + 38f, 62f, C(190, 200, 212, 150), 1.4f);
                    Gfx.Line(g, cx, 24f, cx, 100f, C(190, 200, 212, 150), 1.4f);
                }

                // cold-air louvers (south/front edge)
                Gfx.FillRound(g, new RectangleF(14, 98, 228, 16), 3, C(70, 122, 176));
                for (int i = 0; i < 23; i++)
                {
                    Gfx.Rect(g, 18f + i * 9.7f, 100.5f, 6f, 11f, C(190, 226, 250));
                    Gfx.Rect(g, 18f + i * 9.7f, 100.5f, 6f, 2.4f, C(255, 255, 255, 150));
                }
                // control panel with a temperature dial
                Gfx.FillRound(g, new RectangleF(112, 88, 32, 8), 2, C(26, 34, 44));
                Gfx.Rect(g, 116f, 90.5f, 12f, 3f, C(80, 190, 250));
                Gfx.Ellipse(g, 136f, 92f, 2.4f, 2.4f, C(255, 120, 90));

                Gfx.StrokeRound(g, body, 10, C(48, 58, 70), 2.4f);
                Gfx.Bevel(g, body, 10, 90);
            });
        }

        // ------------------------------------------------------------------ UPS unit (128x128)
        public static Bitmap UpsUnit()
        {
            return Gfx.Sprite(128, 128, delegate (Graphics g)
            {
                RectangleF body = new RectangleF(16, 16, 96, 98);
                Gfx.SoftShadow(g, body, 12, 3.5f, 60);
                Gfx.FillRound(g, body, 12, C(74, 82, 90), C(38, 44, 52));

                // lid with a hazard stripe along the back
                RectangleF lid = new RectangleF(24, 24, 80, 60);
                Gfx.FillRound(g, lid, 6, C(96, 106, 116), C(62, 70, 80));
                using (GraphicsPath clip = Gfx.Round(new RectangleF(24, 24, 80, 11), 4))
                {
                    Region old = g.Clip;
                    g.SetClip(clip, CombineMode.Intersect);
                    Gfx.Rect(g, 24, 24, 80, 11, C(238, 190, 40));
                    for (int i = -2; i < 12; i++)
                    {
                        PointF[] stripe =
                        {
                            new PointF(24 + i * 9f, 35), new PointF(24 + i * 9f + 5f, 35),
                            new PointF(24 + i * 9f + 13f, 24), new PointF(24 + i * 9f + 8f, 24)
                        };
                        using (SolidBrush b = new SolidBrush(C(30, 30, 34)))
                        {
                            g.FillPolygon(b, stripe);
                        }
                    }
                    g.Clip = old;
                }
                // LCD readout
                Gfx.FillRound(g, new RectangleF(40, 42, 48, 16), 2.5f, C(12, 26, 18));
                Gfx.StrokeRound(g, new RectangleF(40, 42, 48, 16), 2.5f, C(120, 132, 144), 1.2f);
                for (int i = 0; i < 6; i++)
                {
                    Gfx.Rect(g, 44f + i * 6.6f, 46f, 4.6f, 8f, i < 5 ? C(80, 240, 130) : C(30, 70, 44));
                }
                // battery icon
                Gfx.RectOutline(g, 46f, 66f, 26f, 10f, C(230, 236, 242), 1.6f);
                Gfx.Rect(g, 72.5f, 69f, 3f, 4f, C(230, 236, 242));
                Gfx.Rect(g, 48f, 68f, 14f, 6f, C(90, 235, 140));
                // dark plate where the game draws the live charge bar
                Gfx.FillRound(g, new RectangleF(24, 92, 80, 16), 4, C(22, 26, 32));
                Gfx.StrokeRound(g, new RectangleF(24, 92, 80, 16), 4, C(80, 90, 102), 1f);
                // power cord loop on the right
                Gfx.EllipseOutline(g, 112f, 56f, 6f, 10f, C(24, 26, 30), 2.6f);

                Gfx.StrokeRound(g, body, 12, C(16, 20, 26), 2.4f);
                Gfx.Bevel(g, body, 12, 55);
            });
        }

        // ------------------------------------------------------------------ Operations console (256x128)
        public static Bitmap OperationsConsole()
        {
            return Gfx.Sprite(256, 128, delegate (Graphics g)
            {
                RectangleF desk = new RectangleF(10, 14, 236, 98);
                Gfx.SoftShadow(g, desk, 8, 3.5f, 55);
                Gfx.FillRound(g, desk, 8, C(92, 102, 114), C(60, 68, 78));

                // two monitors along the back edge, seen from above at a slight angle
                float[] mx = { 32f, 140f };
                foreach (float x in mx)
                {
                    Gfx.FillRound(g, new RectangleF(x + 8, 40, 18, 12), 2, C(30, 34, 40));            // stand
                    Gfx.FillRound(g, new RectangleF(x, 18, 84, 24), 3, C(22, 26, 32));                  // bezel
                    Gfx.FillRound(g, new RectangleF(x + 3, 21, 78, 15), 1.6f, C(64, 158, 226), C(28, 92, 168), 90f);
                    Gfx.Line(g, x + 8, 32, x + 22, 27, C(220, 245, 255, 210), 1.3f);
                    Gfx.Line(g, x + 22, 27, x + 36, 31, C(220, 245, 255, 210), 1.3f);
                    Gfx.Line(g, x + 36, 31, x + 52, 24, C(220, 245, 255, 210), 1.3f);
                    Gfx.Line(g, x + 52, 24, x + 72, 28, C(220, 245, 255, 210), 1.3f);
                    Gfx.Rect(g, x + 4f, 23f, 30f, 1.2f, C(255, 255, 255, 90));
                }
                // keyboard and mouse
                Gfx.FillRound(g, new RectangleF(66, 72, 108, 26), 3, C(26, 30, 36));
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 14; col++)
                    {
                        Gfx.FillRound(g, new RectangleF(70f + col * 7.4f, 75f + row * 7f, 5.6f, 5f), 1f, C(58, 66, 78));
                    }
                }
                Gfx.FillRound(g, new RectangleF(186, 74, 22, 22), 3, C(70, 78, 90));
                Gfx.FillRound(g, new RectangleF(191, 78, 12, 16), 5, C(30, 34, 40));
                Gfx.Line(g, 197f, 78f, 197f, 85f, C(90, 98, 110), 1f);
                // clutter: mug and a notepad
                Gfx.Ellipse(g, 30f, 84f, 7f, 7f, C(232, 232, 236));
                Gfx.Ellipse(g, 30f, 84f, 4.6f, 4.6f, C(90, 60, 40));
                Gfx.Rect(g, 218f, 56f, 18f, 24f, C(232, 226, 200));
                Gfx.Line(g, 221f, 62f, 233f, 62f, C(120, 130, 150), 1f);
                Gfx.Line(g, 221f, 67f, 233f, 67f, C(120, 130, 150), 1f);
                Gfx.Line(g, 221f, 72f, 229f, 72f, C(120, 130, 150), 1f);

                Gfx.StrokeRound(g, desk, 8, C(18, 22, 28), 2.4f);
                Gfx.Bevel(g, desk, 8, 60);
            });
        }

        // ------------------------------------------------------------------ Data cartridge (64x64)
        public static Bitmap DataCartridge()
        {
            return DataCartridge(C(62, 74, 112), C(26, 32, 56), C(60, 190, 240), C(236, 196, 84), null);
        }

        /// <summary>Research data: a cyan-green "compute" palette.</summary>
        public static Bitmap DataCartridgeResearch()
        {
            return DataCartridge(C(52, 100, 92), C(20, 42, 38), C(64, 220, 176), C(210, 220, 120), null);
        }

        /// <summary>Financial data: a warm gold "ledger" palette with brighter contacts.</summary>
        public static Bitmap DataCartridgeFinancial()
        {
            return DataCartridge(C(112, 92, 48), C(48, 38, 18), C(232, 182, 60), C(255, 224, 120), null);
        }

        /// <summary>Medical data: a red-and-white "case file" palette with a plus mark instead of contacts.</summary>
        public static Bitmap DataCartridgeMedical()
        {
            return DataCartridge(C(120, 58, 58), C(50, 22, 22), C(228, 92, 84), C(255, 210, 200), "plus");
        }

        /// <summary>
        /// The data cartridge sprite, shared by every specialization. Only the colors (and, for Medical, the
        /// bottom accent) change between variants, so every cartridge reads as the same physical object.
        /// </summary>
        private static Bitmap DataCartridge(Color bodyTop, Color bodyBottom, Color labelBar, Color contactColor, string bottomAccent)
        {
            return Gfx.Sprite(64, 64, delegate (Graphics g)
            {
                g.TranslateTransform(32f, 32f);
                g.RotateTransform(-16f);
                g.TranslateTransform(-32f, -32f);

                RectangleF body = new RectangleF(15, 9, 34, 46);
                Gfx.SoftShadow(g, body, 4, 2.5f, 70);
                using (GraphicsPath p = new GraphicsPath())
                {
                    // rounded rectangle with a chamfered top-right corner (the notch)
                    p.AddArc(15, 9, 8, 8, 180, 90);
                    p.AddLine(43, 9, 49, 15);
                    p.AddArc(41, 47, 8, 8, 0, 90);
                    p.AddArc(15, 47, 8, 8, 90, 90);
                    p.CloseFigure();
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(15, 9, 34, 46), bodyTop, bodyBottom, 90f))
                    {
                        g.FillPath(b, p);
                    }
                    using (Pen pen = new Pen(C(12, 16, 30), 1.6f))
                    {
                        pen.LineJoin = LineJoin.Round;
                        g.DrawPath(pen, p);
                    }
                }
                // label
                Gfx.FillRound(g, new RectangleF(20, 15, 24, 19), 2, C(232, 238, 246));
                Gfx.Rect(g, 20, 15, 24, 4.5f, labelBar);
                Gfx.Line(g, 23, 25, 40, 25, C(110, 122, 140), 1.2f);
                Gfx.Line(g, 23, 29, 34, 29, C(110, 122, 140), 1.2f);
                // bottom accent: gold contacts by default, or a plus mark for medical data
                if (bottomAccent == "plus")
                {
                    Gfx.FillRound(g, new RectangleF(24, 37, 16, 16), 1.5f, C(250, 250, 250));
                    Gfx.Rect(g, 30.5f, 40f, 3f, 10f, contactColor);
                    Gfx.Rect(g, 27f, 43.5f, 10f, 3f, contactColor);
                }
                else
                {
                    Gfx.FillRound(g, new RectangleF(24, 38, 16, 7), 1.5f, C(150, 160, 174));
                    for (int i = 0; i < 5; i++)
                    {
                        Gfx.Rect(g, 21f + i * 5f, 48.5f, 3f, 5f, contactColor);
                    }
                }
                // top-left sheen
                Gfx.Line(g, 18f, 14f, 18f, 44f, C(255, 255, 255, 60), 1.4f);
            });
        }

        // ------------------------------------------------------------------ Security doors (128x128 movers)
        // RimWorld draws a door as two copies of one "mover" picture: the picture as drawn plus a mirrored
        // copy, slid apart when the door opens. So each mover below is ONE leaf of the door, in the left half
        // of the image; the two leaves meet at the vertical centre line.

        public static Bitmap BiometricDoorLeaf()
        {
            return Gfx.Sprite(128, 128, delegate (Graphics g)
            {
                RectangleF leaf = new RectangleF(1, 34, 63.5f, 60);
                Gfx.SoftShadow(g, leaf, 4, 3f, 70);
                Gfx.FillRound(g, leaf, 3.5f, C(80, 94, 112), C(38, 46, 58));
                // brushed-steel panel lines
                for (int i = 0; i < 5; i++)
                {
                    Gfx.Rect(g, 5f, 42f + i * 9.5f, 40f, 1f, C(20, 26, 34, 120));
                }
                // status strip along the bottom edge (teal = secured)
                Gfx.FillRound(g, new RectangleF(5, 84, 56, 4.4f), 2f, C(50, 190, 230));
                Gfx.FillRound(g, new RectangleF(5, 84, 56, 1.8f), 0.9f, C(180, 240, 255, 200));
                // the scanner lens near the seam: dark bezel, teal iris, bright glint
                Gfx.Ellipse(g, 54f, 62f, 8.5f, 8.5f, C(14, 18, 24));
                Gfx.EllipseOutline(g, 54f, 62f, 8.5f, 8.5f, C(150, 165, 182), 1.6f);
                Gfx.Ellipse(g, 54f, 62f, 5.4f, 5.4f, C(30, 140, 190));
                Gfx.Ellipse(g, 54f, 62f, 2.6f, 2.6f, C(8, 30, 44));
                Gfx.Ellipse(g, 52.2f, 60.2f, 1.3f, 1.3f, C(230, 250, 255));
                // rivets
                foreach (float rx in new[] { 6.5f, 41f })
                {
                    Gfx.Ellipse(g, rx, 40f, 1.5f, 1.5f, C(150, 160, 172));
                }
                Gfx.Rect(g, 62.6f, 35f, 1.9f, 58f, C(14, 18, 24));   // seam edge
                Gfx.StrokeRound(g, leaf, 3.5f, C(14, 18, 24), 2f);
                Gfx.Bevel(g, leaf, 3.5f, 60);
            });
        }

        public static Bitmap MetalDetectorLeaf()
        {
            return Gfx.Sprite(128, 128, delegate (Graphics g)
            {
                RectangleF leaf = new RectangleF(1, 34, 63.5f, 60);
                Gfx.SoftShadow(g, leaf, 4, 3f, 70);
                Gfx.FillRound(g, leaf, 3.5f, C(104, 112, 120), C(56, 62, 70));
                // hazard stripe along the top edge
                using (GraphicsPath clip = Gfx.Round(new RectangleF(1, 34, 63.5f, 13), 3.5f))
                {
                    Region old = g.Clip;
                    g.SetClip(clip, CombineMode.Intersect);
                    Gfx.Rect(g, 1, 34, 63.5f, 13, C(240, 192, 40));
                    for (int i = -2; i < 9; i++)
                    {
                        PointF[] stripe =
                        {
                            new PointF(1 + i * 10f, 47), new PointF(1 + i * 10f + 5f, 47),
                            new PointF(1 + i * 10f + 13f, 34), new PointF(1 + i * 10f + 8f, 34)
                        };
                        using (SolidBrush b = new SolidBrush(C(28, 28, 32)))
                        {
                            g.FillPolygon(b, stripe);
                        }
                    }
                    g.Clip = old;
                }
                // detector coil: an arch drawn on the panel
                using (Pen pen = new Pen(C(236, 190, 50), 3.4f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, 14f, 52f, 34f, 34f, 180, 180);
                    g.DrawLine(pen, 14f, 69f, 14f, 86f);
                    g.DrawLine(pen, 48f, 69f, 48f, 86f);
                }
                Gfx.Rect(g, 22f, 68f, 18f, 1.4f, C(30, 34, 40, 140));
                // alarm lamp near the seam
                Gfx.Ellipse(g, 55f, 62f, 5.5f, 5.5f, C(50, 14, 12));
                Gfx.Ellipse(g, 55f, 62f, 3.6f, 3.6f, C(255, 90, 70));
                Gfx.Ellipse(g, 53.8f, 60.8f, 1.2f, 1.2f, C(255, 220, 210));
                Gfx.Rect(g, 62.6f, 35f, 1.9f, 58f, C(14, 18, 24));   // seam edge
                Gfx.StrokeRound(g, leaf, 3.5f, C(16, 18, 22), 2f);
                Gfx.Bevel(g, leaf, 3.5f, 60);
            });
        }

        /// <summary>The whole door for the architect menu: the leaf plus its mirror image.</summary>
        public static Bitmap DoorMenuIcon(Bitmap leaf)
        {
            Bitmap icon = new Bitmap(128, 128);
            using (Graphics g = Graphics.FromImage(icon))
            {
                g.DrawImage(leaf, 0, 0, 128, 128);
                using (Bitmap mirrored = (Bitmap)leaf.Clone())
                {
                    mirrored.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    g.DrawImage(mirrored, 0, 0, 128, 128);
                }
            }
            return icon;
        }

        // ------------------------------------------------------------------ AI core (256x256, 2x2 tiles)
        public static Bitmap AiCore()
        {
            return Gfx.Sprite(256, 256, delegate (Graphics g)
            {
                RectangleF body = new RectangleF(14, 14, 228, 228);
                Gfx.SoftShadow(g, body, 16, 4f, 70);
                Gfx.FillRound(g, body, 16, C(58, 64, 88), C(24, 28, 44));
                // corner brackets
                foreach (PointF c in new[] { new PointF(28, 28), new PointF(228, 28), new PointF(28, 200), new PointF(228, 200) })
                {
                    Gfx.Ellipse(g, c.X, c.Y, 6f, 6f, C(150, 158, 178));
                    Gfx.Ellipse(g, c.X, c.Y, 2.4f, 2.4f, C(30, 34, 46));
                }
                // vent slots down both sides
                for (int i = 0; i < 9; i++)
                {
                    Gfx.FillRound(g, new RectangleF(24, 50 + i * 15f, 22, 6), 2f, C(14, 16, 26));
                    Gfx.FillRound(g, new RectangleF(210, 50 + i * 15f, 22, 6), 2f, C(14, 16, 26));
                    Gfx.Rect(g, 26, 54 + i * 15f, 18, 1.2f, C(80, 96, 130, 140));
                    Gfx.Rect(g, 212, 54 + i * 15f, 18, 1.2f, C(80, 96, 130, 140));
                }
                // the lens: glowing rings around a dark iris
                const float cx = 128f;
                const float cy = 114f;
                Gfx.Ellipse(g, cx, cy, 78f, 78f, C(12, 14, 24));
                Gfx.EllipseOutline(g, cx, cy, 78f, 78f, C(120, 132, 160), 3f);
                for (int i = 0; i < 4; i++)
                {
                    Gfx.EllipseOutline(g, cx, cy, 66f - i * 12f, 66f - i * 12f, C(70 + i * 25, 170 + i * 15, 245, 200 - i * 30), 2.6f);
                }
                Gfx.Ellipse(g, cx, cy, 24f, 24f, C(30, 120, 210));
                Gfx.Ellipse(g, cx, cy, 15f, 15f, C(8, 22, 44));
                Gfx.Ellipse(g, cx - 5f, cy - 6f, 5f, 5f, C(210, 240, 255));
                // tick marks around the outer ring
                for (int i = 0; i < 24; i++)
                {
                    double a = i * Math.PI * 2 / 24;
                    Gfx.Line(g, cx + (float)Math.Cos(a) * 71f, cy + (float)Math.Sin(a) * 71f, cx + (float)Math.Cos(a) * 76f, cy + (float)Math.Sin(a) * 76f, C(150, 190, 240, 180), 1.6f);
                }
                // front panel (south edge): pips and a label plate
                Gfx.FillRound(g, new RectangleF(40, 204, 176, 26), 5f, C(16, 18, 30));
                Gfx.StrokeRound(g, new RectangleF(40, 204, 176, 26), 5f, C(96, 108, 140), 1.2f);
                for (int i = 0; i < 8; i++)
                {
                    Gfx.Ellipse(g, 56f + i * 12f, 217f, 3.2f, 3.2f, i < 5 ? C(90, 230, 150) : C(50, 60, 84));
                }
                Gfx.FillRound(g, new RectangleF(158, 210, 48, 14), 3f, C(200, 208, 222));
                Gfx.Line(g, 164, 217, 200, 217, C(80, 90, 110), 1.6f);
                Gfx.StrokeRound(g, body, 16, C(14, 16, 28), 3f);
                Gfx.Bevel(g, body, 16, 60);
            });
        }

        public static Bitmap AiIconDirective()
        {
            return Gfx.Sprite(64, 64, delegate (Graphics g)
            {
                Gfx.FillRound(g, new RectangleF(6, 6, 52, 52), 10, C(40, 48, 74), C(20, 26, 44));
                Gfx.StrokeRound(g, new RectangleF(6, 6, 52, 52), 10, C(110, 190, 240), 2f);
                float[] knob = { 0.3f, 0.7f, 0.5f };
                for (int i = 0; i < 3; i++)
                {
                    float y = 20f + i * 13f;
                    Gfx.Line(g, 14f, y, 50f, y, C(90, 104, 140), 3f);
                    Gfx.Ellipse(g, 14f + 36f * knob[i], y, 5f, 5f, C(110, 210, 255));
                    Gfx.Ellipse(g, 14f + 36f * knob[i], y, 2.2f, 2.2f, C(14, 28, 50));
                }
            });
        }

        /// <summary>Command icon for a server rack's Specialization menu: three small labeled cards fanned out.</summary>
        public static Bitmap SpecializationIcon()
        {
            return Gfx.Sprite(64, 64, delegate (Graphics g)
            {
                Color[] cards = { C(64, 220, 176), C(232, 182, 60), C(228, 92, 84) };
                float[] rot = { -18f, 0f, 18f };
                float[] offX = { -14f, 0f, 14f };
                float[] offY = { 4f, -3f, 4f };
                for (int i = 0; i < 3; i++)
                {
                    GraphicsState state = g.Save();
                    g.TranslateTransform(32f + offX[i], 34f + offY[i]);
                    g.RotateTransform(rot[i]);
                    RectangleF card = new RectangleF(-10f, -15f, 20f, 28f);
                    Gfx.FillRound(g, card, 3f, C(26, 30, 44));
                    Gfx.FillRound(g, new RectangleF(card.X + 2f, card.Y + 2f, card.Width - 4f, 6f), 1.5f, cards[i]);
                    Gfx.StrokeRound(g, card, 3f, C(12, 14, 22), 1.6f);
                    g.Restore(state);
                }
            });
        }

        public static Bitmap AiIconReport()
        {
            return Gfx.Sprite(64, 64, delegate (Graphics g)
            {
                Gfx.FillRound(g, new RectangleF(12, 6, 40, 52), 5, C(236, 240, 248), C(190, 198, 214));
                Gfx.StrokeRound(g, new RectangleF(12, 6, 40, 52), 5, C(60, 74, 104), 2f);
                for (int i = 0; i < 3; i++)
                {
                    Gfx.Line(g, 19f, 14f + i * 6f, 45f - (i == 2 ? 10f : 0f), 14f + i * 6f, C(110, 122, 148), 1.8f);
                }
                float[] bars = { 10f, 18f, 13f, 22f };
                for (int i = 0; i < bars.Length; i++)
                {
                    Gfx.Rect(g, 20f + i * 8f, 50f - bars[i], 5f, bars[i], C(50, 160, 230));
                }
            });
        }

        // ------------------------------------------------------------------ output helpers
        /// <summary>
        /// Saves the four Graphic_Multi rotations. RimWorld's default rotation (Rot4.North) shows the
        /// building with its front facing the bottom of the image, so the drawing is the _north image;
        /// each clockwise quarter turn of the building rotates the picture clockwise.
        /// </summary>
        public static void SaveRotations(Bitmap frontDown, string folder, string baseName)
        {
            Directory.CreateDirectory(folder);
            Save(frontDown, Path.Combine(folder, baseName + "_north.png"));
            SaveRotated(frontDown, RotateFlipType.Rotate90FlipNone, Path.Combine(folder, baseName + "_east.png"));
            SaveRotated(frontDown, RotateFlipType.Rotate180FlipNone, Path.Combine(folder, baseName + "_south.png"));
            SaveRotated(frontDown, RotateFlipType.Rotate270FlipNone, Path.Combine(folder, baseName + "_west.png"));
        }

        private static void SaveRotated(Bitmap source, RotateFlipType rotation, string path)
        {
            using (Bitmap copy = (Bitmap)source.Clone())
            {
                copy.RotateFlip(rotation);
                Save(copy, path);
            }
        }

        public static void Save(Bitmap bmp, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bmp.Save(path, ImageFormat.Png);
        }
    }
}
