using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal static unsafe partial class VisualEngine3D
{
    private static void DrawElevatorPanelTexture(ref Image img, Finish finish)
    {
        const int w = 512, h = 1024;
        Image canvas = img;
        Color* px = (Color*)canvas.Data;
        bool cabin = finish == Finish.CabinPanel;
        bool broken = finish == Finish.LiftPanelBroken;

        // Horizontal brushed anodized steel / stainless grain + chamfer vignette.
        for (int y = 0; y < h; y++)
        {
            int streak = Hash(0, y) % 9 - 4;
            for (int x = 0; x < w; x++)
            {
                int fine = Hash(x, y) % 7 - 3;
                int edge = Math.Min(Math.Min(x, w - 1 - x), Math.Min(y, h - 1 - y));
                int bevelShade = edge < 8 ? (x < 10 || y < 10 ? 22 : -18) : 0;
                int baseR = cabin ? 56 : 24, baseG = cabin ? 61 : 28, baseB = cabin ? 67 : 34;
                int d = streak + fine + bevelShade;
                px[y * w + x] = new Color(
                    (byte)Math.Clamp(baseR + d, 0, 255),
                    (byte)Math.Clamp(baseG + d, 0, 255),
                    (byte)Math.Clamp(baseB + d, 0, 255), (byte)255);
            }
        }

        Color brass = new(218, 184, 122, 255);
        Color steelHi = new(138, 146, 154, 255);
        Color steelLo = new(44, 50, 58, 255);
        Color screenBg = new(8, 12, 16, 255);
        Color faultRed = new(242, 68, 52, 255);
        Color faultAmber = new(246, 164, 58, 255);
        Color okTeal = new(98, 232, 212, 255);
        Color textMuted = new(176, 184, 192, 255);

        void DrawRing(int cx, int cy, float radius, float thickness, Color col, bool rightArcOnly = false)
        {
            int pad = (int)MathF.Ceiling(radius + thickness + 1);
            int y0 = Math.Max(0, cy - pad), y1 = Math.Min(h - 1, cy + pad);
            int x0 = Math.Max(0, cx - pad), x1 = Math.Min(w - 1, cx + pad);
            float half = thickness * .5f;
            for (int y = y0; y <= y1; y++)
            {
                float dy = y - cy;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    if (rightArcOnly && (dx < 2 || MathF.Abs(dy) > dx * 1.15f)) continue;
                    float dist = MathF.Abs(MathF.Sqrt(dx * dx + dy * dy) - radius);
                    if (dist <= half + .7f)
                    {
                        float a = Math.Clamp(1f - Math.Max(0f, dist - half) / .7f, 0f, 1f);
                        Color dst = px[y * w + x];
                        px[y * w + x] = new Color(
                            (byte)(dst.R + (col.R - dst.R) * a),
                            (byte)(dst.G + (col.G - dst.G) * a),
                            (byte)(dst.B + (col.B - dst.B) * a), (byte)255);
                    }
                }
            }
        }

        void FrameRect(int x, int y, int rw, int rh, Color border, Color fill)
        {
            Raylib.ImageDrawRectangle(ref canvas, x, y, rw, rh, fill);
            Raylib.ImageDrawRectangleLines(ref canvas, new Rectangle(x, y, rw, rh), 3, border);
        }

        void CenterText(string text, int cy, int size, Color color, int left = 0, int right = w)
        {
            var font = GameFonts.Get(Typeface.InterfaceBold, size);
            int tw = (int)Raylib.MeasureTextEx(font, text, size, 0).X;
            int tx = left + (right - left - tw) / 2;
            Raylib.ImageDrawTextEx(ref canvas, font, text, new(tx, cy), size, 0, color);
        }

        void DrawText(string text, int x, int y, int size, Color color)
        {
            var font = GameFonts.Get(Typeface.InterfaceBold, size);
            Raylib.ImageDrawTextEx(ref canvas, font, text, new(x, y), size, 0, color);
        }

        // Crisp 7-segment LED digit renderer.
        void SevenSegDigit(int digit, int ox, int oy, int dw, int dh, int t, Color lit, Color dim)
        {
            int mask = digit switch
            {
                0 => 0b1111110, 1 => 0b0110000, 2 => 0b1101101, 3 => 0b1111001,
                4 => 0b0110011, 5 => 0b1011011, 6 => 0b1011111, 7 => 0b1110000,
                8 => 0b1111111, 9 => 0b1111011, _ => 0
            };
            int half = dh / 2;
            void Seg(int bit, int x, int y, int sw, int sh) =>
                Raylib.ImageDrawRectangle(ref canvas, x, y, sw, sh, ((mask >> bit) & 1) != 0 ? lit : dim);
            Seg(6, ox + t, oy, dw - 2 * t, t);
            Seg(5, ox + dw - t, oy + t, t, half - t - t / 2);
            Seg(4, ox + dw - t, oy + half + t / 2, t, half - t - t / 2);
            Seg(3, ox + t, oy + dh - t, dw - 2 * t, t);
            Seg(2, ox, oy + half + t / 2, t, half - t - t / 2);
            Seg(1, ox, oy + t, t, half - t - t / 2);
            Seg(0, ox + t, oy + half - t / 2, dw - 2 * t, t);
        }

        void BrailleCell(int ox, int oy, int bits)
        {
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 2; c++)
                {
                    bool raised = ((bits >> (r * 2 + c)) & 1) != 0;
                    Raylib.ImageDrawCircle(ref canvas, ox + c * 14, oy + r * 14, raised ? 4 : 2,
                        raised ? new Color(196, 202, 208, 255) : new Color(54, 60, 68, 255));
                }
        }

        // Subtle outer recessed hairline border.
        Raylib.ImageDrawRectangleLines(ref canvas, new Rectangle(12, 12, w - 24, h - 24), 3, steelLo);

        if (cabin)
        {
            CenterText(Loc.T("ЭНТРОПИК · КАБИНА", "ENTROPIC · CABIN"), 24, 24, brass);
            FrameRect(44, 64, w - 88, 134, new Color(92, 100, 108, 255), screenBg);
            for (int sy = 72; sy < 194; sy += 6)
                Raylib.ImageDrawRectangle(ref canvas, 48, sy, w - 96, 2, new Color(16, 22, 28, 255));
            Color dimSeg = new(24, 42, 44, 255);
            SevenSegDigit(0, 88, 86, 54, 90, 10, okTeal, dimSeg);
            SevenSegDigit(1, 162, 86, 54, 90, 10, okTeal, dimSeg);
            Raylib.ImageDrawTriangle(ref canvas, new(266, 152), new(300, 96), new(334, 152), okTeal);
            DrawText(Loc.T("ЭТАЖ", "FLOOR"), 354, 106, 24, brass);
            DrawText("1 / 6", 354, 138, 26, okTeal);

            // Intercom speaker grille.
            FrameRect(68, 220, w - 136, 72, steelLo, new Color(36, 40, 46, 255));
            for (int sy = 234; sy <= 278; sy += 11)
                Raylib.ImageDrawRectangle(ref canvas, 92, sy, w - 184, 5, new Color(12, 14, 18, 255));

            // 2x4 button escutcheon labels next to 3D cylindrical cabin buttons.
            string[] labels = ["01", "02", "03", "04", "05", "06", "< >", "> <"];
            int[] rowY = [372, 502, 632, 762];
            int[] colX = [148, 364];
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    int cx = colX[c], cy = rowY[r];
                    Raylib.ImageDrawCircle(ref canvas, cx, cy, 52, new Color(28, 32, 38, 255));
                    DrawRing(cx, cy, 52, 3f, steelHi);
                    int labelX = c == 0 ? cx - 102 : cx + 62;
                    DrawText(labels[r * 2 + c], labelX, cy - 15, 28, r == 0 && c == 0 ? okTeal : brass);
                }
            }

            // Bottom emergency bell / stop housing.
            FrameRect(76, 854, w - 152, 118, new Color(165, 58, 48, 255), new Color(40, 22, 24, 255));
            DrawText(Loc.T("СТОП / ТРЕВОГА", "STOP / ALARM"), 198, 898, 26, new Color(248, 128, 114, 255));
            Raylib.ImageDrawCircle(ref canvas, 136, 913, 36, new Color(186, 36, 32, 255));
            DrawRing(136, 913, 36, 3f, new Color(245, 198, 160, 255));
            Raylib.ImageFlipVertical(ref canvas);
            img = canvas;
            return;
        }

        // === EXTERIOR ELEVATOR CALL & SECURITY LOCK PANEL ===
        CenterText(Loc.T("ЭНТРОПИК · ЛИФТ 01", "ENTROPIC · LIFT 01"), 26, 26, brass);
        Raylib.ImageDrawRectangle(ref canvas, 38, 66, w - 76, 2, new Color(90, 76, 52, 255));

        // Digital floor & status LED display window (py 84..242).
        FrameRect(42, 84, w - 84, 158, new Color(98, 108, 116, 255), screenBg);
        for (int sy = 92; sy < 236; sy += 6)
            Raylib.ImageDrawRectangle(ref canvas, 46, sy, w - 92, 2, new Color(15, 20, 24, 255));

        Color ledCol = broken ? faultRed : okTeal;
        Color ledDim = broken ? new Color(46, 16, 16, 255) : new Color(18, 44, 44, 255);
        SevenSegDigit(0, 72, 110, 56, 104, 11, ledCol, ledDim);
        SevenSegDigit(1, 148, 110, 56, 104, 11, ledCol, ledDim);
        Raylib.ImageDrawRectangle(ref canvas, 224, 104, 2, 118, new Color(44, 52, 60, 255));

        if (broken)
        {
            Raylib.ImageDrawTriangle(ref canvas, new(246, 156), new(268, 116), new(290, 156), faultAmber);
            Raylib.ImageDrawRectangle(ref canvas, 266, 129, 5, 14, screenBg);
            Raylib.ImageDrawRectangle(ref canvas, 266, 146, 5, 5, screenBg);
            DrawText(Loc.T("ЗАПЕРТ", "LOCKED"), 302, 116, 26, faultRed);
            DrawText(Loc.T("СБ БЛОК", "SEC HOLD"), 302, 148, 22, faultAmber);
            DrawText(Loc.T("ЩИТОК СГОРЕЛ", "RELAY FAULT"), 246, 186, 20, new Color(224, 134, 108, 255));
        }
        else
        {
            Raylib.ImageDrawTriangle(ref canvas, new(248, 154), new(270, 116), new(292, 154), okTeal);
            DrawText(Loc.T("ДОСТУП", "ACCESS"), 306, 116, 26, okTeal);
            DrawText(Loc.T("ОТКРЫТ", "GRANTED"), 306, 148, 22, new Color(168, 246, 224, 255));
            DrawText(Loc.T("ЛИФТ ГОТОВ", "LIFT READY"), 246, 186, 20, brass);
        }

        Raylib.ImageDrawRectangle(ref canvas, 38, 262, w - 76, 2, steelLo);

        // Call Buttons Zone (UP at py=356, DOWN at py=493, button center px=191).
        void DrawCallSlot(int cy, string label, int braille)
        {
            Raylib.ImageDrawCircle(ref canvas, 191, cy, 62, new Color(16, 19, 24, 255));
            DrawRing(191, cy, 62, 3f, steelHi);
            DrawRing(191, cy, 55, 2.5f, broken ? new Color(142, 46, 38, 255) : new Color(56, 148, 138, 255));
            DrawText(label, 276, cy - 26, 26, textMuted);
            BrailleCell(282, cy + 8, braille);
            BrailleCell(318, cy + 8, braille ^ 0b011010);
        }
        DrawCallSlot(356, Loc.T("ВВЕРХ", "UP"), 0b101011);
        DrawCallSlot(493, Loc.T("ВНИЗ", "DOWN"), 0b110101);

        Raylib.ImageDrawRectangle(ref canvas, 38, 574, w - 76, 2, steelLo);

        // RFID Keycard Reader Pad (py 594..752).
        FrameRect(62, 594, w - 124, 158, new Color(76, 84, 94, 255), new Color(14, 17, 22, 255));
        CenterText(Loc.T("КЛЮЧ-КАРТА СБ", "SECURITY KEYCARD"), 616, 22, brass);
        FrameRect(126, 658, 96, 62, new Color(140, 156, 170, 255), new Color(26, 33, 42, 255));
        Raylib.ImageDrawRectangle(ref canvas, 138, 674, 22, 16, brass);
        Raylib.ImageDrawRectangle(ref canvas, 138, 700, 56, 6, new Color(92, 108, 122, 255));
        for (int r = 16; r <= 42; r += 13)
            DrawRing(248, 689, r, 3.5f, broken ? new Color(216, 168, 96, 255) : okTeal, rightArcOnly: true);
        DrawText("RFID", 320, 676, 28, textMuted);

        Raylib.ImageDrawRectangle(ref canvas, 38, 774, w - 76, 2, steelLo);

        // Lower Service / Wiring Relay Bay (py 792..986).
        CenterText(Loc.T("220V · ЩИТОК РЕЛЕ", "220V · RELAY BAY"), 788, 20, broken ? faultAmber : textMuted);
        if (broken)
        {
            for (int y = 808; y < 996; y++)
            {
                for (int x = 44; x < w - 44; x++)
                {
                    float dx = (x - 256f) / 195f, dy = (y - 898f) / 92f;
                    float r = MathF.Sqrt(dx * dx + dy * dy);
                    if (r < 1.05f)
                    {
                        float soot = Math.Clamp(1f - r, 0f, 1f) * .72f;
                        Color c = px[y * w + x];
                        px[y * w + x] = new Color(
                            (byte)(c.R * (1 - soot)),
                            (byte)(c.G * (1 - soot * 1.05f)),
                            (byte)(c.B * (1 - soot * 1.1f)), (byte)255);
                    }
                }
            }
            FrameRect(76, 824, w - 152, 146, new Color(82, 74, 68, 255), new Color(10, 11, 13, 255));
            FrameRect(102, 844, w - 204, 106, new Color(36, 68, 48, 255), new Color(18, 34, 24, 255));
            for (int tx = 122; tx < 390; tx += 28)
                Raylib.ImageDrawRectangle(ref canvas, tx, 856, 4, 82, new Color(46, 92, 62, 255));
        }
        else
        {
            FrameRect(76, 824, w - 152, 146, steelHi, new Color(42, 48, 56, 255));
            CenterText(Loc.T("ПИТАНИЕ В НОРМЕ", "POWER NOMINAL"), 882, 22, okTeal);
        }

        Raylib.ImageFlipVertical(ref canvas);
        img = canvas;
    }
}
