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

        int? waitPid = null;
        string? zipUrl = null;
        string? installDirectory = null;
        string? mainExe = null;
        string? updaterExe = null;

        for (var index = 0; index < args.Length; index++)
        {
            if (TryReadOptionValue(args, ref index, "--wait-pid", out var waitPidValue))
            {
                if (waitPidValue is not null && int.TryParse(waitPidValue, out var parsedPid))
                    waitPid = parsedPid;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--zip-url", out zipUrl))
                continue;
            if (TryReadOptionValue(args, ref index, "--install-dir", out installDirectory))
                continue;
            if (TryReadOptionValue(args, ref index, "--main-exe", out mainExe))
                continue;
            if (TryReadOptionValue(args, ref index, "--updater-exe", out updaterExe))
                continue;

            error = $"Unknown argument: {args[index]}";
            return false;
        }

        if (waitPid is null or <= 0)
        {
            error = "Missing or invalid --wait-pid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(zipUrl))
        {
            error = "Missing --zip-url.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
        {
            error = "Missing or invalid --install-dir.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(mainExe))
        {
            error = "Missing --main-exe.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(updaterExe))
        {
            error = "Missing --updater-exe.";
            return false;
        }

        arguments = new UpdaterArguments
        {
            WaitPid = waitPid.Value,
            ZipUrl = zipUrl,
            InstallDirectory = Path.GetFullPath(installDirectory),
            MainExe = mainExe,
            UpdaterExe = updaterExe
        };
        return true;
    }

    private static bool TryReadOptionValue(
        string[] args,
        ref int index,
        string optionName,
        out string? value)
    {
        value = null;
        if (!string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (index + 1 >= args.Length)
            return true;

        value = args[++index];
        return true;
    }
}
