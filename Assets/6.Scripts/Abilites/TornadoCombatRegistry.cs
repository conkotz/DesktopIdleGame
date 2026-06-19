using System.Collections.Generic;
using UnityEngine;

/// <summary>Tracks active Tornado ultimate instances for lightning-arc routing and gameplay queries.</summary>
public static class TornadoCombatRegistry
{
    private static readonly List<TornadoInstance> Active = new();

    public static IReadOnlyList<TornadoInstance> ActiveInstances => Active;

    public static void Register(TornadoInstance instance)
    {
        if (instance == null || Active.Contains(instance))
            return;

        Active.Add(instance);
    }

    public static void Unregister(TornadoInstance instance)
    {
        if (instance == null)
            return;

        Active.Remove(instance);
    }

    public static void ClearAll()
    {
        Active.Clear();
    }
}
