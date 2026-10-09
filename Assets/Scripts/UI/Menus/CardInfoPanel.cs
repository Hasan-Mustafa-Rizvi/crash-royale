using CrashRoyale.Data;
using TMPro;
using UnityEngine;

namespace CrashRoyale.UI
{
    /// <summary>Stats and description of the card selected in the deck builder.</summary>
    public class CardInfoPanel : MonoBehaviour
    {
        [SerializeField] private CardView card;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text stats;
        [SerializeField] private TMP_Text status;

        public void Show(CardData data, bool unlocked, bool inDeck)
        {
            if (data == null) return;

            card.SetCard(data);
            card.SetLocked(!unlocked);
            title.text = data.displayName.ToUpperInvariant();
            description.text = data.description;
            stats.text = Describe(data);

            if (!unlocked) status.text = $"LOCKED - unlock it in the shop for {data.unlockPrice} gold";
            else if (inDeck) status.text = "IN YOUR DECK";
            else status.text = "Tap a deck slot above to swap it in";
        }

        private static string Describe(CardData c)
        {
            string count = c.unitCount > 1 ? $" x{c.unitCount}" : string.Empty;
            string damage = c.splashRadius > 0f ? $"{c.damage} (area)" : c.damage.ToString();
            string range = c.attackKind == AttackKind.Melee ? "Melee" : c.attackKind == AttackKind.Kamikaze ? "Contact" : $"{c.attackRange:0.#}";
            string targets = c.targets == TargetMode.BuildingsOnly ? "Buildings" : "Troops & buildings";
            return $"Hitpoints  <b>{c.maxHealth}{count}</b>\n" +
                   $"Damage  <b>{damage}</b>\n" +
                   $"Hit speed  <b>{c.hitSpeed:0.0}s</b>\n" +
                   $"Range  <b>{range}</b>\n" +
                   $"Speed  <b>{SpeedLabel(c.moveSpeed)}</b>\n" +
                   $"Targets  <b>{targets}</b>";
        }

        private static string SpeedLabel(float speed)
        {
            if (speed >= 0.9f) return "Very fast";
            if (speed >= 0.7f) return "Fast";
            if (speed >= 0.5f) return "Medium";
            return "Slow";
        }
    }
}
