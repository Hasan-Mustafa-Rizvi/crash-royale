using System.Collections.Generic;
using CrashRoyale.Audio;
using CrashRoyale.Core;
using CrashRoyale.Data;
using CrashRoyale.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    /// <summary>Spend battle gold on locked cards and emotes.</summary>
    public class ShopScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private RectTransform cardList;
        [SerializeField] private RectTransform emoteList;
        [SerializeField] private ShopItemView cardTemplate;
        [SerializeField] private ShopItemView emoteTemplate;
        [SerializeField] private Button backButton;
        [SerializeField] private Button deckButton;
        [SerializeField] private Transform goldBadge;

        private readonly List<(ShopItemView view, CardData card)> cardItems = new List<(ShopItemView, CardData)>();
        private readonly List<(ShopItemView view, EmoteData emote)> emoteItems = new List<(ShopItemView, EmoteData)>();
        private CardDatabase database;

        private void Start()
        {
            database = CardDatabase.Load();
            PlayerProfile.EnsureInitialized(database);
            AudioManager.PlayMusic(MusicTrack.Menu);

            cardTemplate.gameObject.SetActive(false);
            emoteTemplate.gameObject.SetActive(false);

            foreach (var card in database.cards)
            {
                if (card == null || card.unlockedByDefault) continue;
                var view = Instantiate(cardTemplate, cardList);
                view.gameObject.SetActive(true);
                view.SetupCard(card);
                var captured = card;
                view.BuyButton.onClick.AddListener(() => BuyCard(captured));
                cardItems.Add((view, card));
            }

            foreach (var emote in database.emotes)
            {
                if (emote.price <= 0) continue;
                var view = Instantiate(emoteTemplate, emoteList);
                view.gameObject.SetActive(true);
                view.SetupEmote(emote);
                var captured = emote;
                view.BuyButton.onClick.AddListener(() => BuyEmote(captured));
                emoteItems.Add((view, emote));
            }

            backButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.MainMenu));
            deckButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.Deck));
            PlayerProfile.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy() => PlayerProfile.Changed -= Refresh;

        private void BuyCard(CardData card)
        {
            if (PlayerProfile.IsUnlocked(card)) return;
            if (!PlayerProfile.TrySpendGold(card.unlockPrice))
            {
                NotEnoughGold();
                return;
            }

            PlayerProfile.Unlock(card.id);
            AudioManager.Play(Sfx.Purchase);
            Toast.Show($"{card.displayName} unlocked! Add it in your deck.", 2f);
        }

        private void BuyEmote(EmoteData emote)
        {
            if (PlayerProfile.OwnsEmote(emote)) return;
            if (!PlayerProfile.TrySpendGold(emote.price))
            {
                NotEnoughGold();
                return;
            }

            PlayerProfile.GrantEmote(emote.id);
            AudioManager.Play(Sfx.Purchase);
            Toast.Show("New emote! Use it in battle.", 2f);
        }

        private void NotEnoughGold()
        {
            AudioManager.Play(Sfx.Invalid);
            Toast.Show("Not enough gold. Win battles to earn more!");
            if (goldBadge != null) UITween.Punch(goldBadge, 0.2f, 0.25f);
        }

        private void Refresh()
        {
            goldText.text = PlayerProfile.Gold.ToString();
            foreach (var (view, card) in cardItems) view.SetOwned(PlayerProfile.IsUnlocked(card), PlayerProfile.Gold >= card.unlockPrice);
            foreach (var (view, emote) in emoteItems) view.SetOwned(PlayerProfile.OwnsEmote(emote), PlayerProfile.Gold >= emote.price);
        }
    }
}
