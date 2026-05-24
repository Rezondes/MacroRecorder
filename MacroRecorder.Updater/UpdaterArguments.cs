namespace MacroRecorder.Updater;

internal sealed class UpdaterArguments
{
    public required int WaitPid { get; init; }
    public required string ZipUrl { get; init; }
    public required string InstallDirectory { get; init; }
    public required string MainExe { get; init; }
    public required string UpdaterExe { get; init; }

    public static bool TryParse(string[] args, out UpdaterArguments? arguments, out string? error)
    {
        arguments = null;
        error = null;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown argument: {argument}";
                return false;
            }

            var optionBody = argument[2..];
            if (string.IsNullOrWhiteSpace(optionBody))
            {
                error = $"Invalid option: {argument}";
                return false;
            }

            string optionName;
            string? optionValue;
            var equalsIndex = optionBody.IndexOf('=');
            if (equalsIndex >= 0)
            {
                optionName = optionBody[..equalsIndex];
                optionValue = optionBody[(equalsIndex + 1)..];
            }
            else
            {
                optionName = optionBody;
                if (index + 1 >= args.Length)
                {
                    error = $"Missing value for --{optionName}.";
                    return false;
                }

                optionValue = args[++index];
            }

            if (string.IsNullOrWhiteSpace(optionName))
            {
                error = $"Invalid option: {argument}";
                return false;
            }

            values[optionName] = optionValue;
        }

        if (!values.TryGetValue("wait-pid", out var waitPidValue)
            || !int.TryParse(waitPidValue, out var waitPid)
            || waitPid <= 0)
        {
            error = "Missing or invalid --wait-pid.";
            return false;
        }

        if (!values.TryGetValue("zip-url", out var zipUrl) || string.IsNullOrWhiteSpace(zipUrl))
        {
            error = "Missing --zip-url.";
            return false;
        }

        if (!values.TryGetValue("install-dir", out var installDirectory)
            || string.IsNullOrWhiteSpace(installDirectory)
            || !Directory.Exists(installDirectory))
        {
            error = "Missing or invalid --install-dir.";
            return false;
        }

        if (!values.TryGetValue("main-exe", out var mainExe) || string.IsNullOrWhiteSpace(mainExe))
        {
            error = "Missing --main-exe.";
            return false;
        }

        if (!values.TryGetValue("updater-exe", out var updaterExe) || string.IsNullOrWhiteSpace(updaterExe))
        {
            error = "Missing --updater-exe.";
            return false;
        }

        arguments = new UpdaterArguments
        {
            WaitPid = waitPid,
            ZipUrl = zipUrl,
            InstallDirectory = Path.GetFullPath(installDirectory),
            MainExe = mainExe,
            UpdaterExe = updaterExe
        };
        return true;
    }
}
