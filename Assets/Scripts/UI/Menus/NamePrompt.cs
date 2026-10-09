using CrashRoyale.Audio;
using CrashRoyale.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    /// <summary>First-launch name entry (replaces the old fake login). Also used to change the name later.</summary>
    public class NamePrompt : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private TMP_Text error;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private void Awake()
        {
            panel.SetActive(false);
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(() => panel.SetActive(false));
            input.characterLimit = PlayerProfile.MaxNameLength;
            input.onSubmit.AddListener(_ => Confirm());
        }

        public void Open(bool firstTime)
        {
            panel.SetActive(true);
            UITween.Pop(panel.transform, 0.85f, 1.03f, 0.22f);
            title.text = firstTime ? "WELCOME TO THE ARENA!\nWHAT SHOULD WE CALL YOU?" : "CHANGE NAME";
            input.text = PlayerProfile.Name;
            error.text = string.Empty;
            cancelButton.gameObject.SetActive(!firstTime);
        }

        private void Confirm()
        {
            string name = PlayerProfile.SanitizeName(input.text);
            if (name.Length < 2)
            {
                error.text = "Use 2-16 letters or numbers.";
                AudioManager.Play(Sfx.Invalid);
                UITween.Punch(input.transform, 0.06f, 0.2f);
                return;
            }

            PlayerProfile.Name = name;
            panel.SetActive(false);
        }
    }
}
