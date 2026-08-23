using UnityEngine;

namespace WaterFlow.Core
{
    [CreateAssetMenu(fileName = "Particle Database", menuName = "Data/Particles/Particle Database")]
    public class ParticleDatabase : ScriptableObject
    {
        [SerializeField] Particle[] particles;
        public Particle[] Particles => particles;
    }
}
