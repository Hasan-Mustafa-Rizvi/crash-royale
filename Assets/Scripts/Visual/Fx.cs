using UnityEngine;

namespace CrashRoyale.Visual
{
    public static class Fx
    {
        public static GameObject Spawn(GameObject prefab, Vector2 position, float scale = 1f)
        {
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, position, Quaternion.identity);
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale *= scale;
            return go;
        }
    }
}
