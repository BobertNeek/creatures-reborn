using System.Collections.Generic;

namespace CreaturesReborn.Sim.Brain;

public enum BrainPortKind
{
    Drive,
    Motor,
    Chemical,
    LobeInput,
    LobeOutput,
    Module
}

public sealed record BrainPort(
    string Name,
    BrainPortKind Kind,
    int? LobeToken,
    int? Index,
    string Description);

public sealed class BrainPortRegistry
{
    private BrainPortRegistry(IReadOnlyList<BrainPort> ports)
    {
        Ports = ports;
    }

    public IReadOnlyList<BrainPort> Ports { get; }

    public static BrainPortRegistry CreateDefault()
    {
        var ports = new List<BrainPort>();
        int driv = Brain.TokenFromString("driv");
        int decn = Brain.TokenFromString("decn");

        for (int i = 0; i < 20; i++)
        {
            ports.Add(new(
                $"drive:{i}",
                BrainPortKind.Drive,
                driv,
                i,
                $"Drive lobe neuron {i}, fed from creature drive loci."));
        }

        ports.Add(new("motor:verb", BrainPortKind.Motor, decn, 0, "Current winning verb decision output."));
        ports.Add(new("motor:noun", BrainPortKind.Motor, decn, 1, "Current winning noun decision output."));
        ports.Add(new("chemical:reward", BrainPortKind.Chemical, null, 32, "Reward chemical reinforcement signal."));
        ports.Add(new("chemical:punishment", BrainPortKind.Chemical, null, 33, "Punishment chemical reinforcement signal."));
        ports.Add(new("chemical:instinct", BrainPortKind.Chemical, null, 255, "Birth instinct processing signal."));

        return new BrainPortRegistry(ports);
    }
}
