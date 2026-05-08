using UnityEngine;

/// <summary>
/// Place on empty UI rects under the world map; <see cref="mapNodeId"/> must match <see cref="MapNodeDefinition.nodeId"/>.
/// </summary>
public class WorldMapNodeAnchor : MonoBehaviour
{
    [Tooltip("Matches MapNodeDefinition.nodeId for the level plotted at this anchor.")]
    public string mapNodeId;

    public string ResolveTrimmedId()
    {
        return string.IsNullOrWhiteSpace(mapNodeId) ? "" : mapNodeId.Trim();
    }
}

/// <summary>
/// Optional: parent folder for anchors belonging to one region. <see cref="regionId"/> matches <see cref="RegionDefinition.regionId"/>.
/// </summary>
public class WorldMapRegionAnchorGroup : MonoBehaviour
{
    [Tooltip("Matches RegionDefinition.regionId (e.g. greenlands, tutorial).")]
    public string regionId;

    public string ResolveTrimmedRegionId()
    {
        return string.IsNullOrWhiteSpace(regionId) ? "" : regionId.Trim();
    }
}
