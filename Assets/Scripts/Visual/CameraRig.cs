using UnityEngine;

namespace CrashRoyale.Visual
{
    /// <summary>
    /// Keeps the whole arena visible on any aspect ratio (tall phones included) and adds a small screen shake.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private float minWorldWidth = 9.8f;
        [SerializeField] private float minWorldHeight = 18.7f;

        private static CameraRig instance;
        private Camera cam;
        private Vector3 basePosition;
        private float shakeTime;
        private float shakeDuration;
        private float shakeStrength;

        private void Awake()
        {
            instance = this;
            cam = GetComponent<Camera>();
            basePosition = transform.localPosition;
            Fit();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Shake(float strength, float duration)
        {
            if (instance == null) return;
            instance.shakeStrength = Mathf.Max(instance.shakeStrength * (instance.shakeTime > 0f ? 1f : 0f), strength);
            instance.shakeDuration = duration;
            instance.shakeTime = duration;
        }

        private void LateUpdate()
        {
            Fit();

            if (shakeTime > 0f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                float falloff = Mathf.Clamp01(shakeTime / Mathf.Max(0.01f, shakeDuration));
                Vector2 offset = Random.insideUnitCircle * shakeStrength * falloff;
                transform.localPosition = basePosition + new Vector3(offset.x, offset.y, 0f);
            }
            else
            {
                shakeStrength = 0f;
                transform.localPosition = basePosition;
            }
        }

        private void Fit()
        {
            float aspect = Mathf.Max(0.1f, cam.aspect);
            cam.orthographicSize = Mathf.Max(minWorldHeight * 0.5f, minWorldWidth / aspect * 0.5f);
        }
    }
}
