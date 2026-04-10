using System.Globalization;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;

namespace USS.Desktop.App.Services;

public static class ThemeColorHelper
{
    public static MediaColor ParseHex(string hex) =>
        (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(NormalizeHex(hex));

    public static string NormalizeHex(string hex)
    {
        var trimmed = hex.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length != 6 && trimmed.Length != 8)
        {
            throw new FormatException("Color value must be in #RRGGBB or #AARRGGBB format.");
        }

        return $"#{trimmed.ToUpperInvariant()}";
    }

    public static string ToHex(MediaColor color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static MediaColor Blend(MediaColor first, MediaColor second, double ratio)
    {
        var clamped = Math.Clamp(ratio, 0d, 1d);
        return MediaColor.FromRgb(
            (byte)Math.Round((first.R * (1d - clamped)) + (second.R * clamped), MidpointRounding.AwayFromZero),
            (byte)Math.Round((first.G * (1d - clamped)) + (second.G * clamped), MidpointRounding.AwayFromZero),
            (byte)Math.Round((first.B * (1d - clamped)) + (second.B * clamped), MidpointRounding.AwayFromZero));
    }

    public static MediaColor ShiftBrightness(MediaColor color, double delta)
    {
        var factor = delta >= 0d ? 1d - delta : 1d + delta;
        return MediaColor.FromRgb(
            Adjust(color.R, delta, factor),
            Adjust(color.G, delta, factor),
            Adjust(color.B, delta, factor));
    }

    public static MediaColor WithOpacity(MediaColor color, double opacity) =>
        MediaColor.FromArgb(
            (byte)Math.Round(Math.Clamp(opacity, 0d, 1d) * byte.MaxValue, MidpointRounding.AwayFromZero),
            color.R,
            color.G,
            color.B);

    public static MediaColor GetReadableForeground(MediaColor background)
    {
        var luminance = ((0.299d * background.R) + (0.587d * background.G) + (0.114d * background.B)) / 255d;
        return luminance >= 0.62d ? MediaColors.Black : MediaColors.White;
    }

    private static byte Adjust(byte component, double delta, double factor)
    {
        var value = delta >= 0d
            ? component + ((255 - component) * delta)
            : component * factor;

        return byte.Parse(Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0d, 255d).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
