using System.Collections;
using CrashRoyale.Battle;
using CrashRoyale.Data;
using UnityEngine;

namespace CrashRoyale.UI
{
    /// <summary>
    /// World-space feedback while placing a card: a see-through preview of the troop, its footprint, and the
    /// enemy half shaded red where you are not allowed to deploy (minus any pocket you've earned).
    /// </summary>
    public class DeployGhost : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer unitPreview;
        [SerializeField] private SpriteRenderer footprint;
        [SerializeField] private SpriteRenderer[] forbiddenZones = new SpriteRenderer[2];
        [SerializeField] private SpriteRenderer invalidMarker;
        [SerializeField] private Color validColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.35f, 0.35f, 0.6f);
        [SerializeField] private Color zoneColor = new Color(1f, 0.15f, 0.2f, 0.22f);

        private Coroutine flash;

        private void Awake()
        {
            Hide();
            if (invalidMarker != null) invalidMarker.enabled = false;
        }

        public void Show(CardData card, Team team, Vector2 position, bool valid)
        {
            transform.position = position;
            unitPreview.enabled = true;
            unitPreview.sprite = card.PreviewSprite(team);
            unitPreview.transform.localScale = Vector3.one * card.visualScale;
            var tint = card.UsesFallback(team) ? card.fallbackTint : Color.white;
            unitPreview.color = (valid ? validColor : invalidColor) * tint;

            footprint.enabled = true;
            float size = Mathf.Max(0.5f, card.radius * 2.6f + (card.unitCount > 1 ? card.formationSpacing : 0f));
            footprint.transform.localScale = new Vector3(size, size * 0.6f, 1f);
            footprint.color = valid ? new Color(1f, 1f, 1f, 0.35f) : new Color(1f, 0.3f, 0.3f, 0.35f);

            ShowZones(team);
        }

        public void HideUnit()
        {
            unitPreview.enabled = false;
            footprint.enabled = false;
        }

        public void Hide()
        {
            HideUnit();
            foreach (var zone in forbiddenZones)
            {
                if (zone != null) zone.enabled = false;
            }
        }

        public void ShowZones(Team team)
        {
            var layout = ArenaLayout.Instance;
            if (layout == null) return;

            float forward = team.Forward();
            Vector2 c = layout.Center;
            float top = c.y + forward * layout.halfHeight;

            for (int lane = 0; lane < forbiddenZones.Length; lane++)
            {
                var zone = forbiddenZones[lane];
                if (zone == null) continue;

                float start = layout.PocketOpen(team, lane)
                    ? layout.RiverY + forward * (layout.riverHalfThickness + layout.pocketDepth)
                    : layout.RiverY - forward * layout.riverHalfThickness;

                float minY = Mathf.Min(start, top);
                float maxY = Mathf.Max(start, top);
                float minX = lane == 0 ? c.x - layout.halfWidth : c.x;
                float maxX = lane == 0 ? c.x : c.x + layout.halfWidth;

                zone.enabled = true;
                zone.color = zoneColor;
                zone.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
                zone.transform.localScale = new Vector3(maxX - minX, maxY - minY, 1f);
            }
        }

        public void FlashInvalid(Vector2 position)
        {
            if (invalidMarker == null) return;
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(FlashRoutine(position));
        }

        private IEnumerator FlashRoutine(Vector2 position)
        {
            invalidMarker.transform.position = position;
            invalidMarker.enabled = true;
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.45f;
                invalidMarker.color = new Color(1f, 0.25f, 0.25f, 0.8f * (1f - k));
                invalidMarker.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.1f, k);
                yield return null;
            }

            invalidMarker.enabled = false;
            flash = null;
        }
    }
}
