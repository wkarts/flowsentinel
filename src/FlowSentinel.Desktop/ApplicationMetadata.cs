using System.Reflection;

namespace FlowSentinel.Desktop;

internal static class ApplicationMetadata
{
    internal const string DeveloperCompany = "WWSoftware's Sistemas e Tecnologias";
    internal const string DeveloperName = "Wallace Kleiton";
    internal const string GitHubUser = "@wkarts";
    internal const string GitHubUrl = "https://github.com/wkarts";
    internal const string WhatsAppDisplay = "+55 75 98844-9231";
    internal const string WhatsAppUrl = "https://wa.me/5575988449231";
    internal const string Email = "wkarts@gmail.com";

    internal static string Version
    {
        get
        {
            var assembly = typeof(ApplicationMetadata).Assembly;
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                return informational.Split('+')[0];
            }

            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }
}
