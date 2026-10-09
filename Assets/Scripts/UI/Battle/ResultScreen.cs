using CrashRoyale.Battle;
using CrashRoyale.Core;
using CrashRoyale.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    public class ResultScreen : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text blueName;
        [SerializeField] private TMP_Text redName;
        [SerializeField] private Image[] blueCrowns = new Image[3];
        [SerializeField] private Image[] redCrowns = new Image[3];
        [SerializeField] private GameObject rewardsRow;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text trophyText;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Color winColor = new Color(1f, 0.83f, 0.25f);
        [SerializeField] private Color loseColor = new Color(1f, 0.4f, 0.4f);
        [SerializeField] private Color drawColor = new Color(0.8f, 0.85f, 1f);
        [SerializeField] private Color crownOff = new Color(1f, 1f, 1f, 0.15f);

        private void Awake()
        {
            panel.SetActive(false);
            playAgainButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.Battle));
            menuButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.MainMenu));
        }

        public void Show(BattleResult result, Reward reward, bool rewardApplied, string blue, string red)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            UITween.Pop(panel.transform, 0.8f, 1.04f, 0.3f);

            switch (result.Outcome)
            {
                case BattleOutcome.BlueWins:
                    title.text = "VICTORY!";
                    title.color = winColor;
                    subtitle.text = result.KingDestroyed ? (result.RedCrowns == 0 ? "Three crowns without losing a tower" : "Three crowns! King tower down")
                        : result.WentToOvertime ? "Won in overtime" : "Won on crowns";
                    break;
                case BattleOutcome.RedWins:
                    title.text = "DEFEAT";
                    title.color = loseColor;
                    subtitle.text = result.KingDestroyed ? "They took your king tower" : result.WentToOvertime ? "Lost in overtime" : "They had more crowns";
                    break;
                default:
                    title.text = "DRAW";
                    title.color = drawColor;
                    subtitle.text = "Even on crowns after overtime";
                    break;
            }

            blueName.text = blue;
            redName.text = red;
            SetCrowns(blueCrowns, result.BlueCrowns);
            SetCrowns(redCrowns, result.RedCrowns);

            rewardsRow.SetActive(rewardApplied);
            if (rewardApplied)
            {
                goldText.text = $"+{reward.Gold}";
                trophyText.text = reward.Trophies >= 0 ? $"+{reward.Trophies}" : reward.Trophies.ToString();
                trophyText.color = reward.Trophies >= 0 ? Color.white : loseColor;
            }
        }

        private void SetCrowns(Image[] icons, int count)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                icons[i].color = i < count ? Color.white : crownOff;
                if (i < count) UITween.Pop(icons[i].transform, 0.2f, 1.3f, 0.35f + i * 0.1f);
            }
        }
    }
}
