using CrashRoyale.Audio;
using CrashRoyale.Core;
using CrashRoyale.Data;
using CrashRoyale.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private TMP_Text resetLabel;
        [SerializeField] private TMP_Text versionText;

        private bool confirmingReset;

        private void Awake()
        {
            panel.SetActive(false);
            closeButton.onClick.AddListener(Close);
            resetButton.onClick.AddListener(OnReset);

            musicSlider.onValueChanged.AddListener(value =>
            {
                PlayerProfile.MusicVolume = value;
                AudioManager.RefreshVolumes();
            });

            sfxSlider.onValueChanged.AddListener(value =>
            {
                PlayerProfile.SfxVolume = value;
                AudioManager.Play(Sfx.Click);
            });
        }

        public void Open()
        {
            musicSlider.SetValueWithoutNotify(PlayerProfile.MusicVolume);
            sfxSlider.SetValueWithoutNotify(PlayerProfile.SfxVolume);
            confirmingReset = false;
            resetLabel.text = "RESET PROGRESS";
            if (versionText != null) versionText.text = $"v{Application.version}";
            panel.SetActive(true);
            UITween.Pop(panel.transform, 0.85f, 1.03f, 0.2f);
        }

        public void Close() => panel.SetActive(false);

        private void OnReset()
        {
            if (!confirmingReset)
            {
                confirmingReset = true;
                resetLabel.text = "TAP AGAIN TO CONFIRM";
                UITween.Punch(resetButton.transform);
                return;
            }

            PlayerProfile.ResetProgress();
            PlayerProfile.EnsureInitialized(CardDatabase.Load());
            Close();
            SceneFlow.Load(SceneFlow.MainMenu);
        }
    }
}
