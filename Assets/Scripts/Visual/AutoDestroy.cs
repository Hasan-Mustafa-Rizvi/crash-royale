using UnityEngine;

namespace CrashRoyale.Visual
{
    /// <summary>Destroys the object after a fixed time.</summary>
    public class AutoDestroy : MonoBehaviour
    {
        [SerializeField] private float lifetime = 1f;

        private void Start() => Destroy(gameObject, lifetime);
    }
}
