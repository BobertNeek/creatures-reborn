using System;
using System.Text;
using CreaturesReborn.Sim.Util;

namespace CreaturesReborn.Sim.Genome;

/// <summary>
/// Direct port of c2e's <c>Genome</c> class (engine/Creature/Genome.h + Genome.cpp).
/// </summary>
/// <remarks>
/// <para>
/// The c2e genome is a raw byte buffer of the form
/// <c>['d','n','a','3']</c> (file header only, stripped by <see cref="GenomeReader"/>) followed by
/// a sequence of genes, each starting with <c>['g','e','n','e']</c>, then an 8-byte header
/// (type, subtype, id, generation, switch-on age, mutability flags, mutability breadth, variant),
/// then gene-specific data codons, terminated by a <c>['g','e','n','d']</c> end marker.
/// </para>
/// <para>
/// This port preserves that layout byte-for-byte. <c>myGenePointer</c> is an <see cref="int"/>
/// index into <c>myGenes</c>, not a C++ pointer; semantics are otherwise identical. Mutation and
/// crossover still operate on raw bytes, which means this port can load and save original
/// C3/DS <c>.gen</c> files unchanged.
/// </para>
/// </remarks>
public sealed class Genome
{
    // -------- data --------

    /// <summary>Raw genome bytes (excluding the file-level DNA3 header — that is consumed at read).</summary>
    internal byte[]? myGenes;

    internal int myStoredPosition;
    internal int myStoredPosition2;

    /// <summary>Index into <see cref="myGenes"/> of the next codon to read.</summary>
    internal int myGenePointer;

    /// <summary>Total number of bytes in <see cref="myGenes"/>.</summary>
    internal int myLength;

    /// <summary>Used during child copying in <see cref="CrossLoop"/>.</summary>
    internal int myCrossMaxLength;

    /// <summary>Unique moniker identifying this creature.</summary>
    public string Moniker { get; set; } = string.Empty;

    /// <summary>Express sex-linked genes of this gender only (1 = male, 2 = female).</summary>
    public int Sex { get; set; }

    /// <summary>Express genes of this behaviour variant only (0 = all, 1..NUM_BEHAVIOUR_VARIANTS).</summary>
    public int Variant { get; set; }

    /// <summary>Age of creature (0-255) — controls which time-gated genes switch on.</summary>
    public byte Age { get; set; }

    internal bool myEndHasBeenReached;
    internal byte myGeneAge = 255;

    public int CrossoverMutationCount { get; internal set; }
    public int CrossoverCrossCount { get; internal set; }

    private readonly IRng _rng;

    // -------- construction --------

    /// <summary>
    /// Null constructor — equivalent to c2e's <c>Genome::Genome()</c>. Call
    /// <see cref="AttachBytes"/> or <see cref="Cross"/> afterwards to populate.
    /// </summary>
    public Genome(IRng rng)
    {
        _rng = rng;
        Init();
    }

    private void Init()
    {
        myGenes = null;
        myStoredPosition = 0;
        myStoredPosition2 = 0;
        myLength = 0;
        Moniker = string.Empty;
        myGenePointer = 0;
        Sex = 1;
        Variant = 0;
        Age = 0;
        myEndHasBeenReached = false;
        myGeneAge = 255;
        CrossoverMutationCount = 0;
        CrossoverCrossCount = 0;
        myCrossMaxLength = 0;
    }

    /// <summary>
    /// Attach pre-loaded genome bytes (no DNA3 file header — that must be stripped by the caller).
    /// Called by <see cref="GenomeReader"/> after reading a <c>.gen</c> file.
    /// </summary>
    public void AttachBytes(byte[] bytes, int sex, byte age, int variant, string moniker)
    {
        Init();
        myGenes = bytes;
        myLength = bytes.Length;
        Sex = sex;
        Age = age;
        Variant = variant;
        Moniker = moniker;
        Reset();
    }

