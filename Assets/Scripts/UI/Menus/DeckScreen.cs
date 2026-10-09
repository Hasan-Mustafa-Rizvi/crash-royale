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
    /// <summary>
    /// Pick the four cards you take into battle. Tap a card in the collection, then tap the deck slot it should replace.
    /// The deck is saved immediately and used by the next battle.
    /// </summary>
    public class DeckScreen : MonoBehaviour
    {
        [SerializeField] private CardView[] deckSlots = new CardView[4];
        [SerializeField] private RectTransform collectionRoot;
        [SerializeField] private CardView collectionTemplate;
        [SerializeField] private CardInfoPanel info;
        [SerializeField] private TMP_Text averageCost;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private Button backButton;
        [SerializeField] private Button shopButton;

        private readonly List<CardView> collection = new List<CardView>();
        private CardDatabase database;
        private List<string> deck;
        private CardData selected;

        private void Start()
        {
            database = CardDatabase.Load();
            PlayerProfile.EnsureInitialized(database);
            AudioManager.PlayMusic(MusicTrack.Menu);

            deck = new List<string>(PlayerProfile.Deck);
            collectionTemplate.gameObject.SetActive(false);

            foreach (var card in database.cards)
            {
                var view = Instantiate(collectionTemplate, collectionRoot);
                view.gameObject.SetActive(true);
                view.SetCard(card);
                var captured = card;
                view.GetComponent<Button>().onClick.AddListener(() => OnCollectionTapped(captured));
                collection.Add(view);
            }

            for (int i = 0; i < deckSlots.Length; i++)
            {
                int slot = i;
                deckSlots[i].GetComponent<Button>().onClick.AddListener(() => OnDeckSlotTapped(slot));
            }

            backButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.MainMenu));
            shopButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.Shop));

            Select(database.Get(deck.Count > 0 ? deck[0] : null));
        }

        private void OnCollectionTapped(CardData card)
        {
            Select(card);
            AudioManager.Play(PlayerProfile.IsUnlocked(card) ? Sfx.CardSelect : Sfx.Invalid);
        }

        private void OnDeckSlotTapped(int slot)
        {
            var current = database.Get(deck[slot]);
            bool canSwapIn = selected != null && PlayerProfile.IsUnlocked(selected) && !deck.Contains(selected.id);

            if (canSwapIn)
            {
                deck[slot] = selected.id;
                if (PlayerProfile.SetDeck(deck, database))
                {
                    AudioManager.Play(Sfx.Deploy);
                    UITween.Pop(deckSlots[slot].transform, 0.7f, 1.1f, 0.25f);
                    Toast.Show($"{selected.displayName} is in your deck");
                }
                else
                {
                    deck = new List<string>(PlayerProfile.Deck);
                }

                Select(selected);
                return;
            }

            Select(current);
            AudioManager.Play(Sfx.CardSelect);
        }

        private void Select(CardData card)
        {
            selected = card;
            if (card != null) info.Show(card, PlayerProfile.IsUnlocked(card), deck.Contains(card.id));
            Refresh();
        }

        private void Refresh()
        {
            float total = 0f;
            for (int i = 0; i < deckSlots.Length; i++)
            {
                var card = i < deck.Count ? database.Get(deck[i]) : null;
                deckSlots[i].SetCard(card);
                deckSlots[i].SetHighlighted(card != null && card == selected);
                if (card != null) total += card.elixirCost;
            }

            averageCost.text = $"Average elixir: {total / Mathf.Max(1, deck.Count):0.0}";
            if (goldText != null) goldText.text = PlayerProfile.Gold.ToString();

            foreach (var view in collection)
            {
                var card = view.Card;
                bool unlocked = PlayerProfile.IsUnlocked(card);
                view.SetLocked(!unlocked, unlocked ? null : card.unlockPrice.ToString());
                view.SetHighlighted(card == selected);
                view.SetTag(deck.Contains(card.id) ? "IN DECK" : null);
            }
        }
    }
}
