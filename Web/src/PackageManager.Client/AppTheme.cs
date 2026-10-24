using MudBlazor;

namespace PackageManager.Client;

/// <summary>MudTheme from the "Theme" design board. Source colors (unitypackage / UPM) live in app.css.</summary>
public static class AppTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#8B7CF6", Secondary = "#2DD4BF",
            Background = "#0F1115", Surface = "#171A21",
            AppbarBackground = "#14171D", DrawerBackground = "#14171D",
            TextPrimary = "#E7E9EE", TextSecondary = "#A3A9B6",
            LinesDefault = "#2A2F3A", Error = "#F87171",
            Warning = "#FACC15", Success = "#34D399", Info = "#38BDF8",
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#5B4BDB", Secondary = "#0F766E",
            Background = "#F6F7F9", Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF", TextPrimary = "#161A22",
            TextSecondary = "#4B5363", LinesDefault = "#E3E6EC",
            Error = "#DC2626", Warning = "#A16207",
            Success = "#15803D", Info = "#0369A1",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Plus Jakarta Sans", "system-ui", "sans-serif"],
            },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "10px" },
    };
}
