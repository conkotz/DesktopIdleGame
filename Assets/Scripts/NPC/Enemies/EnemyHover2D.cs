using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHover2D : MonoBehaviour
{
    public event Action<bool> OnHoverChanged;

    private void OnMouseEnter() => OnHoverChanged?.Invoke(true);
    private void OnMouseExit() => OnHoverChanged?.Invoke(false);
}