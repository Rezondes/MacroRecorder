namespace MacroRecorder.App.Services;

/// <summary>
/// Marker for a content modal where the window must not treat Escape or Windows keys as shell shortcuts, so those
/// keys can be captured for editing (e.g. insert key stroke). Close only via in-modal Cancel.
/// </summary>
public interface IContentModalKeyCaptureHost;
