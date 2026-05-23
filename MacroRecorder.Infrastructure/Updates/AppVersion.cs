using System.Reflection;

namespace MacroRecorder.Infrastructure.Updates;

internal static class AppVersion
{
    public static string Current =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? "0.0.0";
}
