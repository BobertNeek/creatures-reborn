using System;
using System.IO;
using Godot;

namespace CreaturesReborn.Godot.Import;

/// <summary>
/// Decodes Creatures 3 / Docking Station .c16 sprite files into Godot Image arrays.
///
/// Format reference: DSE-HS-Appendix-A-C16.md
///
/// File layout:
///   uint32 flags         — bit 0: pixel format (0=RGB555, 1=RGB565); bit 1: must be 1
///   uint16 sprite_count
///   For each sprite:
///     uint32 line0_offset
///     uint16 width, height
///     uint32 line_offsets[height-1]   — absolute file offsets for lines 1..h-1
///   Image data: per-line RLE tags (uint16), colour runs, transparent runs, terminated by 0x0000
/// </summary>
public static class C16Decoder
{
    /// <summary>
    /// Decodes all sprite frames from a .c16 file byte array.
    /// Returns an array of RGBA8 Godot Images, one per sprite frame.
    /// Returns empty array on corrupt/unrecognised data.
    /// </summary>
    public static Image[] Decode(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length < 6)
            return Array.Empty<Image>();

        try
        {
            using var ms = new MemoryStream(fileBytes);
            using var br = new BinaryReader(ms);

            uint   flags       = br.ReadUInt32();
            bool   isRgb565    = (flags & 1) != 0;
            ushort spriteCount = br.ReadUInt16();

            if (spriteCount == 0) return Array.Empty<Image>();

            // ── Read sprite headers ────────────────────────────────────────────
            var headers = new SpriteHeader[spriteCount];
            for (int i = 0; i < spriteCount; i++)
            {
                uint   line0 = br.ReadUInt32();
                ushort w     = br.ReadUInt16();
                ushort h     = br.ReadUInt16();

                var lineOffsets = new uint[h];
                lineOffsets[0]  = line0;
                for (int y = 1; y < h; y++)
                    lineOffsets[y] = br.ReadUInt32();

                headers[i] = new SpriteHeader(w, h, lineOffsets);
            }

            // ── Decode image data ──────────────────────────────────────────────
            var images = new Image[spriteCount];
            for (int i = 0; i < spriteCount; i++)
            {
                int w = headers[i].Width;
                int h = headers[i].Height;

                var rgba = new byte[w * h * 4]; // pre-zeroed = all transparent

                for (int y = 0; y < h; y++)
                {
                    ms.Seek(headers[i].LineOffsets[y], SeekOrigin.Begin);
                    int x = 0;

                    while (true)
                    {
                        ushort tag = br.ReadUInt16();
                        if (tag == 0) break;           // end-of-line marker

                        int  type   = tag & 1;         // 0 = transparent, 1 = colour
                        int  runLen = tag >> 1;

                        if (type == 0)
                        {
                            // transparent run — pixels remain 0000 (alpha=0)
                            x += runLen;
                        }
                        else
                        {
                            // colour run — followed by runLen uint16 pixel words
                            for (int k = 0; k < runLen; k++)
                            {
                                ushort pixel = br.ReadUInt16();
                                int    idx   = (y * w + x) * 4;

                                if (isRgb565)
                                {
                                    // RGB565: RRRRR GGGGGG BBBBB
                                    rgba[idx + 0] = (byte)(((pixel >> 11) & 0x1F) * 255 / 31);
                                    rgba[idx + 1] = (byte)(((pixel >>  5) & 0x3F) * 255 / 63);
                                    rgba[idx + 2] = (byte)(((pixel      ) & 0x1F) * 255 / 31);
                                }
                                else
                                {
                                    // RGB555: xRRRRR GGGGG BBBBB
                                    rgba[idx + 0] = (byte)(((pixel >> 10) & 0x1F) * 255 / 31);
                                    rgba[idx + 1] = (byte)(((pixel >>  5) & 0x1F) * 255 / 31);
                                    rgba[idx + 2] = (byte)(((pixel      ) & 0x1F) * 255 / 31);
                                }
                                rgba[idx + 3] = 255; // fully opaque
                                x++;
                            }
                        }
                    }
                }

                images[i] = Image.CreateFromData(w, h, false, Image.Format.Rgba8, rgba);
            }

            return images;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[C16Decoder] Failed to decode: {e.Message}");
            return Array.Empty<Image>();
        }
    }

    /// <summary>Loads and decodes a .c16 file from the Godot res:// or absolute path.</summary>
    public static Image[] DecodeFile(string path)
    {
        if (!global::Godot.FileAccess.FileExists(path))
        {
            GD.PrintErr($"[C16Decoder] File not found: {path}");
            return Array.Empty<Image>();
        }

        var bytes = global::Godot.FileAccess.GetFileAsBytes(path);
        if (bytes == null || bytes.Length == 0)
        {
            GD.PrintErr($"[C16Decoder] Could not read file: {path}");
            return Array.Empty<Image>();
        }

        return Decode(bytes);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private readonly struct SpriteHeader
    {
        public readonly int    Width;
        public readonly int    Height;
        public readonly uint[] LineOffsets;

        public SpriteHeader(int w, int h, uint[] lo)
        {
            Width       = w;
            Height      = h;
            LineOffsets = lo;
        }
    }
}
