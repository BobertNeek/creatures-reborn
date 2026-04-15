using System;
using System.IO;
using System.Text;
using CreaturesReborn.Sim.Genome;
using CreaturesReborn.Sim.Util;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Formats;

/// <summary>
/// Reads a C3/DS binary <c>.gen</c> file in the DNA3 format and produces a populated
/// <see cref="G"/> object.
/// </summary>
/// <remarks>
/// DNA3 file layout (mirrors c2e Genome::ReadFromFile in Genome.cpp:158-212):
/// <code>
/// [4 bytes]  DNA3TOKEN magic ('d','n','a','3' = 0x33616E64 little-endian)
/// [N bytes]  raw gene data  (sequence of 'gene' ... 'gend' blocks)
/// </code>
/// The 4-byte header is consumed and discarded here; <see cref="G.AttachBytes"/> receives
/// only the gene data, matching the c2e convention where <c>myGenes</c> never contains the
/// file-level header.
/// </remarks>
public static class GenomeReader
{
    /// <summary>
    /// Load a <c>.gen</c> file from disk and attach it to <paramref name="genome"/>.
    /// </summary>
    /// <param name="genome">Genome to populate (must be freshly constructed or re-usable).</param>
    /// <param name="path">Absolute path to the <c>.gen</c> file.</param>
    /// <param name="sex">Sex to express: 1 = male, 2 = female.</param>
    /// <param name="age">Creature age byte (0–255).</param>
    /// <param name="variant">Behaviour variant (0 = all).</param>
    /// <param name="moniker">Unique creature identifier string.</param>
    /// <exception cref="GenomeException">File is missing, too short, or has a bad magic header.</exception>
    public static void LoadFromFile(
        G genome,
        string path,
        int sex     = GeneConstants.MALE,
        byte age    = 0,
        int variant = 0,
        string moniker = "")
    {
        byte[] fileBytes;
        try
        {
            fileBytes = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            throw new GenomeException($"Cannot read genome file '{path}': {ex.Message}");
        }

        Load(genome, fileBytes, sex, age, variant, moniker);
    }

    /// <summary>
    /// Parse a DNA3 genome from an in-memory byte array and attach it to <paramref name="genome"/>.
    /// </summary>
    public static void Load(
        G genome,
        byte[] fileBytes,
        int sex     = GeneConstants.MALE,
        byte age    = 0,
        int variant = 0,
        string moniker = "")
    {
        if (fileBytes.Length < 4)
            throw new GenomeException($"Genome data too short ({fileBytes.Length} bytes); minimum 4 required.");

        // Validate DNA3 magic — stored as little-endian int, same as GeneConstants.DNA3TOKEN.
        int magic = fileBytes[0] | (fileBytes[1] << 8) | (fileBytes[2] << 16) | (fileBytes[3] << 24);
        if (magic != GeneConstants.DNA3TOKEN)
        {
            string got = Encoding.ASCII.GetString(fileBytes, 0, Math.Min(4, fileBytes.Length));
            throw new GenomeException(
                $"Bad genome header: expected 'dna3' (0x{GeneConstants.DNA3TOKEN:X8}), " +
                $"got '{got}' (0x{magic:X8}). File may be pre-C3 'dna2' format or corrupt.");
        }

        // Strip the 4-byte magic; the remainder is the raw gene data.
        byte[] geneBytes = new byte[fileBytes.Length - 4];
        Buffer.BlockCopy(fileBytes, 4, geneBytes, 0, geneBytes.Length);

        genome.AttachBytes(geneBytes, sex, age, variant, moniker);
    }

    /// <summary>
    /// Convenience overload: construct a new genome from file in one call.
    /// </summary>
    public static G LoadNew(
        IRng rng,
        string path,
        int sex     = GeneConstants.MALE,
        byte age    = 0,
        int variant = 0,
        string moniker = "")
    {
        var g = new G(rng);
        LoadFromFile(g, path, sex, age, variant, moniker);
        return g;
    }
}
