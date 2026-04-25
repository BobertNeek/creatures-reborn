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
            "treehouse-metaroom-v1",
            "norn-texture-set-v1",
            "egg-sprite-v1",
            "food-fruit-v1",
            "food-seed-v1",
            "food-food-v1",
            "norn-procedural-model-v1",
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

    [Fact]
    public void ArtManifest_ActiveAssetsHaveRuntimeFilesAndExpectedPngDimensions()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(ManifestPath)));
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ManifestPath)!, ".."));

        foreach (JsonElement asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            string status = asset.GetProperty("status").GetString() ?? "";
            if (status != "active") continue;

            string id = asset.GetProperty("id").GetString() ?? "";
            string runtimePath = asset.GetProperty("runtimePath").GetString() ?? "";
            string localPath = ResolveRuntimePath(repoRoot, runtimePath);

            if (runtimePath.EndsWith("/", StringComparison.Ordinal))
            {
                Assert.True(Directory.Exists(localPath), $"Active asset directory missing: {id} -> {runtimePath}");
                Assert.NotEmpty(Directory.EnumerateFiles(localPath));
            }
            else
            {
                Assert.True(File.Exists(localPath), $"Active asset file missing: {id} -> {runtimePath}");
            }

            if (asset.TryGetProperty("expectedWidth", out JsonElement widthElement)
                && asset.TryGetProperty("expectedHeight", out JsonElement heightElement))
            {
                var (width, height) = ReadPngSize(localPath);
                Assert.Equal(widthElement.GetInt32(), width);
                Assert.Equal(heightElement.GetInt32(), height);
            }
        }
    }

    private static string ResolveRuntimePath(string repoRoot, string runtimePath)
    {
        Assert.StartsWith("res://", runtimePath);
        string relative = runtimePath["res://".Length..]
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
        return Path.Combine(repoRoot, relative);
    }

    private static (int width, int height) ReadPngSize(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[24];
        int read = stream.Read(header);
        Assert.True(read >= 24, $"PNG header too short: {path}");
        Assert.Equal((byte)0x89, header[0]);
        Assert.Equal((byte)'P', header[1]);
        Assert.Equal((byte)'N', header[2]);
        Assert.Equal((byte)'G', header[3]);

        int width = ReadBigEndianInt32(header[16..20]);
        int height = ReadBigEndianInt32(header[20..24]);
        return (width, height);
    }

    private static int ReadBigEndianInt32(ReadOnlySpan<byte> bytes)
        => (bytes[0] << 24)
         | (bytes[1] << 16)
         | (bytes[2] << 8)
         | bytes[3];

    private static void AssertRequiredString(JsonElement asset, string propertyName)
    {
        Assert.True(asset.TryGetProperty(propertyName, out JsonElement value), $"Missing {propertyName}");
        Assert.False(string.IsNullOrWhiteSpace(value.GetString()), $"{propertyName} is empty");
    }
}
