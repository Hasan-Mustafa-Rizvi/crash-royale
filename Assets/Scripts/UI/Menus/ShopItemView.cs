using CrashRoyale.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.UI
{
    public class ShopItemView : MonoBehaviour
    {
        [SerializeField] private CardView card;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button buyButton;
        [SerializeField] private GameObject priceGroup;
        [SerializeField] private GameObject ownedBadge;

        public Button BuyButton => buyButton;
        public CardData Card { get; private set; }
        public EmoteData Emote { get; private set; }

        public void SetupCard(CardData data)
        {
            Card = data;
            if (card != null) card.SetCard(data);
            title.text = data.displayName;
            subtitle.text = data.description;
            priceText.text = data.unlockPrice.ToString();
        }

        public void SetupEmote(EmoteData emote)
        {
            Emote = emote;
            bool hasIcon = emote.icon != null;
            if (icon != null)
            {
                icon.gameObject.SetActive(hasIcon);
                if (hasIcon) icon.sprite = emote.icon;
            }

            if (iconText != null)
            {
                iconText.gameObject.SetActive(!hasIcon);
                iconText.text = emote.text;
            }

            title.text = emote.text;
            subtitle.gameObject.SetActive(false);
            priceText.text = emote.price.ToString();
        }

        public void SetOwned(bool owned, bool affordable)
        {
            buyButton.interactable = !owned;
            if (priceGroup != null) priceGroup.SetActive(!owned);
            if (ownedBadge != null) ownedBadge.SetActive(owned);
            priceText.color = affordable ? Color.white : new Color(1f, 0.55f, 0.55f);
        }
    }
}