    /// <summary>
    /// Set a null byte to show we don't know the parents — we either have both parents or none.
    /// Any engineered file loaded in counts as no parents.
    /// </summary>
    public void DeclareUnverifiedParents()
    {
        Reset();
        if (myGenes == null) return;
        myGenes[myGenePointer + HeaderGeneOffsets.GO_MUM] = 0;
        myGenes[myGenePointer + HeaderGeneOffsets.GO_DAD] = 0;
    }

    // -------- token helpers --------

    /// <summary>Read a 4-byte token (little-endian int) at the given buffer offset.</summary>
    internal static int TokenAt(byte[] buf, int offset)
    {
        return buf[offset]
            | (buf[offset + 1] << 8)
            | (buf[offset + 2] << 16)
            | (buf[offset + 3] << 24);
    }

    /// <summary>Write a 4-byte token (little-endian int) at the given buffer offset.</summary>
    internal static void WriteTokenAt(byte[] buf, int offset, int token)
    {
        buf[offset + 0] = (byte)(token & 0xFF);
        buf[offset + 1] = (byte)((token >> 8) & 0xFF);
        buf[offset + 2] = (byte)((token >> 16) & 0xFF);
        buf[offset + 3] = (byte)((token >> 24) & 0xFF);
    }

    // -------- crossover --------

    /// <summary>
    /// Derive offspring genome from both parents, using crossing-over, cutting errors, and mutations.
    /// Port of <c>Genome::Cross</c> (Genome.cpp:251).
    /// </summary>
    public void Cross(string newMoniker, Genome mum, Genome dad,
        byte mumChanceOfMutation, byte mumDegreeOfMutation,
        byte dadChanceOfMutation, byte dadDegreeOfMutation)
    {
        Moniker = newMoniker;

        CrossoverMutationCount = 0;
        CrossoverCrossCount = 0;
        CrossLoop(mum, dad, mumChanceOfMutation, mumDegreeOfMutation,
            dadChanceOfMutation, dadDegreeOfMutation);

        // Write mum's & dad's monikers into the Header Gene, so that when it gets expressed
        // the child will be able to recognise its parents & siblings.
        Reset();
        if (myGenes == null)
            return;

        WriteMonikerField(HeaderGeneOffsets.GO_MUM, mum.Moniker);
        WriteMonikerField(HeaderGeneOffsets.GO_DAD, dad.Moniker);
    }

    private void WriteMonikerField(int fieldOffset, string moniker)
    {
        if (myGenes == null) return;
        for (int i = 0; i < 32; ++i)
        {
            byte value = (i < moniker.Length) ? (byte)moniker[i] : (byte)0;
            myGenes[myGenePointer + fieldOffset + i] = value;
        }
    }

