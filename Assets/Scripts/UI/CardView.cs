using CrashRoyale.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    /// <summary>A card face: frame by rarity, portrait, elixir cost and optional name, lock and highlight.</summary>
    public class CardView : MonoBehaviour
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image portrait;
        [SerializeField] private GameObject costBadge;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private TMP_Text lockText;
        [SerializeField] private GameObject highlight;
        [SerializeField] private GameObject cornerTag;
        [SerializeField] private TMP_Text cornerTagText;
        [SerializeField] private Image chargeOverlay;
        [SerializeField] private Sprite[] rarityFrames = new Sprite[3];

        private static readonly Color Dimmed = new Color(0.55f, 0.55f, 0.6f, 1f);

        public CardData Card { get; private set; }

        public void SetCard(CardData card)
        {
            Card = card;
            bool has = card != null;

            portrait.enabled = has;
            if (has) portrait.sprite = card.Portrait;
            if (costBadge != null) costBadge.SetActive(has);
            if (costText != null) costText.text = has ? card.elixirCost.ToString() : string.Empty;
            if (nameText != null) nameText.text = has ? card.displayName : string.Empty;

            if (has && frame != null && rarityFrames != null)
            {
                int r = (int)card.rarity;
                if (r < rarityFrames.Length && rarityFrames[r] != null) frame.sprite = rarityFrames[r];
            }

            SetCharge(1f);
        }

        public void SetLocked(bool locked, string label = null)
        {
            if (lockOverlay != null) lockOverlay.SetActive(locked);
            if (lockText != null) lockText.text = label ?? string.Empty;
            portrait.color = locked ? Dimmed : Color.white;
        }

        public void SetHighlighted(bool on)
        {
            if (highlight != null) highlight.SetActive(on);
        }

        public void SetTag(string text)
        {
            if (cornerTag == null) return;
            cornerTag.SetActive(!string.IsNullOrEmpty(text));
            if (cornerTagText != null) cornerTagText.text = text ?? string.Empty;
        }

        /// <summary>0..1 progress toward affording the card. Below 1 the card is greyed out and fills up.</summary>
        public void SetCharge(float fraction)
        {
            if (chargeOverlay == null) return;
            bool ready = fraction >= 1f;
            chargeOverlay.enabled = !ready;
            chargeOverlay.fillAmount = 1f - Mathf.Clamp01(fraction);
            portrait.color = ready ? Color.white : Dimmed;
        }
    }
}
