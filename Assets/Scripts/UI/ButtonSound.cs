using CrashRoyale.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    /// <summary>Click sound plus a small press squash on any button.</summary>
    [RequireComponent(typeof(Button))]
    public class ButtonSound : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private string sound = Sfx.Click;
        [SerializeField] private float pressedScale = 0.94f;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(() => AudioManager.Play(sound));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button.interactable) transform.localScale = new Vector3(pressedScale, pressedScale, 1f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = Vector3.one;
        }

        private void OnDisable() => transform.localScale = Vector3.one;
    }
}
