using System.Diagnostics;
using System.Text;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Interop;
using Microsoft.Extensions.Logging;

namespace MacroRecorder.Infrastructure.Playback;

internal readonly record struct PlaybackWindowMatchSpec(
    string ProcessName,
    string WindowTitle,
    int? ExpectedClientWidth,
    int? ExpectedClientHeight,
    int ReferenceClientWidthTolerance = 0,
    int ReferenceClientHeightTolerance = 0);

internal static class FocusWindowMatcher
{
    /// <summary>Legacy macros: <see cref="RecordingMetadata.MouseAnchor"/> is set and a mouse-like event appears
    /// before the earliest <see cref="FocusChangedRecordedEvent"/>; those early coordinates are client-relative to that anchor.</summary>
    public static bool RequiresLegacyMouseAnchor(Macro macro, IReadOnlyList<RecordedInputEvent> orderedBySequence)
    {
        if (macro.Metadata.MouseAnchor is null)
            return false;

        var focusSequences = orderedBySequence
            .OfType<FocusChangedRecordedEvent>()
            .Where(focusChanged => focusChanged.Hwnd is not null)
            .Select(focusChanged => focusChanged.Sequence)
            .ToList();
        if (focusSequences.Count == 0)
            return orderedBySequence.Any(IsMouseLikeEvent);

        var minFocusSequence = focusSequences.Min();
        return orderedBySequence.Any(
            recordedEvent => IsMouseLikeEvent(recordedEvent) && recordedEvent.Sequence < minFocusSequence);
    }

    private static bool IsMouseLikeEvent(RecordedInputEvent recordedEvent) =>
        recordedEvent is MouseMoveRecordedEvent
            or MouseButtonDownRecordedEvent
            or MouseButtonUpRecordedEvent
            or MouseWheelRecordedEvent;

    public static nint ResolveInitialFocusBoundHwnd(Macro macro, IReadOnlyList<RecordedInputEvent> orderedBySequence)
    {
        if (RequiresLegacyMouseAnchor(macro, orderedBySequence))
            return ResolveForPlayback(macro.Metadata.MouseAnchor!);

        return nint.Zero;
    }

    public static nint ResolveForPlayback(MousePlaybackAnchor anchor, ILogger? logger = null) =>
        Resolve(new PlaybackWindowMatchSpec(
            anchor.ProcessName,
            anchor.WindowTitle,
            anchor.ReferenceClientWidth,
            anchor.ReferenceClientHeight),
            logger);

    public static nint ResolveForPlayback(FocusChangedRecordedEvent focusChanged, ILogger? logger = null) =>
        Resolve(new PlaybackWindowMatchSpec(
            focusChanged.ProcessName,
            focusChanged.WindowTitle,
            focusChanged.ReferenceClientWidth,
            focusChanged.ReferenceClientHeight,
            focusChanged.ReferenceClientWidthTolerance,
            focusChanged.ReferenceClientHeightTolerance),
            logger);

    /// <summary>Throws <see cref="PlaybackFocusTargetException"/> on first resolution failure.</summary>
    public static void ValidateFocusBoundTimeline(Macro macro, IReadOnlyList<RecordedInputEvent> orderedBySequence)
    {
        if (!macro.Metadata.UseFocusBoundMouseCoordinates)
            return;

        if (RequiresLegacyMouseAnchor(macro, orderedBySequence))
        {
            if (macro.Metadata.MouseAnchor is null)
                throw new PlaybackFocusTargetException(PlaybackFocusTargetKind.AnchorMissing);

            _ = ResolveForPlayback(macro.Metadata.MouseAnchor!);
        }

        foreach (var recordedEvent in orderedBySequence)
        {
            if (recordedEvent is FocusChangedRecordedEvent { Hwnd: not null } focusChanged)
                _ = ResolveForPlayback(focusChanged);
        }
    }

    private static nint Resolve(PlaybackWindowMatchSpec spec, ILogger? logger = null)
    {
        nint found = nint.Zero;
        var expectedTitle = spec.WindowTitle;
        var expectedProcess = spec.ProcessName;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
                return true;
            if (NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT) != hwnd)
                return true;

            var titleBuilder = new StringBuilder(512);
            _ = NativeMethods.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
            var title = titleBuilder.ToString();
            if (!string.Equals(title, expectedTitle, StringComparison.OrdinalIgnoreCase))
                return true;

            NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
            string processName;
            try
            {
                using var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch (Exception exception)
            {
                logger?.LogWarning(exception, "Failed to resolve process for focus window candidate");
                return true;
            }

            if (!string.Equals(processName, expectedProcess, StringComparison.OrdinalIgnoreCase))
                return true;

            found = hwnd;
            return false;
        }, nint.Zero);

        if (found == nint.Zero)
        {
            throw new PlaybackFocusTargetException(
                PlaybackFocusTargetKind.WindowNotFound,
                spec.ProcessName,
                spec.WindowTitle);
        }

        if (!NativeMethods.GetClientRect(found, out var rect))
        {
            if (spec.ExpectedClientWidth is not int expectedW || spec.ExpectedClientHeight is not int expectedH)
                return found;

            throw new PlaybackFocusTargetException(
                PlaybackFocusTargetKind.ClientSizeMismatch,
                spec.ProcessName,
                spec.WindowTitle,
                expectedW,
                expectedH,
                null,
                null);
        }

        if (spec.ExpectedClientWidth is int ew && spec.ExpectedClientHeight is int eh)
        {
            var w = rect.Width;
            var h = rect.Height;
            var tolW = Math.Max(0, spec.ReferenceClientWidthTolerance);
            var tolH = Math.Max(0, spec.ReferenceClientHeightTolerance);
            if (Math.Abs(w - ew) > tolW || Math.Abs(h - eh) > tolH)
            {
                throw new PlaybackFocusTargetException(
                    PlaybackFocusTargetKind.ClientSizeMismatch,
                    spec.ProcessName,
                    spec.WindowTitle,
                    ew,
                    eh,
                    w,
                    h);
            }
        }

        return found;
    }
}
