namespace USS.Desktop.Application;

public sealed record AppThemePalette(
    string PageBackground,
    string ChromeBackground,
    string CardBackground,
    string BorderColor,
    string TextColor,
    string MutedTextColor,
    string PrimaryColor,
    string AccentColor,
    string SuccessColor,
    string WarningColor,
    string DangerColor,
    string LogBackground)
{
    public static AppThemePalette LightDefault { get; } = new(
        "#F2ECE2",
        "#233647",
        "#FFFDF9",
        "#DCCFBC",
        "#18212B",
        "#5B6672",
        "#C5642F",
        "#2B5D7B",
        "#55735A",
        "#B87817",
        "#BE3D2F",
        "#F7F2E9");

    public static AppThemePalette DarkDefault { get; } = new(
        "#1A2027",
        "#22303B",
        "#28323C",
        "#43515D",
        "#F4F1EA",
        "#ACB7C2",
        "#D68856",
        "#73A9C7",
        "#78A884",
        "#D0A15A",
        "#D96E62",
        "#171E25");
}
