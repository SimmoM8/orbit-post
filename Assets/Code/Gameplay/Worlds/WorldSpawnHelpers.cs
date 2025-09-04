using UnityEngine;
using OrbitPost.Gameplay.Planets;

public static class WorldSpawnHelpers
{
    // Centralized application of authored planet data to a spawned Planet
    public static void ApplyAuthoredPlanet(Planet planet, WorldDefinition.AuthoredPlanet ap)
    {
        if (!planet) return;

        // Set visual type and update sprite
        planet.planetType = ap.type;
        planet.ApplyTypeVisual();

        // Assign production profile (not visuals)
        planet.profile = ap.profile;

        // Apply authored mass
        planet.mass = ap.mass;
        if (planet.mass <= 0f)
        {
            Debug.LogWarning($"[WorldBuilder] Planet at {ap.position} has mass 0; it will not contribute to gravity until you set a positive mass in the WorldDefinition.");
        }

        // Rebuild producer weights if present
        var producer = planet.GetComponent<PlanetProducer>();
        if (producer) producer.RebuildTypeWeights();
    }

    // Centralized application of authored post data to a spawned DeliveryNode
    public static void ApplyAuthoredPost(DeliveryNode node, WorldDefinition.AuthoredPost ap)
    {
        if (!node) return;
        node.level = Mathf.Max(1, ap.startLevel);
        node.displayName = ap.displayName;
        node.influenceRadius = ap.influenceRadius;
        node.requestAmount = ap.initialRequestAmount; // syncs requestGoal internally
        node.SetRequest(ap.initialRequestMaterial);
    }
}
