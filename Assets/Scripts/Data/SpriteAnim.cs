using System;
using UnityEngine;

namespace CrashRoyale.Data
{
    /// <summary>A flipbook animation: a list of sprites played at a fixed rate.</summary>
    [Serializable]
    public class SpriteAnim
    {
        public Sprite[] frames = Array.Empty<Sprite>();
        public float fps = 10f;
        public bool loop = true;

        [Tooltip("Frame that fires the animation event (for example the hit frame of an attack). -1 means none.")]
        public int eventFrame = -1;

        public bool IsValid => frames != null && frames.Length > 0 && frames[0] != null;

        public float Duration => IsValid ? frames.Length / Mathf.Max(1f, fps) : 0f;
    }
}
