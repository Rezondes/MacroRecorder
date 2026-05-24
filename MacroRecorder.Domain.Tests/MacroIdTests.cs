namespace MacroRecorder.Domain.Tests;

public sealed class MacroIdTests
{
    [Fact]
    public void New_returns_parseable_ulid_string()
    {
        var id = MacroId.New();

        Assert.False(string.IsNullOrWhiteSpace(id.Value));
        Assert.Equal(id, MacroId.Parse(id.Value));
    }

    [Fact]
    public void Parse_accepts_canonical_ulid()
    {
        var ulid = System.Ulid.NewUlid().ToString();
        var id = MacroId.Parse(ulid);

        Assert.Equal(ulid, id.Value);
    }

    [Fact]
    public void Parse_accepts_guid_with_dashes()
    {
        var guid = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var id = MacroId.Parse(guid.ToString("D"));

        Assert.Equal(guid.ToString("D"), id.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-id")]
    public void Parse_rejects_invalid_values(string value)
    {
        Assert.ThrowsAny<Exception>(() => MacroId.Parse(value));
    }

    [Fact]
    public void ToFileStem_for_guid_uses_32_hex_without_dashes()
    {
        var guid = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var id = MacroId.Parse(guid.ToString("D"));

        Assert.Equal("a1b2c3d4e5f67890abcdef1234567890", id.ToFileStem());
    }

    [Fact]
    public void ToFileStem_for_ulid_keeps_canonical_string()
    {
        var ulid = System.Ulid.NewUlid().ToString();
        var id = MacroId.Parse(ulid);

        Assert.Equal(ulid, id.ToFileStem());
    }

    [Fact]
    public void ToString_returns_value()
    {
        var id = MacroId.New();

        Assert.Equal(id.Value, id.ToString());
    }
}
