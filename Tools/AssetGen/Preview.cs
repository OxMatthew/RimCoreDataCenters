using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace AssetGen
{
    /// <summary>
    /// Renders the Workshop preview: an original, RimWorld-style top-down illustration of a working
    /// data center built from this mod's own sprites. No game artwork is used.
    /// </summary>
    internal static class Preview
    {
        private const int W = 1280;
        private const int H = 720;
        private const float T = 66f;               // tile size in pixels
        private const float RoomX = 79f;            // interior left edge
        private const float RoomY = 192f;           // interior top edge
        private const int Cols = 17;
        private const int Rows = 7;

        private static Color C(int r, int g, int b, int a = 255) { return Gfx.C(r, g, b, a); }

        public static Bitmap Render()
        {
            Bitmap bmp = new Bitmap(W, H);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                DrawBackdrop(g);
                DrawFloor(g);
                DrawWalls(g);
                DrawSecurityDoors(g);
                DrawCoolingPlumes(g);
                DrawConduits(g);

                using (Bitmap rack = Sprites.ServerRack())
                using (Bitmap rackFlipped = Rotate(rack, RotateFlipType.Rotate180FlipNone))
                using (Bitmap core = Sprites.NetworkCore())
                using (Bitmap coreFlipped = Rotate(core, RotateFlipType.Rotate180FlipNone))
                using (Bitmap cooler = Sprites.PrecisionCooler())
                using (Bitmap ups = Sprites.UpsUnit())
                using (Bitmap console = Sprites.OperationsConsole())
                using (Bitmap cartridge = Sprites.DataCartridge())
                using (Bitmap cartridgeResearch = Sprites.DataCartridgeResearch())
                using (Bitmap cartridgeFinancial = Sprites.DataCartridgeFinancial())
                using (Bitmap cartridgeMedical = Sprites.DataCartridgeMedical())
                using (Bitmap aiCore = Sprites.AiCore())
                {
                    Bitmap[] cartridgeVariety = { cartridge, cartridgeResearch, cartridgeFinancial, cartridgeMedical };
                    // The AI core sits in the aisle between the two network cores.
                    Blit(g, aiCore, RoomX + 1.45f * T, RoomY + 2f * T, 2 * T, 2 * T);
                    // Coolers embedded in the north wall, blowing cold air south into the room.
                    foreach (int col in new[] { 4, 8, 12 })
                    {
                        Blit(g, cooler, RoomX + col * T, RoomY - T, 2 * T, T);
                    }

                    // Two rows of racks facing the central aisle, one network core at the end of each row.
                    for (int i = 0; i < 12; i++)
                    {
                        Blit(g, rack, RoomX + (4 + i) * T, RoomY + 1 * T, T, T);
                        Blit(g, rackFlipped, RoomX + (4 + i) * T, RoomY + 4 * T, T, T);
                    }
                    Blit(g, core, RoomX + 1.7f * T, RoomY + 1 * T, 2 * T, T);
                    Blit(g, coreFlipped, RoomX + 1.7f * T, RoomY + 4 * T, 2 * T, T);

                    // UPS bank along the west wall.
                    for (int i = 0; i < 4; i++)
                    {
                        Blit(g, ups, RoomX + 0.4f * T, RoomY + (1 + i) * T, T, T);
                    }

                    // Finished cartridges waiting in front of a few racks, in every data type's color.
                    float[] cartCols = { 4, 6, 7, 9, 11, 13 };
                    for (int i = 0; i < cartCols.Length; i++)
                    {
                        float cx = RoomX + (cartCols[i] + 0.5f) * T;
                        Blit(g, cartridgeVariety[i % cartridgeVariety.Length], cx - 21f, RoomY + 2.06f * T, 42f, 42f);
                        if (i % 2 == 0)
                        {
                            Blit(g, cartridgeVariety[(i + 2) % cartridgeVariety.Length], cx - 12f, RoomY + 2.16f * T, 42f, 42f);
                        }
                    }
                    Blit(g, cartridgeFinancial, RoomX + 10.1f * T, RoomY + 3.05f * T, 38f, 38f);
                    Blit(g, cartridgeMedical, RoomX + 14.2f * T, RoomY + 2.95f * T, 38f, 38f);

                    // Operations console corner.
                    Blit(g, console, RoomX + 12.4f * T, RoomY + 5.2f * T, 2 * T, T);
                }

                DrawLedGlow(g);
                DrawColonists(g);
                DrawStatusPanel(g);
                DrawLightingAndVignette(g);
                DrawTitle(g);
            }
            return bmp;
        }

        // ------------------------------------------------------------------------------------------

        private static Bitmap Rotate(Bitmap source, RotateFlipType type)
        {
            Bitmap copy = (Bitmap)source.Clone();
            copy.RotateFlip(type);
            return copy;
        }

        private static void Blit(Graphics g, Bitmap bmp, float x, float y, float w, float h)
        {
            g.DrawImage(bmp, new RectangleF(x, y, w, h));
        }

        private static void DrawBackdrop(Graphics g)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, W, H), C(14, 20, 30), C(26, 36, 48), 90f))
            {
                g.FillRectangle(b, 0, 0, W, H);
            }
        }

        private static void DrawFloor(Graphics g)
        {
            // Concrete tiles with a slight checker and hairline joints.
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int shade = ((r + c) % 2 == 0) ? 0 : 7;
                    Color top = C(168 - shade, 174 - shade, 178 - shade);
                    Color bottom = C(152 - shade, 158 - shade, 164 - shade);
                    RectangleF tile = new RectangleF(RoomX + c * T, RoomY + r * T, T, T);
                    using (LinearGradientBrush b = new LinearGradientBrush(tile, top, bottom, 90f))
                    {
                        g.FillRectangle(b, tile);
                    }
                    Gfx.RectOutline(g, tile.X, tile.Y, tile.Width, tile.Height, C(96, 104, 112, 90), 1f);
                }
            }
            // Darker floor lane where the aisle runs between the racks.
            Gfx.Rect(g, RoomX, RoomY + 2f * T, Cols * T, 2f * T, C(20, 30, 44, 22));
        }

        private static void DrawWalls(Graphics g)
        {
            float thick = T;
            float x0 = RoomX - 20f;
            float y0 = RoomY - thick;
            float totalW = Cols * T + 40f;
            float totalH = Rows * T + thick + 20f;
            // Outer steel wall ring (the north edge is a full tile tall so the coolers can sit in it).
            RectangleF outer = new RectangleF(x0, y0, totalW, totalH);
            Gfx.SoftShadow(g, outer, 4f, 10f, 120);
            using (GraphicsPath ring = new GraphicsPath())
            {
                ring.AddRectangle(outer);
                ring.AddRectangle(new RectangleF(RoomX, RoomY, Cols * T, Rows * T));
                using (LinearGradientBrush b = new LinearGradientBrush(outer, C(120, 130, 140), C(72, 80, 90), 90f))
                {
                    g.FillPath(b, ring);
                }
            }
            // Panel seams.
            for (float x = x0; x < x0 + totalW; x += T)
            {
                Gfx.Line(g, x, y0, x, y0 + thick, C(50, 58, 66, 130), 1f);
                Gfx.Line(g, x, y0 + totalH - 20f, x, y0 + totalH, C(50, 58, 66, 130), 1f);
            }
            Gfx.RectOutline(g, outer.X, outer.Y, outer.Width, outer.Height, C(24, 30, 38), 3f);
            Gfx.RectOutline(g, RoomX, RoomY, Cols * T, Rows * T, C(30, 36, 44), 2.5f);

        }

        /// <summary>The checkpoint in the east wall: a biometric door and a metal detector gate, and a turned-away visitor.</summary>
        private static void DrawSecurityDoors(Graphics g)
        {
            float doorY = RoomY + 2f * T;
            float wallMid = RoomX + Cols * T + 10f;
            using (Bitmap bioLeaf = Sprites.BiometricDoorLeaf())
            using (Bitmap detLeaf = Sprites.MetalDetectorLeaf())
            using (Bitmap bioIcon = Sprites.DoorMenuIcon(bioLeaf))
            using (Bitmap detIcon = Sprites.DoorMenuIcon(detLeaf))
            using (Bitmap bio = Rotate(bioIcon, RotateFlipType.Rotate90FlipNone))
            using (Bitmap det = Rotate(detIcon, RotateFlipType.Rotate90FlipNone))
            {
                Blit(g, bio, wallMid - T / 2f, doorY, T, T);
                Blit(g, det, wallMid - T / 2f, doorY + T, T, T);
            }
            // A visitor with a rifle waits outside the metal detector; a red alert badge above.
            float vx = W - 40f;
            float vy = doorY + 1.5f * T;
            Pawn(g, vx, vy, 1.1f, C(120, 130, 96), C(60, 40, 30), C(228, 180, 140), 90f);
            Gfx.Line(g, vx - 24f, vy + 9f, vx + 20f, vy + 9f, C(40, 40, 46), 3.4f);
            Gfx.Ellipse(g, vx, doorY - 6f, 13f, 13f, C(200, 40, 34));
            Gfx.EllipseOutline(g, vx, doorY - 6f, 13f, 13f, C(255, 200, 190), 1.6f);
            using (Font f = new Font("Segoe UI Semibold", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.DrawString("!", f, new SolidBrush(C(255, 255, 255)), vx - 3.8f, doorY - 16.5f);
            }
        }

        private static void DrawCoolingPlumes(Graphics g)
        {
            // Soft cold-air fans below each precision cooling unit.
            foreach (int col in new[] { 4, 8, 12 })
            {
                float cx = RoomX + (col + 1f) * T;
                float cy = RoomY + 0.2f * T;
                GraphicsPath path = new GraphicsPath();
                path.AddEllipse(cx - 2.4f * T, cy - 0.4f * T, 4.8f * T, 4.6f * T);
                using (PathGradientBrush pg = new PathGradientBrush(path))
                {
                    pg.CenterPoint = new PointF(cx, cy);
                    pg.CenterColor = C(90, 190, 255, 120);
                    pg.SurroundColors = new[] { C(90, 190, 255, 0) };
                    g.FillPath(pg, path);
                }
                path.Dispose();
            }
            // Warm exhaust shimmer above the wall.
            foreach (int col in new[] { 4, 8, 12 })
            {
                float cx = RoomX + (col + 1f) * T;
                GraphicsPath path = new GraphicsPath();
                path.AddEllipse(cx - 1.2f * T, RoomY - 2.1f * T, 2.4f * T, 1.6f * T);
                using (PathGradientBrush pg = new PathGradientBrush(path))
                {
                    pg.CenterColor = C(255, 150, 80, 90);
                    pg.SurroundColors = new[] { C(255, 150, 80, 0) };
                    g.FillPath(pg, path);
                }
                path.Dispose();
            }
        }

        private static void DrawConduits(Graphics g)
        {
            // Power conduits running under the rack rows and along the UPS bank.
            Color body = C(196, 160, 60);
            Color edge = C(90, 70, 26);
            foreach (float row in new[] { 0.5f, 5.5f })
            {
                float y = RoomY + row * T;
                Gfx.Line(g, RoomX + 0.5f * T, y, RoomX + 16.5f * T, y, edge, 7f);
                Gfx.Line(g, RoomX + 0.5f * T, y, RoomX + 16.5f * T, y, body, 4.5f);
            }
            float x = RoomX + 1f * T;
            Gfx.Line(g, x, RoomY + 0.5f * T, x, RoomY + 5.5f * T, edge, 7f);
            Gfx.Line(g, x, RoomY + 0.5f * T, x, RoomY + 5.5f * T, body, 4.5f);
        }

        private static void DrawLedGlow(Graphics g)
        {
            // Faint status-LED glow along the front of each rack (green = operational, one amber = due for service).
            for (int i = 0; i < 12; i++)
            {
                Color led = (i == 6) ? C(255, 190, 60) : C(80, 255, 130);
                float cx = RoomX + (4 + i + 0.5f) * T;
                Glow(g, cx, RoomY + 1.9f * T, led);
                Glow(g, cx, RoomY + 4.1f * T, led);
            }
        }

        private static void Glow(Graphics g, float x, float y, Color c)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddEllipse(x - 15f, y - 9f, 30f, 18f);
            using (PathGradientBrush pg = new PathGradientBrush(path))
            {
                pg.CenterColor = Color.FromArgb(70, c);
                pg.SurroundColors = new[] { Color.FromArgb(0, c) };
                g.FillPath(pg, path);
            }
            path.Dispose();
        }

        // ---------------------------------------------------------------------------- colonists

        private static void DrawColonists(Graphics g)
        {
            // Servicing a rack in the aisle, hauling finished cartridges, and running the operations console.
            // facing: 0 = south, 180 = north, 90 = west, 270 = east
            Pawn(g, RoomX + 8.5f * T, RoomY + 2.6f * T, 1.35f, C(60, 110, 170), C(70, 40, 24), C(236, 190, 150), 180f);
            Sparks(g, RoomX + 8.5f * T, RoomY + 2.12f * T);
            Pawn(g, RoomX + 11.6f * T, RoomY + 3.35f * T, 1.35f, C(176, 84, 60), C(230, 200, 120), C(214, 160, 120), 270f);
            using (Bitmap carried = Sprites.DataCartridge())
            {
                Blit(g, carried, RoomX + 12.05f * T, RoomY + 2.95f * T, 40f, 40f);
            }
            Pawn(g, RoomX + 13.4f * T, RoomY + 6.55f * T, 1.35f, C(84, 140, 96), C(30, 26, 26), C(168, 118, 88), 180f);
            Pawn(g, RoomX + 15.6f * T, RoomY + 3.3f * T, 1.35f, C(150, 120, 200), C(180, 90, 50), C(240, 200, 170), 90f);
        }

        private static void Pawn(Graphics g, float cx, float cy, float s, Color shirt, Color hair, Color skin, float facing)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(facing);
            // Ground shadow.
            Gfx.Ellipse(g, 1.5f, 2.5f, 15f * s, 11f * s, C(0, 0, 0, 55));
            // Shoulders and arms.
            Gfx.Ellipse(g, 0, 0, 14f * s, 8.5f * s, C(24, 28, 34));
            Gfx.Ellipse(g, 0, 0, 13f * s, 7.5f * s, shirt);
            Gfx.Ellipse(g, -13f * s, 1f * s, 4.2f * s, 4.2f * s, C(24, 28, 34));
            Gfx.Ellipse(g, 13f * s, 1f * s, 4.2f * s, 4.2f * s, C(24, 28, 34));
            Gfx.Ellipse(g, -13f * s, 1f * s, 3.2f * s, 3.2f * s, skin);
            Gfx.Ellipse(g, 13f * s, 1f * s, 3.2f * s, 3.2f * s, skin);
            // Head and hair.
            Gfx.Ellipse(g, 0, -1f * s, 8.6f * s, 8.6f * s, C(24, 28, 34));
            Gfx.Ellipse(g, 0, -1f * s, 7.6f * s, 7.6f * s, skin);
            Gfx.Ellipse(g, 0, 1.2f * s, 7.6f * s, 6.4f * s, hair);
            Gfx.Ellipse(g, 0, 3.6f * s, 5f * s, 3.4f * s, skin);
            g.Restore(state);
        }

        private static void Sparks(Graphics g, float x, float y)
        {
            Random rng = new Random(9);
            for (int i = 0; i < 9; i++)
            {
                float dx = (float)(rng.NextDouble() * 22 - 11);
                float dy = (float)(rng.NextDouble() * 12 - 10);
                Gfx.Ellipse(g, x + dx, y + dy, 1.8f, 1.8f, C(255, 226, 120, 210));
            }
            Gfx.Ellipse(g, x, y, 6f, 6f, C(255, 240, 180, 120));
        }

        // ---------------------------------------------------------------------------- overlays

        private static void DrawStatusPanel(Graphics g)
        {
            // A mock of the in-game inspect pane, so the status text is visible in the preview.
            RectangleF panel = new RectangleF(RoomX + 0.6f * T, RoomY + 5.05f * T, 372f, 126f);
            Gfx.FillRound(g, panel, 6f, C(18, 24, 34, 232));
            Gfx.StrokeRound(g, panel, 6f, C(96, 116, 138), 1.5f);
            using (Font title = new Font("Segoe UI Semibold", 15f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font body = new Font("Segoe UI", 12.5f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush white = new SolidBrush(C(236, 240, 245)))
            using (SolidBrush dim = new SolidBrush(C(176, 190, 204)))
            using (SolidBrush green = new SolidBrush(C(140, 242, 152)))
            {
                g.DrawString("Server rack", title, white, panel.X + 12f, panel.Y + 8f);
                float y = panel.Y + 37f;
                g.DrawString("Status:", body, dim, panel.X + 12f, y);
                g.DrawString("Operational", body, green, panel.X + 70f, y);
                y += 21f;
                g.DrawString("Network: linked (6/6 racks on this core)", body, dim, panel.X + 12f, y);
                y += 21f;
                g.DrawString("Efficiency: 100% (heat 100%, maintenance 100%)", body, dim, panel.X + 12f, y);
                y += 21f;
                g.DrawString("Temperature: 27.4 C    Wear: 22%", body, dim, panel.X + 12f, y);
            }
        }

        private static void DrawLightingAndVignette(Graphics g)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(-W * 0.25f, -H * 0.35f, W * 1.5f, H * 1.7f);
                using (PathGradientBrush pg = new PathGradientBrush(path))
                {
                    pg.CenterColor = C(0, 0, 0, 0);
                    pg.SurroundColors = new[] { C(0, 0, 0, 92) };
                    pg.CenterPoint = new PointF(W * 0.5f, H * 0.6f);
                    Region old = g.Clip;
                    g.FillRectangle(pg, 0, 0, W, H);
                    g.Clip = old;
                }
            }
        }

        private static void DrawTitle(Graphics g)
        {
            // Title banner over the top band.
            using (LinearGradientBrush shade = new LinearGradientBrush(new Rectangle(0, 0, W, 150), C(6, 10, 16, 235), C(6, 10, 16, 0), 90f))
            {
                g.FillRectangle(shade, 0, 0, W, 150);
            }
            using (Font title = new Font("Segoe UI Semibold", 62f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font sub = new Font("Segoe UI", 25f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font tag = new Font("Segoe UI Semibold", 15f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                float x = 58f;
                g.DrawString("RimCore Data Centers", title, new SolidBrush(C(0, 0, 0, 170)), x + 3f, 15f);
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, 12f, 640f, 76f), C(255, 255, 255), C(170, 226, 255), 90f))
                {
                    g.DrawString("RimCore Data Centers", title, b, x, 12f);
                }
                g.DrawString("Build. Cool. Secure. Automate. Sell.", sub, new SolidBrush(C(0, 0, 0, 160)), x + 2f, 90f);
                g.DrawString("Build. Cool. Secure. Automate. Sell.", sub, new SolidBrush(C(96, 210, 246)), x, 88f);

                // Feature tags on the right of the banner.
                string[] tags = { "Server racks", "Upgrade tree", "AI core", "Data types", "Biometric doors" };
                float tx = W - 58f;
                for (int i = tags.Length - 1; i >= 0; i--)
                {
                    SizeF size = g.MeasureString(tags[i], tag);
                    float w = size.Width + 24f;
                    tx -= w;
                    RectangleF chip = new RectangleF(tx, 96f, w, 30f);
                    Gfx.FillRound(g, chip, 15f, C(20, 44, 64, 220));
                    Gfx.StrokeRound(g, chip, 15f, C(86, 190, 236), 1.4f);
                    g.DrawString(tags[i], tag, new SolidBrush(C(214, 240, 255)), chip.X + 12f, chip.Y + 5f);
                    tx -= 10f;
                }
            }
        }
    }
}
