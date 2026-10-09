using System.Collections.Generic;
using CrashRoyale.Battle;
using CrashRoyale.Data;
using CrashRoyale.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    /// <summary>Emote button for the player, plus the speech bubbles that pop up for both sides.</summary>
    public class EmotePanel : MonoBehaviour
    {
        [SerializeField] private Button toggleButton;
        [SerializeField] private GameObject menu;
        [SerializeField] private RectTransform buttonContainer;
        [SerializeField] private EmoteButton buttonTemplate;
        [SerializeField] private EmoteBubble blueBubble;
        [SerializeField] private EmoteBubble redBubble;

        private BattleManager battle;
        private readonly List<EmoteButton> buttons = new List<EmoteButton>();

        private void Start()
        {
            battle = BattleManager.Instance;
            if (battle == null) return;

            battle.EmoteShown += OnEmote;
            menu.SetActive(false);
            buttonTemplate.gameObject.SetActive(false);
            toggleButton.onClick.AddListener(() =>
            {
                menu.SetActive(!menu.activeSelf);
                if (menu.activeSelf) UITween.Pop(menu.transform, 0.7f, 1.05f, 0.2f);
            });

            foreach (var emote in PlayerProfile.OwnedEmotes(battle.Assets.database))
            {
                var button = Instantiate(buttonTemplate, buttonContainer);
                button.gameObject.SetActive(true);
                string id = emote.id;
                button.Setup(emote, () =>
                {
                    battle.ShowEmote(Team.Blue, id);
                    menu.SetActive(false);
                });
                buttons.Add(button);
            }
        }

        private void OnDestroy()
        {
            if (battle != null) battle.EmoteShown -= OnEmote;
        }

        private void OnEmote(Team team, EmoteData emote)
        {
            var bubble = team == Team.Blue ? blueBubble : redBubble;
            if (bubble != null) bubble.Show(emote);
        }
    }
}
