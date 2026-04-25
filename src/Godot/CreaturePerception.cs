using System;
using System.Collections.Generic;
using Godot;
using CreaturesReborn.Sim.World;

namespace CreaturesReborn.Godot;

internal static class CreaturePerception
{
    public static FoodNode? FindNearestReachableFood(Node? parent, Vector3 position, float maxDist)
    {
        if (parent == null) return null;

        var closeFood = FindNearestDirectFood(parent, position, maxDist);
        if (maxDist <= 2.0f)
            return closeFood;

        if (parent is not WorldNode world || world.Navigation == null)
            return closeFood;

        var foodsById = new Dictionary<string, FoodNode>();
        var targets = new List<NavigationTarget>();
        foreach (Node node in parent.GetChildren())
        {
            if (node is not FoodNode food || food.IsConsumed) continue;
            if (MathF.Abs(food.Position.X - position.X) > maxDist) continue;

            string id = food.GetInstanceId().ToString();
            foodsById[id] = food;
            targets.Add(new NavigationTarget(food.Position.X, food.Position.Y, id));
        }

        ResolvedNavigationTarget? target = world.Navigation.FindNearestReachableTarget(
            position.X,
            position.Y,
            targets);

        return target != null && foodsById.TryGetValue(target.Value.Id, out FoodNode? routed)
            ? routed
            : closeFood;
    }

    public static int DirectionToward(Node? parent, Vector3 from, Vector3 target)
    {
        if (parent is WorldNode world && world.Navigation != null)
            return world.FirstNavigationDirection(from, target);

        float dx = target.X - from.X;
        if (MathF.Abs(dx) < 0.1f) return 0;
        return dx > 0 ? 1 : -1;
    }

    private static FoodNode? FindNearestDirectFood(Node parent, Vector3 position, float maxDist)
    {
        if (maxDist <= 2.0f)
            return FindNearestFoodByDistance(parent, position, maxDist);

        const float SameFloorTolerance = 1.5f;

        FoodNode? same = null;
        float sameDist = maxDist;
        FoodNode? any = null;
        float anyDist = maxDist;

        foreach (Node node in parent.GetChildren())
        {
            if (node is not FoodNode food || food.IsConsumed) continue;

            float dx = MathF.Abs(food.Position.X - position.X);
            if (dx < anyDist)
            {
                any = food;
                anyDist = dx;
            }

            if (MathF.Abs(food.Position.Y - position.Y) <= SameFloorTolerance
                && dx < sameDist)
            {
                same = food;
                sameDist = dx;
            }
        }

        return same ?? any;
    }

    private static FoodNode? FindNearestFoodByDistance(Node parent, Vector3 position, float maxDist)
    {
        FoodNode? nearest = null;
        float nearestDist = maxDist;

        foreach (Node node in parent.GetChildren())
        {
            if (node is not FoodNode food || food.IsConsumed) continue;

            float distance = position.DistanceTo(food.Position);
            if (distance < nearestDist)
            {
                nearest = food;
                nearestDist = distance;
            }
        }

        return nearest;
    }
}
