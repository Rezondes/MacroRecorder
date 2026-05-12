using System.ComponentModel;

namespace MacroRecorder.App.Localization;

/// <summary>
/// Bumped when UI culture changes so <see cref="LocExtension"/> bindings re-evaluate.
/// </summary>
public sealed class UiCulturePulse : INotifyPropertyChanged
{
    public static UiCulturePulse Instance { get; } = new();

    private int _tick;

    public int Tick
    {
        get => _tick;
        private set
        {
            if (value == _tick)
                return;
            _tick = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tick)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Bump() => Tick++;
}