    /// <summary>
    /// Port of <c>Genome::CrossLoop</c> (Genome.cpp:293).
    /// Call the null constructor only before this.
    /// </summary>
    private void CrossLoop(Genome mum, Genome dad,
        byte mumChanceOfMutation, byte mumDegreeOfMutation,
        byte dadChanceOfMutation, byte dadDegreeOfMutation)
    {
        if (mum.myGenes == null || dad.myGenes == null)
            throw new GenomeException("CrossLoop: a parent genome is not loaded");

        // Allocate enough space for the inherited genome.
        // The (OUR_LONGER_THAN_ANY_GENE * 2) margin ensures we have space for the last gene and
        // any closing bytes that are written.
        myCrossMaxLength = mum.myLength + dad.myLength;
        myGenes = new byte[myCrossMaxLength + GeneConstants.OUR_LONGER_THAN_ANY_GENE * 2];
        myLength = 0;
        myGenePointer = 0;

        Genome src; // gene being read
        Genome alt; // gene not being read
        int prevGeneId = 0;
        bool bMum;

        mum.Reset();
        dad.Reset();
        Reset();

        if (_rng.Rnd(1) != 0)
        {
            bMum = true;
            src = mum;
            alt = dad;
        }
        else
        {
            bMum = false;
            src = dad;
            alt = mum;
        }

        while (true)
        {
            int cross;
            int g;
            do
            {
                // Pick next cross-over point
                cross = _rng.Rnd(10, GeneConstants.LINKAGE * 2); // avg n-gene linkage (Dylan: min was 1)

                // For each gene up to next crossover point...
                for (g = 0; g < cross; g++)
                {
                    if ((myGenePointer >= myCrossMaxLength) ||
                        (TokenAt(src.myGenes!, src.myGenePointer) == GeneConstants.ENDGENOMETOKEN))
                    {
                        Terminate();
                        return;
                    }
                    prevGeneId = src.GeneID();

                    if (bMum)
                        CopyGene(src, mumChanceOfMutation, mumDegreeOfMutation);
                    else
                        CopyGene(src, dadChanceOfMutation, dadDegreeOfMutation);
                }

                if ((myGenePointer >= myCrossMaxLength) ||
                    TokenAt(src.myGenes!, src.myGenePointer) == GeneConstants.ENDGENOMETOKEN)
                {
                    Terminate();
                    return;
                }

                // Reached a potential crossover point. Don't stop if there's no equivalent allele
                // on the other strand, or if we're in the middle of a run of identical genes.
            } while (!alt.FindGene(src.GeneID()) || prevGeneId == src.GeneID());

            CrossoverCrossCount++;

            // Swap strands
            (src, alt) = (alt, src);
            bMum = !bMum;

            // Decide whether to cut or dup a gene:
            //   no error - continue from where src is pointing     (ABC -> def...)
            //   dup      - copy alt version then continue with src (ABCD -> def...)
            //   cut      - skip next gene on src                   (ABC -> efg...)
            g = 0;
            if (_rng.Rnd(GeneConstants.CUTERRORRATE) == 0)
                g = _rng.Rnd(1, 2);

            if (g == 1 && (alt.myGenes![alt.myGenePointer + GeneHeaderOffsets.GH_FLAGS] & (byte)MutFlags.DUP) != 0)
            {
                // DUP — copy gene from previous strand before continuing with same gene on new strand.
                alt.myGenes[alt.myGenePointer + GeneHeaderOffsets.GH_GEN]++; // mark clone
                if (bMum)
                    CopyGene(alt, dadChanceOfMutation, dadDegreeOfMutation);
                else
                    CopyGene(alt, mumChanceOfMutation, mumDegreeOfMutation);
            }
            else if (g == 2 && (src.myGenes![src.myGenePointer + GeneHeaderOffsets.GH_FLAGS] & (byte)MutFlags.CUT) != 0)
            {
                // CUT — start with next-but-one gene.
                if (!src.NextMarker())
                {
                    Terminate();
                    return;
                }
            }
        }
    }

    /// <summary>Add the end-of-genome marker at <c>myGenePointer</c> and record final length.</summary>
    private void Terminate()
    {
        if (myGenes == null) return;
        WriteTokenAt(myGenes, myGenePointer, GeneConstants.ENDGENOMETOKEN);
        myGenePointer += 4;
        myLength = myGenePointer;
    }

    /// <summary>
    /// Find the next gene start marker. Set <see cref="myGenePointer"/> to point to it.
    /// Returns false if end of genome was reached. Port of <c>Genome::NextMarker</c>.
    /// </summary>
    private bool NextMarker()
    {
        if (myGenes == null) return false;
        myGenePointer++; // in case still pointing at previous marker
        int marker;
        do
        {
            marker = TokenAt(myGenes, myGenePointer);
            myGenePointer++;
            if (marker == GeneConstants.ENDGENOMETOKEN)
                return false;
        } while (marker != GeneConstants.GENETOKEN);
        myGenePointer--; // undo the increment & point to the marker
        return true;
    }

    /// <summary>
    /// Unique header identifier derived from a gene's type, subtype and ID number.
    /// Port of <c>Genome::GeneID</c>.
    /// </summary>
    private int GeneID()
    {
        if (myGenes == null) return 0;
        return (myGenes[myGenePointer + GeneHeaderOffsets.GH_TYPE] << 16)
             | (myGenes[myGenePointer + GeneHeaderOffsets.GH_SUB] << 8)
             | myGenes[myGenePointer + GeneHeaderOffsets.GH_ID];
    }

