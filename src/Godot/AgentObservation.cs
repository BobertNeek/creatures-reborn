using Godot;
using CreaturesReborn.Godot.Agents;
using CreaturesReborn.Sim.Agent;

namespace CreaturesReborn.Godot;

internal static class AgentObservation
{
    public static bool TryGetArchetype(Node node, out AgentArchetype archetype)
    {
        switch (node)
        {
            case FoodNode food:
                archetype = food.AgentArchetype;
                return true;
            case GadgetNode gadget:
                archetype = gadget.AgentArchetype;
                return true;
            case FoodPlantNode plant:
                archetype = plant.AgentArchetype;
                return true;
            case DoorNode door:
                archetype = door.AgentArchetype;
                return true;
            case ElevatorNode:
                archetype = AgentCatalog.Elevator;
                return true;
            case EggNode egg:
                archetype = egg.AgentArchetype;
                return true;
            case IncubatorNode incubator:
                archetype = incubator.AgentArchetype;
                return true;
            case PointerAgent hand:
                archetype = hand.AgentArchetype;
                return true;
            default:
                archetype = default;
                return false;
        }
    }
}
