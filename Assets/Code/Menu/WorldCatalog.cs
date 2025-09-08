using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Asset listing available worlds for the Play -> Choose a World screen.
/// Create via Assets/Create/Orbit Post/World Catalog and add your WorldDefinition assets.
/// </summary>
[CreateAssetMenu(menuName = "Orbit Post/World Catalog", fileName = "WorldCatalog")]
public class WorldCatalog : ScriptableObject
{
    public List<WorldDefinition> worlds = new List<WorldDefinition>();
}

