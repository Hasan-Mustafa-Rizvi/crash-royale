using UnityEngine;
using UnityEngine.EventSystems;

namespace CrashRoyale.UI
{
    /// <summary>Invisible full-screen target behind the HUD that turns taps on the arena into deploy requests.</summary>
    public class ArenaTapCatcher : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private CardHandUI hand;

        public void OnPointerClick(PointerEventData eventData) => hand.OnArenaTapped(eventData.position);
    }
}