    /// <summary>
    /// Find the first gene in this genome whose header ID equals <paramref name="id"/>, and set
    /// <see cref="myGenePointer"/> to the start of that gene. Port of <c>Genome::FindGene</c>.
    /// </summary>
    private bool FindGene(int id)
    {
        Reset();
        while (id != GeneID())
        {
            if (!NextMarker())
                return false;
        }
        return true;
    }

    /// <summary>
    /// Copy next gene from <paramref name="src"/> to this genome, mutating occasionally if
    /// the gene permits mutation. Port of <c>Genome::CopyGene</c>.
    /// </summary>
    private void CopyGene(Genome src, byte parentChanceOfMutation, byte parentDegreeOfMutation)
    {
        if (myGenes == null || src.myGenes == null)
            throw new GenomeException("CopyGene: genome buffer not allocated");

        int countLength = 0;
        int mutate = src.myGenes[src.myGenePointer + GeneHeaderOffsets.GH_FLAGS] & (int)MutFlags.MUT;
        byte mutability = src.myGenes[src.myGenePointer + GeneHeaderOffsets.GH_MUTABILITY];

        // Copy most of header without mutation.
        for (int i = 0; i < GeneHeaderOffsets.GH_SWITCHON; i++)
        {
            ++countLength;
            myGenes[myGenePointer++] = src.myGenes[src.myGenePointer++];
        }

        // Switch-on time may mutate.
        CopyCodon(src, mutate, mutability, parentChanceOfMutation, parentDegreeOfMutation);

        // Flags byte is never mutated.
        ++countLength;
        myGenes[myGenePointer++] = src.myGenes[src.myGenePointer++];

        // Copy rest of gene with possible mutations.
        while ((TokenAt(src.myGenes, src.myGenePointer) != GeneConstants.GENETOKEN) &&
               (TokenAt(src.myGenes, src.myGenePointer) != GeneConstants.ENDGENOMETOKEN))
        {
            ++countLength;
            CopyCodon(src, mutate, mutability, parentChanceOfMutation, parentDegreeOfMutation);
        }

        if (countLength >= GeneConstants.OUR_LONGER_THAN_ANY_GENE)
        {
            throw new GenomeException(
                $"Gene exceeded maximum length: {countLength} >= {GeneConstants.OUR_LONGER_THAN_ANY_GENE}");
        }
    }

    /// <summary>
    /// Copy one codon from <paramref name="src"/> to this genome, mutating occasionally.
    /// Port of <c>Genome::CopyCodon</c>.
    /// </summary>
    private void CopyCodon(Genome src, int mutate, byte mutability,
        byte parentChanceOfMutation, byte parentDegreeOfMutation)
    {
        if (myGenes == null || src.myGenes == null) return;
        const float fMaxDegreeMinus1 = 127.0f;

        byte codon = src.myGenes[src.myGenePointer++];

        if (mutate != 0)
        {
            int chanceOfMutation = GeneConstants.MUTATIONRATE;
            chanceOfMutation = (chanceOfMutation * (256 - mutability)) / 256;
            chanceOfMutation = (chanceOfMutation * (256 - parentChanceOfMutation)) / 256;

            if (_rng.Rnd(chanceOfMutation) == 0)
            {
                // 1.0 -> linear relationship ... 32.0 -> tight bell curve.
                double dDegree = 1.0 + (fMaxDegreeMinus1 * ((double)(255 - parentDegreeOfMutation)) / 255.0);
                double dRandom = _rng.RndFloat();
                double dP = System.Math.Pow(dRandom, dDegree);

                // MutationMask must always have at least one bit set.
                byte mutationMask = (byte)(255.0 * dP);
                if (mutationMask == 0x00)
                    mutationMask = 0x01;

                byte newCodon = (byte)(codon ^ mutationMask);
                if (newCodon != codon)
                    CrossoverMutationCount++;
                codon = newCodon;
            }
        }

        myGenes[myGenePointer++] = codon;
    }

    // -------- gene expression --------

