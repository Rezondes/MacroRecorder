namespace MacroRecorder.Application.Ports;

/// <summary>Which flow opened the unsaved-changes modal (labels and chrome).</summary>
public enum UnsavedChangesPromptContext
{
    Editor,

    /// <summary>General + Visuals settings (language and/or appearance).</summary>
    Settings,
}
