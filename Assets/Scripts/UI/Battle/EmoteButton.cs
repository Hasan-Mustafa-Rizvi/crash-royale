using System;
using CrashRoyale.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    public class EmoteButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text text;

        public void Setup(EmoteData emote, Action onClick)
        {
            bool hasIcon = emote.icon != null;
            icon.gameObject.SetActive(hasIcon);
            text.gameObject.SetActive(!hasIcon);
            if (hasIcon) icon.sprite = emote.icon;
            else text.text = emote.text;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }
    }
}
