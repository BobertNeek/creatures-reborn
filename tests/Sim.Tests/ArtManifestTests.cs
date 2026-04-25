using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace CreaturesReborn.Sim.Tests;

public class ArtManifestTests
{
    private static readonly string ManifestPath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "art", "manifest.json");

    [Fact]
    public void ArtManifest_TracksRequiredRebootAssetRecords()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(ManifestPath)));
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("schemaVersion", out _));
        Assert.True(root.TryGetProperty("artDirection", out _));
        Assert.True(root.TryGetProperty("runtimeShape", out _));
        JsonElement assets = root.GetProperty("assets");
        Assert.True(assets.GetArrayLength() >= 5);

        string[] requiredIds =
        {
            "treehouse-metaroom-v0",
            "norn-texture-set-v0",
            "egg-sprite-v1",
            "food-fruit-v1",
            "ui-icons-v1",
        };

        string[] foundIds = assets.EnumerateArray()
            .Select(asset => asset.GetProperty("id").GetString() ?? "")
            .ToArray();

        foreach (string requiredId in requiredIds)
            Assert.Contains(requiredId, foundIds);

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            AssertRequiredString(asset, "id");
            AssertRequiredString(asset, "category");
            AssertRequiredString(asset, "runtimePath");
            AssertRequiredString(asset, "replacementTarget");
            AssertRequiredString(asset, "sourceNotes");
            AssertRequiredString(asset, "promptRef");
            AssertRequiredString(asset, "status");

            string promptRef = asset.GetProperty("promptRef").GetString()!;
            string promptPath = Path.Combine(
                Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ManifestPath)!, "..")),
                promptRef.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(promptPath), $"Missing prompt record: {promptRef}");
        }
    }

    private static void AssertRequiredString(JsonElement asset, string propertyName)
    {
        Assert.True(asset.TryGetProperty(propertyName, out JsonElement value), $"Missing {propertyName}");
        Assert.False(string.IsNullOrWhiteSpace(value.GetString()), $"{propertyName} is empty");
    }
}
