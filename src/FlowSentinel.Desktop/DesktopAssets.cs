namespace FlowSentinel.Desktop;

internal static class DesktopAssets
{
    private const string DeveloperLogoFileName = "WWSoftwaresDeveloperLogo.png";

    internal static Image? LoadDeveloperLogo()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", DeveloperLogoFileName),
            Path.Combine(AppContext.BaseDirectory, DeveloperLogoFileName)
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
