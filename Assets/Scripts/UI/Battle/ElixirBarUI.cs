using CrashRoyale.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    public class ElixirBarUI : MonoBehaviour
    {
        [SerializeField] private Image fullFill;
        [SerializeField] private Image chargingFill;
        [SerializeField] private TMP_Text amount;
        [SerializeField] private Transform amountBadge;
        [SerializeField] private GameObject doubleBadge;
        [SerializeField] private Team team = Team.Blue;

        private BattleManager battle;
        private int shown = -1;

        private void Update()
        {
            if (battle == null) battle = BattleManager.Instance;
            if (battle == null) return;

            var bank = battle.Bank(team);
            if (bank == null) return;

            float value = bank.Current;
            fullFill.fillAmount = Mathf.Floor(value + 0.0001f) / ElixirBank.Max;
            chargingFill.fillAmount = value / ElixirBank.Max;

            if (bank.Whole != shown)
            {
                shown = bank.Whole;
                amount.text = shown.ToString();
                if (amountBadge != null && shown > 0) UITween.Punch(amountBadge, 0.12f, 0.18f);
            }

            if (doubleBadge != null && doubleBadge.activeSelf != battle.DoubleElixir) doubleBadge.SetActive(battle.DoubleElixir);
        }
    }
}
