namespace MacroRecorder.Domain;

/// <summary>Macro identity: new macros use a ULID string; legacy files may still use a GUID string.</summary>
public readonly record struct MacroId(string Value)
{
    public static MacroId New() => new(System.Ulid.NewUlid().ToString());

    public static MacroId Parse(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            throw new ArgumentException("Macro id is required.", nameof(s));
        s = s.Trim();
        if (System.Ulid.TryParse(s, null, out _))
            return new MacroId(s);
        if (Guid.TryParse(s, out _))
            return new MacroId(s);
        throw new FormatException($"Invalid macro id (expected ULID or GUID string): {s}");
    }

    /// <summary>File stem under the macro repository (no extension): GUID → 32 hex without dashes; ULID → canonical string.</summary>
    public string ToFileStem() =>
        Guid.TryParse(Value, out var guid) ? guid.ToString("N") : Value;

    public override string ToString() => Value;
}
