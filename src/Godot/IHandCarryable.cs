using Godot;
using CreaturesReborn.Sim.Agent;

namespace CreaturesReborn.Godot;

public interface IHandCarryable
{
    bool CanBeCarriedByHand { get; }
    Node3D CarryNode { get; }
    AgentArchetype AgentArchetype { get; }

    void PickUp(Node3D holder);
    void Drop(Vector3 worldPos);
}
