using UnityEngine;

public class LevelUpEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem levelParticles;

    private void Awake()
    {
        if (levelParticles)
        {
            levelParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            levelParticles.Clear(true);
        }
    }

    public void PlayLevelUp()
    {
        if (!levelParticles) return;

        levelParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        levelParticles.Clear(true);
        levelParticles.Play(true);
    }
}