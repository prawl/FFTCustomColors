using System;
using System.Drawing;

namespace FFTColorCustomizer.ThemeEditor
{
    public struct HslColor
    {
        public double H { get; set; }  // Hue: 0-360
        public double S { get; set; }  // Saturation: 0-1
        public double L { get; set; }  // Lightness: 0-1

        public HslColor(double h, double s, double l)
        {
            H = h;
            S = s;
            L = l;
        }

        public static HslColor FromRgb(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            double s = 0;
            double l = (max + min) / 2.0;

            if (delta > 0)
            {
                s = l < 0.5 ? delta / (max + min) : delta / (2.0 - max - min);

                if (max == r)
                    h = ((g - b) / delta) + (g < b ? 6 : 0);
                else if (max == g)
                    h = ((b - r) / delta) + 2;
                else
                    h = ((r - g) / delta) + 4;

                h *= 60;
            }

            return new HslColor(h, s, l);
        }

        public Color ToRgb()
        {
            if (S == 0)
            {
                int gray = (int)(L * 255);
                return Color.FromArgb(gray, gray, gray);
            }

            double q = L < 0.5 ? L * (1 + S) : L + S - L * S;
            double p = 2 * L - q;
            double hNorm = H / 360.0;

            double r = HueToRgb(p, q, hNorm + 1.0 / 3.0);
            double g = HueToRgb(p, q, hNorm);
            double b = HueToRgb(p, q, hNorm - 1.0 / 3.0);

            return Color.FromArgb(
                (int)Math.Round(r * 255),
                (int)Math.Round(g * 255),
                (int)Math.Round(b * 255)
            );
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }

        public static ColorShades GenerateShades(Color baseColor)
        {
            var hsl = FromRgb(baseColor);

            // Shadow: darker, slightly more saturated
            var shadow = new HslColor(hsl.H, Math.Min(hsl.S * 1.1, 1.0), hsl.L * 0.65);

            // Highlight: lighter, slightly less saturated
            var highlight = new HslColor(hsl.H, hsl.S * 0.85, Math.Min(hsl.L * 1.35, 0.95));

            // Accent: lighter detail color for trim/decorative elements (like boot/arm stripes)
            var accent = new HslColor(hsl.H, hsl.S * 0.7, Math.Min(hsl.L * 1.5, 0.90));

            // AccentShadow: slightly darker version of accent for depth
            var accentShadow = new HslColor(hsl.H, hsl.S * 0.8, Math.Min(hsl.L * 1.25, 0.80));

            return new ColorShades(shadow.ToRgb(), baseColor, highlight.ToRgb(), accent.ToRgb(), accentShadow.ToRgb());
        }
    }

    public readonly struct ColorShades
    {
        public Color Shadow { get; }
        public Color Base { get; }
        public Color Highlight { get; }
        public Color Accent { get; }
        public Color AccentShadow { get; }

        public ColorShades(Color shadow, Color baseColor, Color highlight, Color accent, Color accentShadow)
        {
            Shadow = shadow;
            Base = baseColor;
            Highlight = highlight;
            Accent = accent;
            AccentShadow = accentShadow;
        }
    }
}
