using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Desktop Idle Game/Resource Node Catalog", fileName = "ResourceNodeCatalog")]
public class ResourceNodeCatalog : ScriptableObject
{
    [SerializeField] private List<NodeDefinition> nodes = new();

    private Dictionary<string, NodeDefinition> _byAssetName;

    private void OnEnable() => BuildLookup();

#if UNITY_EDITOR
    private void OnValidate() => BuildLookup();
#endif

    private void BuildLookup()
    {
        if (_byAssetName == null)
            _byAssetName = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);
        else
            _byAssetName.Clear();

        for (int i = 0; i < nodes.Count; i++)
        {
            NodeDefinition node = nodes[i];
            if (!node)
                continue;
            _byAssetName[node.name] = node;
        }
    }

    public bool TryGetByAssetName(string assetName, out NodeDefinition node)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            node = null;
            return false;
        }

        BuildLookup();
        return _byAssetName.TryGetValue(assetName.Trim(), out node);
    }

    public IReadOnlyList<NodeDefinition> Nodes => nodes;

    private static ResourceNodeCatalog _cached;

    public static ResourceNodeCatalog Instance
    {
        get
        {
            if (_cached)
                return _cached;
            _cached = Resources.Load<ResourceNodeCatalog>("Databases/ResourceNodeCatalog");
            return _cached;
        }
    }
}
