namespace FlowSentinel.Desktop;

internal static class DesktopAssets
{
    private const string DeveloperLogoFileName = "WWSoftwaresDeveloperLogo.png";
    private const string DeveloperLogoWhiteFileName = "WWSoftwaresDeveloperLogoWhite.png";

    internal static Image? LoadDeveloperLogo() => LoadImage(DeveloperLogoFileName);

    internal static Image? LoadDeveloperLogoForDarkBackground() =>
        LoadImage(DeveloperLogoWhiteFileName) ?? LoadDeveloperLogo();

    private static Image? LoadImage(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName)
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }

        return null;
    }
}
