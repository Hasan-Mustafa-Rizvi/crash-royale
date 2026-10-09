using CrashRoyale.Audio;
using CrashRoyale.Battle;
using UnityEngine;

namespace CrashRoyale.UI
{
    /// <summary>The player's hand: card slots, the next-card preview and deployment by drag or tap.</summary>
    public class CardHandUI : MonoBehaviour
    {
        [SerializeField] private CardSlotUI[] slots = new CardSlotUI[0];
        [SerializeField] private CardView nextCard;
        [SerializeField] private DeployGhost ghost;
        [SerializeField] private RectTransform handArea;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Team team = Team.Blue;

        private BattleManager battle;
        private DeckCycle deck;
        private int selected = -1;
        private int dragging = -1;

        private void Start()
        {
            battle = BattleManager.Instance;
            if (battle == null)
            {
                enabled = false;
                return;
            }

            if (worldCamera == null) worldCamera = Camera.main;
            deck = battle.Deck(team);
            deck.Changed += Refresh;
            battle.StateChanged += OnStateChanged;

            for (int i = 0; i < slots.Length; i++) slots[i].Bind(this, i);
            Refresh();
        }

        private void OnDestroy()
        {
            if (deck != null) deck.Changed -= Refresh;
            if (battle != null) battle.StateChanged -= OnStateChanged;
        }

        private void Update()
        {
            if (deck == null) return;

            float elixir = battle.Bank(team).Current;
            for (int i = 0; i < slots.Length; i++)
            {
                var card = deck.Hand(i);
                slots[i].View.SetCharge(card == null ? 0f : Mathf.Clamp01(elixir / card.elixirCost));
            }

            if (nextCard != null) nextCard.SetCharge(0.999f);
        }

        private void Refresh()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var card = deck.Hand(i);
                if (slots[i].View.Card != card)
                {
                    slots[i].View.SetCard(card);
                    slots[i].Bounce();
                }
            }

            if (nextCard != null) nextCard.SetCard(deck.Next);
        }

        private void OnStateChanged(BattleState state)
        {
            if (state == BattleState.Ended) CancelInput();
        }

        public void ToggleSelect(int index)
        {
            if (!battle.IsRunning || battle.Paused) return;

            if (selected == index)
            {
                Deselect();
                return;
            }

            Deselect();
            selected = index;
            slots[index].SetSelected(true);
            ghost.ShowZones(team);
            AudioManager.Play(Sfx.CardSelect);
        }

        public void BeginDrag(int index, Vector2 screenPosition)
        {
            if (!battle.IsRunning || battle.Paused) return;

            Deselect();
            dragging = index;
            slots[index].SetDragging(true);
            Drag(screenPosition);
        }

        public void Drag(Vector2 screenPosition)
        {
            if (dragging < 0) return;

            var card = deck.Hand(dragging);
            if (card == null || IsOverHand(screenPosition))
            {
                ghost.HideUnit();
                ghost.ShowZones(team);
                return;
            }

            Vector2 world = ScreenToWorld(screenPosition);
            ghost.Show(card, team, world, battle.CanDeploy(team, card, world));
        }

        public void EndDrag(Vector2 screenPosition)
        {
            if (dragging < 0) return;

            int index = dragging;
            dragging = -1;
            slots[index].SetDragging(false);

            if (!IsOverHand(screenPosition)) TryDeploy(index, ScreenToWorld(screenPosition));
            ghost.Hide();
        }

        /// <summary>Called by the invisible full-screen catcher behind the HUD.</summary>
        public void OnArenaTapped(Vector2 screenPosition)
        {
            if (selected < 0 || dragging >= 0) return;
            if (TryDeploy(selected, ScreenToWorld(screenPosition))) Deselect();
        }

        private bool TryDeploy(int index, Vector2 world)
        {
            var card = deck.Hand(index);
            if (card == null || !battle.IsRunning) return false;

            if (!battle.Bank(team).CanAfford(card.elixirCost))
            {
                Toast.Show("Not enough elixir!");
                AudioManager.Play(Sfx.Invalid);
                return false;
            }

            if (!battle.Layout.IsDeployable(team, world))
            {
                ghost.FlashInvalid(world);
                AudioManager.Play(Sfx.Invalid);
                return false;
            }

            return battle.TryDeploy(team, index, world);
        }

        private void Deselect()
        {
            if (selected >= 0 && selected < slots.Length) slots[selected].SetSelected(false);
            selected = -1;
            ghost.Hide();
        }

        private void CancelInput()
        {
            if (dragging >= 0) slots[dragging].SetDragging(false);
            dragging = -1;
            Deselect();
        }

        private bool IsOverHand(Vector2 screenPosition)
        {
            return handArea != null && RectTransformUtility.RectangleContainsScreenPoint(handArea, screenPosition, null);
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            var point = new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z);
            return worldCamera.ScreenToWorldPoint(point);
        }
    }
}
