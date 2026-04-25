using System;
using Godot;
using CreaturesReborn.Sim.Creature;

namespace CreaturesReborn.Godot;

internal static class CreatureContextDrives
{
    public static void Apply(CreatureNode node, Creature creature)
    {
        Node? parent = node.GetParent();
        if (parent == null) return;

        var bounds = node.GetRoomBounds();
        if (bounds.HasValue)
        {
            var (lb, rb) = bounds.Value;
            float distL = node.Position.X - lb;
            float distR = rb - node.Position.X;
            float nearest = MathF.Min(distL, distR);
            if (nearest < 1.5f)
            {
                float fear = (1.0f - nearest / 1.5f) * 0.6f;
                creature.AddDriveInput(DriveId.Fear, fear);
            }
        }

        bool foundFriend = false;
        foreach (Node n in parent.GetChildren())
        {
            if (n is not CreatureNode other || other == node || other.Creature == null)
                continue;

            float dist = node.Position.DistanceTo(other.Position);
            if (dist < 4.0f)
            {
                float closeness = 1.0f - dist / 4.0f;
                creature.AddDriveInput(DriveId.Loneliness, -closeness * 0.4f);
                foundFriend = true;
            }
        }

        if (!foundFriend)
            creature.AddDriveInput(DriveId.Loneliness, 0.05f);
    }
}
