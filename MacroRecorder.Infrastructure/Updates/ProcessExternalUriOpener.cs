using System.Diagnostics;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.Infrastructure.Updates;

public sealed class ProcessExternalUriOpener : IExternalUriOpener
{
    public void Open(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
