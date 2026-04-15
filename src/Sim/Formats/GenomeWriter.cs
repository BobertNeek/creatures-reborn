using System;
using System.IO;
using CreaturesReborn.Sim.Genome;
using G = CreaturesReborn.Sim.Genome.Genome;

namespace CreaturesReborn.Sim.Formats;

/// <summary>
/// Serializes a <see cref="G"/> back to the DNA3 binary <c>.gen</c> format so offspring
/// can be saved to disk and round-tripped through <see cref="GenomeReader"/>.
/// </summary>
/// <remarks>
/// Output layout (mirrors c2e Genome::WriteToFile):
/// <code>
/// [4 bytes]  DNA3TOKEN magic ('d','n','a','3')
/// [N bytes]  raw gene bytes from <see cref="G.AsSpan"/>
/// </code>
/// </remarks>
public static class GenomeWriter
{
    /// <summary>
    /// Write <paramref name="genome"/> to a DNA3 <c>.gen</c> file at <paramref name="path"/>.
    /// </summary>
    /// <exception cref="GenomeException">Genome has no bytes to write.</exception>
    /// <exception cref="IOException">Path is not writable.</exception>
    public static void SaveToFile(G genome, string path)
    {
        byte[] data = Serialize(genome);
        File.WriteAllBytes(path, data);
    }

    /// <summary>
    /// Serialize <paramref name="genome"/> to a DNA3 byte array (header + gene data) without
    /// writing to disk. Useful for in-memory round-trip tests.
    /// </summary>
    public static byte[] Serialize(G genome)
    {
        ReadOnlySpan<byte> geneBytes = genome.AsSpan();
        if (geneBytes.IsEmpty)
            throw new GenomeException("Cannot serialize an empty genome.");

        byte[] result = new byte[4 + geneBytes.Length];

        // Write magic header as little-endian 4-byte int.
        int token = GeneConstants.DNA3TOKEN;
        result[0] = (byte)(token & 0xFF);
        result[1] = (byte)((token >> 8) & 0xFF);
        result[2] = (byte)((token >> 16) & 0xFF);
        result[3] = (byte)((token >> 24) & 0xFF);

        geneBytes.CopyTo(result.AsSpan(4));
        return result;
    }
}