    /// <summary>Reset the read pointer to the start of the genome.</summary>
    public void Reset()
    {
        myGenePointer = 0;
        myEndHasBeenReached = false;
        myGeneAge = 255;
    }

    public void Store()    { myStoredPosition = myGenePointer; }
    public void Restore()  { myGenePointer = myStoredPosition; myEndHasBeenReached = false; }
    public void Store2()   { myStoredPosition2 = myGenePointer; }
    public void Restore2() { myGenePointer = myStoredPosition2; myEndHasBeenReached = false; }

    /// <summary>
    /// Extract a codon from the next position in a gene and clamp it into the range
    /// <c>[min, max]</c>. Wraps mutated out-of-range codons rather than truncating.
    /// Port of <c>Genome::GetCodon</c>.
    /// </summary>
    public int GetCodon(int min, int max)
    {
        if (myGenes == null) return min;
        int c = myGenes[myGenePointer++];
        if (c >= min && c <= max)
            return c;
        return c % (max - min + 1) + min;
    }

    public int GetCodonLessThan(int maxValue) => GetCodon(0, maxValue - 1);

    /// <summary>Copy a four-byte token (e.g. moniker) from the gene. Pointer advances by 4.</summary>
    public int GetToken()
    {
        if (myGenes == null) return 0;
        int result = TokenAt(myGenes, myGenePointer);
        myGenePointer += 4;
        return result;
    }

    public byte GetByte()                   => (byte)GetCodon(0, 255);
    public char GetChar()                   => (char)GetByte();
    public int  GetByteWithInvalid()        { int b = GetCodon(0, 255); return b == 255 ? -1 : b; }
    public bool GetBool()                   => GetByte() != 0;
    public float GetFloat()                 => GetByte() / 255f;
    public float GetSignedFloat()           => (GetCodon(0, 248) / 124.0f) - 1.0f;
    public int   GetInt()                   { int hi = GetByte(); int lo = GetByte(); return hi * 256 + lo; }
    public byte  GetGeneAge()               => myGeneAge;

    /// <summary>
    /// Find the next gene start marker and set <see cref="myGenePointer"/> to point to its TYPE
    /// codon. Returns false on end of genome. Port of <c>Genome::GetStart</c>.
    /// </summary>
    private bool GetStart()
    {
        if (myGenes == null) return false;
        int marker;
        do
        {
            marker = TokenAt(myGenes, myGenePointer);
            myEndHasBeenReached = (marker == GeneConstants.ENDGENOMETOKEN);
            if (myEndHasBeenReached)
                return false;
            myGenePointer++;
        } while (marker != GeneConstants.GENETOKEN);

        myGenePointer += 3; // point to next codon (gene TYPE)
        return true;
    }

