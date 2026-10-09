using UnityEngine;
using UnityEngine.EventSystems;

namespace CrashRoyale.UI
{
    /// <summary>One card in the battle hand. Tap to select (then tap the arena), or drag it straight onto the arena.</summary>
    public class CardSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] private CardView view;
        [SerializeField] private RectTransform lift;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float selectedLift = 26f;

        private CardHandUI hand;
        private int index;
        private bool dragged;

        public CardView View => view;

        public void Bind(CardHandUI owner, int slotIndex)
        {
            hand = owner;
            index = slotIndex;
        }

        public void SetSelected(bool selected)
        {
            if (lift != null) lift.anchoredPosition = new Vector2(0f, selected ? selectedLift : 0f);
            view.SetHighlighted(selected);
        }

        public void SetDragging(bool dragging)
        {
            if (group != null) group.alpha = dragging ? 0.4f : 1f;
        }

        public void Bounce() => UITween.Pop(view.transform, 0.75f, 1.08f, 0.25f);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragged)
            {
                dragged = false;
                return;
            }

            hand.ToggleSelect(index);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragged = true;
            hand.BeginDrag(index, eventData.position);
        }

        public void OnDrag(PointerEventData eventData) => hand.Drag(eventData.position);

        public void OnEndDrag(PointerEventData eventData)
        {
            hand.EndDrag(eventData.position);
            dragged = false;
        }
    }
}
