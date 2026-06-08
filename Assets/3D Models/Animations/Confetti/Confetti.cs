using UnityEngine;

public class Confetti : MonoBehaviour
{
    [SerializeField] private ParticleSystem confettiSystem;

    void Awake()
    {
        confettiSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // Call this when the win screen is triggered
    public void PlayConfetti()
    {
        confettiSystem.Play();
    }
}
