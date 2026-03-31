using System.Reflection;

namespace ClipVault.Helpers;

public static class AppVersionHelper
{
    public static string GetDisplayVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

        var cleanedInformational = CleanVersion(informationalVersion);
        if (!string.IsNullOrWhiteSpace(cleanedInformational))
        {
            return cleanedInformational;
        }

        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is not null)
        {
            // Show Major.Minor.Build instead of Major.Minor.Build.Revision
            return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(assemblyVersion.Build, 0)}";
        }

        return "1.0.0";
    }

    private static string CleanVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Removes git/hash suffixes like 1.0.0+abc123
        var plusIndex = value.IndexOf('+');
        if (plusIndex >= 0)
        {
            value = value[..plusIndex];
        }

        return value.Trim();
    }
}