namespace OffStreamXIV.Tests;

public class WindowKeyTests
{
    [Theory]
    [InlineData("Settings", "Settings")]
    [InlineData("Settings##SimpleHUD", "Settings##SimpleHUD")]
    [InlineData("Réglages 12%###config", "###config")]
    [InlineData("a###b###c", "###c")]
    [InlineData("####x", "###x")]
    public void KeyIsWhatImGuiUsesAsIdentifier(string name, string key) => Assert.Equal(key, WindowKey.From(name));

    [Fact]
    public void TitleChangesKeepTheSameKey() =>
        Assert.Equal(WindowKey.From("Loot 3 items###LootWindow"), WindowKey.From("Loot 12 items###LootWindow"));

    [Theory]
    [InlineData("Settings", "Settings")]
    [InlineData("Settings##SimpleHUD", "Settings")]
    [InlineData("OffStreamXIV###OffStreamXIVMain", "OffStreamXIV")]
    [InlineData("##Overlay", "##Overlay")]
    [InlineData("###Overlay", "###Overlay")]
    public void LabelIsTheVisibleTitle(string name, string label) => Assert.Equal(label, WindowKey.Label(name));
}
