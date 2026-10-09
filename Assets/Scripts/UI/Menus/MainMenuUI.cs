using CrashRoyale.Audio;
using CrashRoyale.Battle;
using CrashRoyale.Core;
using CrashRoyale.Data;
using CrashRoyale.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private TMP_Text trophies;
        [SerializeField] private TMP_Text gold;
        [SerializeField] private TMP_Text record;
        [SerializeField] private Button editNameButton;

        [Header("Navigation")]
        [SerializeField] private Button battleButton;
        [SerializeField] private Button deckButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button howToPlayButton;
        [SerializeField] private Button quitButton;

        [Header("Difficulty")]
        [SerializeField] private Button[] difficultyButtons = new Button[3];
        [SerializeField] private Image[] difficultyBackgrounds = new Image[3];
        [SerializeField] private TMP_Text difficultyHint;
        [SerializeField] private Color difficultyOn = new Color(1f, 0.8f, 0.25f);
        [SerializeField] private Color difficultyOff = new Color(0.35f, 0.25f, 0.45f);

        [Header("Panels")]
        [SerializeField] private NamePrompt namePrompt;
        [SerializeField] private SettingsPanel settings;
        [SerializeField] private GameObject howToPlay;
        [SerializeField] private Button howToPlayClose;
        [SerializeField] private Transform logo;

        private void Start()
        {
            PlayerProfile.EnsureInitialized(CardDatabase.Load());
            AudioManager.PlayMusic(MusicTrack.Menu);

            battleButton.onClick.AddListener(StartBattle);
            deckButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.Deck));
            shopButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.Shop));
            settingsButton.onClick.AddListener(settings.Open);
            editNameButton.onClick.AddListener(() => namePrompt.Open(false));
            howToPlayButton.onClick.AddListener(() =>
            {
                howToPlay.SetActive(true);
                UITween.Pop(howToPlay.transform, 0.85f, 1.03f, 0.2f);
            });
            howToPlayClose.onClick.AddListener(() => howToPlay.SetActive(false));
            howToPlay.SetActive(false);

#if UNITY_WEBGL || UNITY_EDITOR
            quitButton.gameObject.SetActive(false);
#else
            quitButton.onClick.AddListener(Application.Quit);
#endif

            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                var difficulty = (Difficulty)i;
                difficultyButtons[i].onClick.AddListener(() => PlayerProfile.Difficulty = difficulty);
            }

            PlayerProfile.Changed += Refresh;
            Refresh();

            if (logo != null) UITween.Pop(logo, 0.6f, 1.06f, 0.45f);
            if (!PlayerProfile.HasName) namePrompt.Open(true);
        }

        private void OnDestroy() => PlayerProfile.Changed -= Refresh;

        private void StartBattle()
        {
            BattleSettings.ResetDebugOptions();
            BattleSettings.Difficulty = PlayerProfile.Difficulty;
            SceneFlow.Load(SceneFlow.Battle);
        }

        private void Refresh()
        {
            playerName.text = PlayerProfile.HasName ? PlayerProfile.Name : "Player";
            trophies.text = PlayerProfile.Trophies.ToString();
            gold.text = PlayerProfile.Gold.ToString();
            record.text = $"W {PlayerProfile.Wins}   L {PlayerProfile.Losses}   D {PlayerProfile.Draws}";

            var current = PlayerProfile.Difficulty;
            for (int i = 0; i < difficultyBackgrounds.Length; i++)
            {
                if (difficultyBackgrounds[i] != null) difficultyBackgrounds[i].color = i == (int)current ? difficultyOn : difficultyOff;
            }

            difficultyHint.text = current switch
            {
                Difficulty.Easy => "Slow to react. Good for learning the cards.",
                Difficulty.Hard => "Fast counters and real pushes. Pays 1.5x gold.",
                _ => "Defends, counters and pushes when it has elixir."
            };
        }
    }
}
