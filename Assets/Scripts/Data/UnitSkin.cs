using UnityEngine;

namespace CrashRoyale.Data
{
    /// <summary>The sprite animations for one unit in one team color.</summary>
    [CreateAssetMenu(menuName = "Crash Royale/Unit Skin", fileName = "UnitSkin")]
    public class UnitSkin : ScriptableObject
    {
        public SpriteAnim idle = new SpriteAnim();
        public SpriteAnim run = new SpriteAnim();
        public SpriteAnim attackSide = new SpriteAnim();
        public SpriteAnim attackUp = new SpriteAnim();
        public SpriteAnim attackDown = new SpriteAnim();

        [Tooltip("Extra animation, e.g. the fuse flash before a barrel explodes.")]
        public SpriteAnim special = new SpriteAnim();

        public bool IsValid => idle != null && idle.IsValid && run != null && run.IsValid;
    }
}
