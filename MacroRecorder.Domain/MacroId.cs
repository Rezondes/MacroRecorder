namespace MacroRecorder.Domain;

public readonly record struct MacroId(Guid Value)
{
    public static MacroId New() => new(Guid.NewGuid());

    public static MacroId Parse(string s) => new(Guid.Parse(s));

    public override string ToString() => Value.ToString("D");
}
