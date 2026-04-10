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
        "#13171C",
        "#1C2833",
        "#1E242C",
        "#3B4653",
        "#F3F5F8",
        "#A8B3BF",
        "#D47A46",
        "#5E98BE",
        "#6FB182",
        "#D0A85E",
        "#DB675A",
        "#141A20");
}
