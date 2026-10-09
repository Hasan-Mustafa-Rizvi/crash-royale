using CrashRoyale.Battle;
using TMPro;
using UnityEngine;

namespace CrashRoyale.Visual
{
    /// <summary>Small sprite-based HP bar that floats above a troop or tower.</summary>
    public class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private TextMeshPro label;
        [SerializeField] private Color blueColor = new Color(0.25f, 0.65f, 1f);
        [SerializeField] private Color redColor = new Color(1f, 0.3f, 0.35f);
        [SerializeField] private bool alwaysVisible;
        [SerializeField] private float width = 0.62f;
        [SerializeField] private float height = 0.1f;

        private Health health;
        private int shownHealth = -1;
        private bool hidden;

        public void Bind(Health target, Team team)
        {
            health = target;
            fill.color = team == Team.Blue ? blueColor : redColor;
            if (label != null) label.color = Color.Lerp(fill.color, Color.white, 0.55f);
            Refresh(true);
        }

        public void Hide()
        {
            hidden = true;
            SetVisible(false);
        }

        private void LateUpdate() => Refresh(false);

        private void Refresh(bool force)
        {
            if (health == null) return;

            bool visible = !hidden && !health.IsDead && (alwaysVisible || health.Current < health.Max);
            SetVisible(visible);
            if (!visible || (!force && shownHealth == health.Current)) return;

            shownHealth = health.Current;
            float fraction = health.Fraction;
            fill.size = new Vector2(Mathf.Max(0.001f, width * fraction), height);
            if (label != null) label.text = health.Current.ToString();
        }

        private void SetVisible(bool visible)
        {
            if (background.enabled != visible) background.enabled = visible;
            if (fill.enabled != visible) fill.enabled = visible;
            if (label != null && label.enabled != visible) label.enabled = visible;
        }
    }
}
