using System;
using UnityEngine;

public interface IMagicProjectileVisual
{
    event Action OnImpact;
    void Launch(Vector3 startPosition, Transform target, Vector3 fallbackTargetPosition, float? speedOverride = null, float? rotationOffsetOverride = null);
}
