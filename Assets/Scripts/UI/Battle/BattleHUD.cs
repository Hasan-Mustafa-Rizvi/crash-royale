using System.Collections;
using CrashRoyale.Audio;
using CrashRoyale.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    /// <summary>Timer, crowns, player names, the intro "versus" card, callouts and the hand-off to the result screen.</summary>
    public class BattleHUD : MonoBehaviour
    {
        [Header("Top bar")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text timerCaption;
        [SerializeField] private TMP_Text blueName;
        [SerializeField] private TMP_Text redName;
        [SerializeField] private Image[] blueCrowns = new Image[3];
        [SerializeField] private Image[] redCrowns = new Image[3];
        [SerializeField] private Color crownOff = new Color(1f, 1f, 1f, 0.2f);

        [Header("Intro")]
        [SerializeField] private CanvasGroup intro;
        [SerializeField] private TMP_Text introBlue;
        [SerializeField] private TMP_Text introRed;

        [Header("Panels")]
        [SerializeField] private Announcer announcer;
        [SerializeField] private ResultScreen results;
        [SerializeField] private PauseMenu pauseMenu;
        [SerializeField] private Button pauseButton;

        [Header("Colors")]
        [SerializeField] private Color timerNormal = Color.white;
        [SerializeField] private Color timerUrgent = new Color(1f, 0.35f, 0.35f);
        [SerializeField] private Color announceGold = new Color(1f, 0.83f, 0.25f);
        [SerializeField] private Color announcePink = new Color(1f, 0.45f, 0.9f);

        private BattleManager battle;
        private int lastSecond = -1;

        private void Start()
        {
            battle = BattleManager.Instance;
            if (battle == null)
            {
                enabled = false;
                return;
            }

            battle.StateChanged += OnStateChanged;
            battle.CrownsChanged += OnCrownsChanged;
            battle.DoubleElixirStarted += OnDoubleElixir;
            battle.BattleEnded += OnBattleEnded;

            blueName.text = battle.DisplayName(Team.Blue);
            redName.text = battle.DisplayName(Team.Red);
            SetCrowns(blueCrowns, 0);
            SetCrowns(redCrowns, 0);

            if (pauseButton != null) pauseButton.onClick.AddListener(() => pauseMenu.Toggle());

            if (intro != null)
            {
                bool showIntro = battle.State == BattleState.Intro && !BattleSettings.SkipIntro;
                intro.alpha = showIntro ? 1f : 0f;
                intro.gameObject.SetActive(showIntro);
                if (introBlue != null) introBlue.text = battle.DisplayName(Team.Blue);
                if (introRed != null) introRed.text = battle.DisplayName(Team.Red);
                if (showIntro) UITween.Pop(intro.transform, 0.7f, 1.05f, 0.35f);
            }
        }

        private void OnDestroy()
        {
            if (battle == null) return;
            battle.StateChanged -= OnStateChanged;
            battle.CrownsChanged -= OnCrownsChanged;
            battle.DoubleElixirStarted -= OnDoubleElixir;
            battle.BattleEnded -= OnBattleEnded;
        }

        private void Update()
        {
            if (battle == null) return;

            float remaining = battle.TimeRemaining;
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            if (seconds != lastSecond)
            {
                lastSecond = seconds;
                timerText.text = $"{seconds / 60}:{seconds % 60:00}";
                if (battle.IsRunning && seconds <= 10 && seconds > 0)
                {
                    UITween.Punch(timerText.transform, 0.18f, 0.2f);
                    AudioManager.Play(Sfx.Countdown);
                }
            }

            bool urgent = battle.IsRunning && remaining <= 10f;
            timerText.color = urgent ? timerUrgent : timerNormal;
            timerCaption.text = battle.IsOvertime || battle.Result.WentToOvertime ? "OVERTIME" : "TIME LEFT";

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) pauseMenu.Toggle();
        }

        private void OnStateChanged(BattleState state)
        {
            switch (state)
            {
                case BattleState.Playing:
                    if (intro != null && intro.gameObject.activeSelf) StartCoroutine(HideIntro());
                    announcer.Show("BATTLE!", announceGold);
                    break;
                case BattleState.Overtime:
                    announcer.Show("OVERTIME!", announcePink, "Next crown wins");
                    break;
            }
        }

        private IEnumerator HideIntro()
        {
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                intro.alpha = 1f - t / 0.25f;
                yield return null;
            }

            intro.gameObject.SetActive(false);
        }

        private void OnDoubleElixir()
        {
            announcer.Show("2x ELIXIR!", announcePink, "Last minute");
            AudioManager.Play(Sfx.DoubleElixir);
        }

        private void OnCrownsChanged(Team team, int crowns)
        {
            var icons = team == Team.Blue ? blueCrowns : redCrowns;
            SetCrowns(icons, crowns);
            if (crowns > 0 && crowns <= icons.Length && icons[crowns - 1] != null) UITween.Pop(icons[crowns - 1].transform, 0.3f, 1.4f, 0.4f);
            AudioManager.Play(Sfx.Crown);
        }

        private void SetCrowns(Image[] icons, int count)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null) icons[i].color = i < count ? Color.white : crownOff;
            }
        }

        private void OnBattleEnded(BattleResult result)
        {
            StartCoroutine(ShowResults(result));
        }

        private IEnumerator ShowResults(BattleResult result)
        {
            string headline = result.Outcome == BattleOutcome.BlueWins ? "VICTORY!" : result.Outcome == BattleOutcome.RedWins ? "DEFEAT" : "DRAW";
            announcer.Show(headline, announceGold, null, 1.2f);
            yield return new WaitForSecondsRealtime(2f);
            results.Show(result, battle.LastReward, battle.RewardApplied, battle.DisplayName(Team.Blue), battle.DisplayName(Team.Red));
        }
    }
}
