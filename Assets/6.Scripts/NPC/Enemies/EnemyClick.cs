using UnityEngine;

[DisallowMultipleComponent]
public class EnemyClick : MonoBehaviour
{
    [SerializeField] private EnemyBaseController enemy;

    private void Awake()
    {
        if (!enemy) enemy = GetComponentInParent<EnemyBaseController>();
    }

    /// <summary>Called by PlayerController when this enemy is clicked.</summary>
    public EnemyBaseController GetEnemy() => enemy;
}