    public bool TestCodonExtn()
    {
        if (myGenes == null) return false;
        int marker = TokenAt(myGenes, myGenePointer);
        int gext = ('t' << 24) | ('x' << 16) | ('e' << 8) | 'g';
        if (marker == gext)
        {
            myGenePointer += 4;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Count the number of genes of the given type/subtype in the current genome (resets pointer).
    /// Port of <c>Genome::CountGeneType</c>.
    /// </summary>
    public int CountGeneType(int type, int subtype, int numsubs)
    {
        Reset();
        int count = 0;
        while (GetGeneType(type, subtype, numsubs, GeneSwitchOverride.SWITCH_ALWAYS))
            count++;
        return count;
    }

    /// <summary>
    /// Find the next gene of the given type/subtype (and, if sex-linked, the appropriate gender).
    /// Skips genes that haven't switched on yet. Sets <see cref="myGenePointer"/> to the first codon
    /// after the header. Returns false if end of genome reached. Port of <c>Genome::GetGeneType</c>.
    /// </summary>
    public bool GetGeneType(
        int type,
        int subtype,
        int numsubs,
        GeneSwitchOverride flag = GeneSwitchOverride.SWITCH_AGE,
        int endType = GeneConstants.INVALIDGENE,
        int endSubType = GeneConstants.INVALIDSUBGENE,
        int otherEndType = GeneConstants.INVALIDGENE)
    {
        if (myGenes == null) return false;
        int enteredAt = myGenePointer;

        while (GetStart())
        {
            int thisGeneType    = GetCodon(0, GeneTypeInfo.NUMGENETYPES - 1);
            int thisGeneSubType = GetCodon(0, numsubs - 1);
            myGenePointer++; // skip ID
            myGenePointer++; // skip Generation
            myGeneAge = (byte)GetCodon(0, 255);
            int sexOfThisGene = GetCodon(0, 255);
            myGenePointer++; // skip Mutability Weighting
            int variantOfThisGene = GetCodon(0, GeneConstants.NUM_BEHAVIOUR_VARIANTS);

            // Check switch-on time (unless this is an organ gene — type 3, subtype 0).
            if (!(thisGeneType == 3 && thisGeneSubType == 0) &&
                !TimeToSwitchOn(myGeneAge, flag))
                continue;

            // Check sex: express if MIGNORE, or if no sex-link, or if linked to current sex.
            bool sexOk =
                (sexOfThisGene & (int)MutFlags.MIGNORE) == 0 &&
                (((sexOfThisGene & ((int)MutFlags.LINKMALE | (int)MutFlags.LINKFEMALE)) == 0) ||
                 ((sexOfThisGene & (int)MutFlags.LINKMALE)   != 0 && Sex == GeneConstants.MALE) ||
                 ((sexOfThisGene & (int)MutFlags.LINKFEMALE) != 0 && Sex == GeneConstants.FEMALE));
            if (!sexOk)
                continue;

            // Check behaviour variant.
            if (!(variantOfThisGene == 0 || variantOfThisGene == Variant))
                continue;

            // Early-stop on endType conditions.
            if (thisGeneType == otherEndType ||
                (thisGeneType == endType &&
                 (endSubType == GeneConstants.INVALIDGENE || endSubType == thisGeneSubType)))
            {
                myGenePointer = enteredAt;
                return false;
            }

            if (thisGeneType == type && thisGeneSubType == subtype)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Port of <c>Genome::TimeToSwitchOn</c>. Returns whether a gene is eligible to switch on
    /// right now, given the creature's current age and the requested switch-on override.
    /// </summary>
    private bool TimeToSwitchOn(byte switchOnTime, GeneSwitchOverride flag)
    {
        switch (flag)
        {
            case GeneSwitchOverride.SWITCH_AGE:     return switchOnTime == Age;
            case GeneSwitchOverride.SWITCH_ALWAYS:  return true;
            case GeneSwitchOverride.SWITCH_EMBRYO:  return Age == 0;
            case GeneSwitchOverride.SWITCH_UPTOAGE: return switchOnTime <= Age;
            default:                                return true;
        }
    }

    /// <summary>
    /// Returns the Generation# byte of the gene that <see cref="myGenePointer"/> currently
    /// points into (just past the header). Port of <c>Genome::Generation</c>.
    /// </summary>
    public byte Generation()
    {
        if (myGenes == null) return 0;
        // myGenePointer has just been set by GetGeneType() and points to the first data codon.
        // Look back to the start marker, then forward to the GH_GEN field.
        return myGenes[myGenePointer - GeneHeaderOffsets.GH_LENGTH + GeneHeaderOffsets.GH_GEN];
    }

    /// <summary>
    /// Return the creature's genus as stored in the Header Gene. Port of <c>Genome::GetGenus</c>.
    /// Note: after this call the read pointer is reset to the start of the genome.
    /// </summary>
    public byte GetGenus()
    {
        if (myGenes == null) return 0;
        Reset();
        byte genus = myGenes[myGenePointer + HeaderGeneOffsets.GO_GENUS];
        Reset();
        return genus;
    }

    /// <summary>
    /// Returns a read-only view of the raw genome bytes for serialisation. Preferred over
    /// exposing the array directly so callers can't accidentally mutate it.
    /// </summary>
    public ReadOnlySpan<byte> AsSpan() =>
        myGenes == null ? ReadOnlySpan<byte>.Empty : new ReadOnlySpan<byte>(myGenes, 0, myLength);

    public int Length => myLength;
}
