namespace MacroRecorder.Application.Ports;

/// <summary>Which flow opened the unsaved-changes modal (labels and chrome).</summary>
public enum UnsavedChangesPromptContext
{
    Editor,

    /// <summary>Appearance preview on the Visuals settings tab.</summary>
    Appearance,
}
