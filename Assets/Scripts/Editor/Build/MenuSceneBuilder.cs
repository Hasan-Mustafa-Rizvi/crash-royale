using CrashRoyale.UI;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CrashRoyale.EditorTools
{
    internal static class MenuSceneBuilder
    {
        public static void BuildAll()
        {
            BuildMainMenu();
            BuildDeck();
            BuildShop();
        }

        private static Canvas NewMenuScene(string backgroundPath, Color tint, float aspect)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.05f, 0.12f);
            camera.orthographic = true;
            UIKit.EventSystem();

            var canvas = UIKit.Canvas("UI");
            UIKit.Background(canvas.transform, backgroundPath, tint, aspect);
            var vignette = UIKit.Image(canvas.transform, "Vignette", UIKit.S("vignette"), Color.white);
            vignette.preserveAspect = false;
            vignette.rectTransform.Fill();
            return canvas;
        }

        private static (RectTransform root, TextMeshProUGUI value) GoldCounter(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            var bg = UIKit.Panel(parent, "Gold", "panel_dark", null, false);
            bg.rectTransform.At(anchor, pivot, position, new Vector2(250f, 96f));
            var icon = UIKit.Image(bg.transform, "Icon", UIKit.S("coin"), Color.white);
            icon.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(72f, 72f));
            var value = UIKit.Text(bg.transform, "Value", "150", 48, UIKit.Cream, TextAlignmentOptions.Right);
            value.rectTransform.At(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, -2f), new Vector2(150f, 70f));
            return (bg.rectTransform, value);
        }

        // ---------------------------------------------------------------- main menu

        private static void BuildMainMenu()
        {
            var canvas = NewMenuScene($"{Paths.Arena}/MenuBackground.png", Color.white, 1024f / 1536f);
            var safe = UIKit.SafeArea(canvas.transform).transform;
            var ui = safe.gameObject.AddComponent<MainMenuUI>();

            // Profile bar
            var bar = UIKit.Panel(safe, "ProfileBar", "panel_dark", null, false);
            bar.rectTransform.Band(false, 150f, 16f, 16f, 16f);
            var name = UIKit.Text(bar.transform, "Name", "Player", 52, UIKit.Cream, TextAlignmentOptions.Left);
            name.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -14f), new Vector2(430f, 70f));
            name.enableAutoSizing = true;
            name.fontSizeMin = 26;
            name.fontSizeMax = 52;
            var record = UIKit.Text(bar.transform, "Record", "W 0   L 0   D 0", 30, UIKit.Muted, TextAlignmentOptions.Left);
            record.rectTransform.At(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 16f), new Vector2(430f, 44f));
            var edit = UIKit.IconButton(bar.transform, "EditName", "button_purple", "icon_edit", new Vector2(80f, 80f));
            ((RectTransform)edit.transform).At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(470f, 0f), new Vector2(80f, 80f));

            var trophyGroup = UIKit.IconLabel(bar.transform, "Trophies", $"{Paths.Branding}/Trophy.png", "0", 72f, out var trophies);
            trophyGroup.At(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-300f, 0f), new Vector2(200f, 72f));
            var goldGroup = UIKit.IconLabel(bar.transform, "Gold", "coin", "150", 66f, out var gold);
            goldGroup.At(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(200f, 66f));

            // Logo
            var logo = UIKit.Image(safe, "Logo", EditorKit.Sprite($"{Paths.Branding}/Logo.png"), Color.white);
            logo.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(640f, 640f));

            // Difficulty
            var opponent = UIKit.Text(safe, "OpponentLabel", "CHOOSE YOUR OPPONENT", 36, UIKit.Cream);
            opponent.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 50f));
            var labels = new[] { "EASY", "NORMAL", "HARD" };
            var buttons = new Button[3];
            var backgrounds = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var button = UIKit.Button(safe, $"Difficulty{labels[i]}", labels[i], "pill", new Vector2(270f, 96f), 40);
                ((RectTransform)button.transform).At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 290f, -40f), new Vector2(270f, 96f));
                buttons[i] = button;
                backgrounds[i] = button.GetComponent<Image>();
            }

            var hint = UIKit.Text(safe, "DifficultyHint", "Defends, counters and pushes when it has elixir.", 30, UIKit.Muted);
            hint.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -122f), new Vector2(940f, 50f));

            // Battle
            var battleImage = UIKit.Image(safe, "BattleButton", EditorKit.Sprite($"{Paths.Branding}/PlayButton.png"), Color.white, false, true);
            battleImage.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(640f, 252f));
            var battle = battleImage.gameObject.AddComponent<Button>();
            battle.targetGraphic = battleImage;
            battleImage.gameObject.AddComponent<ButtonSound>();

            // Bottom navigation
            var navLabels = new[] { ("Deck", "icon_deck", "DECK"), ("Shop", "coin", "SHOP"), ("Help", "icon_help", "HOW TO"), ("Settings", "icon_edit", "SETTINGS") };
            var nav = new Button[4];
            for (int i = 0; i < navLabels.Length; i++)
            {
                var (key, icon, label) = navLabels[i];
                var b = UIKit.Button(safe, $"{key}Button", null, "button_purple", new Vector2(220f, 220f));
                ((RectTransform)b.transform).At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((i - 1.5f) * 240f, 70f), new Vector2(220f, 220f));
                var glyph = UIKit.Image(b.transform, "Icon", key == "Settings" ? EditorKit.Sprite($"{Paths.Branding}/SettingsGear.png") : UIKit.S(icon), Color.white);
                glyph.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(120f, 120f));
                var text = UIKit.Text(b.transform, "Label", label, 36, UIKit.Cream);
                text.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(200f, 50f));
                text.enableAutoSizing = true;
                text.fontSizeMin = 20;
                text.fontSizeMax = 36;
                nav[i] = b;
            }

            var quit = UIKit.IconButton(safe, "QuitButton", "button_red", "icon_close", new Vector2(90f, 90f));
            ((RectTransform)quit.transform).At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -186f), new Vector2(90f, 90f));

            var version = UIKit.Text(safe, "Credit", "Made by Hasan Mustafa Rizvi", 24, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center, false);
            version.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(900f, 36f));

            var namePrompt = BuildNamePrompt(canvas.transform);
            var settings = BuildSettings(canvas.transform);
            var (howTo, howToClose) = BuildHowToPlay(canvas.transform);
            BattleSceneBuilder.BuildToast(canvas.transform, new Vector2(0f, 360f));

            EditorKit.Set(ui, "playerName", name);
            EditorKit.Set(ui, "trophies", trophies);
            EditorKit.Set(ui, "gold", gold);
            EditorKit.Set(ui, "record", record);
            EditorKit.Set(ui, "editNameButton", edit);
            EditorKit.Set(ui, "battleButton", battle);
            EditorKit.Set(ui, "deckButton", nav[0]);
            EditorKit.Set(ui, "shopButton", nav[1]);
            EditorKit.Set(ui, "howToPlayButton", nav[2]);
            EditorKit.Set(ui, "settingsButton", nav[3]);
            EditorKit.Set(ui, "quitButton", quit);
            EditorKit.Set(ui, "difficultyButtons", buttons);
            EditorKit.Set(ui, "difficultyBackgrounds", backgrounds);
            EditorKit.Set(ui, "difficultyHint", hint);
            EditorKit.Set(ui, "namePrompt", namePrompt);
            EditorKit.Set(ui, "settings", settings);
            EditorKit.Set(ui, "howToPlay", howTo);
            EditorKit.Set(ui, "howToPlayClose", howToClose);
            EditorKit.Set(ui, "logo", logo.transform);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), $"{Paths.Scenes}/MainMenu.unity");
        }

        private static NamePrompt BuildNamePrompt(Transform parent)
        {
            var (root, panel) = UIKit.Modal(parent, "NamePrompt", new Vector2(900f, 680f));
            var title = UIKit.Text(panel, "Title", "WELCOME TO THE ARENA!\nWHAT SHOULD WE CALL YOU?", 48, UIKit.Gold);
            title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(820f, 150f));
            var input = UIKit.Input(panel, "Input", new Vector2(720f, 120f), "Your name");
            ((RectTransform)input.transform).At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(720f, 120f));
            var error = UIKit.Text(panel, "Error", "", 32, new Color(1f, 0.5f, 0.5f));
            error.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -54f), new Vector2(800f, 50f));
            var confirm = UIKit.Button(panel, "Confirm", "LET'S GO", "button_green", new Vector2(400f, 130f), 54);
            ((RectTransform)confirm.transform).At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120f, 50f), new Vector2(400f, 130f));
            var cancel = UIKit.Button(panel, "Cancel", "CANCEL", "button_purple", new Vector2(300f, 120f), 46);
            ((RectTransform)cancel.transform).At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-250f, 55f), new Vector2(300f, 120f));

            var holder = new GameObject("NamePromptController", typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var prompt = holder.AddComponent<NamePrompt>();
            EditorKit.Set(prompt, "panel", root);
            EditorKit.Set(prompt, "title", title);
            EditorKit.Set(prompt, "input", input);
            EditorKit.Set(prompt, "error", error);
            EditorKit.Set(prompt, "confirmButton", confirm);
            EditorKit.Set(prompt, "cancelButton", cancel);
            root.SetActive(false);
            return prompt;
        }

        private static SettingsPanel BuildSettings(Transform parent)
        {
            var (root, panel) = UIKit.Modal(parent, "Settings", new Vector2(900f, 1060f));
            var title = UIKit.Text(panel, "Title", "SETTINGS", 80, UIKit.Gold);
            title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(800f, 110f));
            var close = UIKit.IconButton(panel, "Close", "button_red", "icon_close", new Vector2(96f, 96f));
            ((RectTransform)close.transform).At(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-20f, -20f), new Vector2(96f, 96f));

            var musicLabel = UIKit.Text(panel, "MusicLabel", "MUSIC", 42, UIKit.Cream, TextAlignmentOptions.Left);
            musicLabel.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -190f), new Vector2(400f, 60f));
            var music = UIKit.Slider(panel, "MusicSlider", new Vector2(760f, 80f));
            ((RectTransform)music.transform).At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(760f, 80f));

            var sfxLabel = UIKit.Text(panel, "SfxLabel", "SOUND EFFECTS", 42, UIKit.Cream, TextAlignmentOptions.Left);
            sfxLabel.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -380f), new Vector2(600f, 60f));
            var sfx = UIKit.Slider(panel, "SfxSlider", new Vector2(760f, 80f));
            ((RectTransform)sfx.transform).At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -450f), new Vector2(760f, 80f));

            var reset = UIKit.Button(panel, "Reset", "RESET PROGRESS", "button_red", new Vector2(620f, 120f), 44);
            ((RectTransform)reset.transform).At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -600f), new Vector2(620f, 120f));
            var resetLabel = reset.GetComponentInChildren<TextMeshProUGUI>();

            var credits = UIKit.Text(panel, "Credits",
                "Art: Tiny Swords by Pixel Frog (pixelfrog-assets.itch.io)\nFont: Black Han Sans (SIL OFL)\nSound effects synthesized for this game",
                26, UIKit.Muted, TextAlignmentOptions.Center, false);
            credits.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(820f, 130f));
            var version = UIKit.Text(panel, "Version", "v1.0.0", 26, UIKit.Muted, TextAlignmentOptions.Center, false);
            version.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(400f, 40f));

            var holder = new GameObject("SettingsController", typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var settings = holder.AddComponent<SettingsPanel>();
            EditorKit.Set(settings, "panel", root);
            EditorKit.Set(settings, "musicSlider", music);
            EditorKit.Set(settings, "sfxSlider", sfx);
            EditorKit.Set(settings, "closeButton", close);
            EditorKit.Set(settings, "resetButton", reset);
            EditorKit.Set(settings, "resetLabel", resetLabel);
            EditorKit.Set(settings, "versionText", version);
            root.SetActive(false);
            return settings;
        }

        private static (GameObject, Button) BuildHowToPlay(Transform parent)
        {
            var (root, panel) = UIKit.Modal(parent, "HowToPlay", new Vector2(940f, 1500f));
            var title = UIKit.Text(panel, "Title", "HOW TO PLAY", 80, UIKit.Gold);
            title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(840f, 110f));
            var close = UIKit.IconButton(panel, "Close", "button_red", "icon_close", new Vector2(96f, 96f));
            ((RectTransform)close.transform).At(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-20f, -20f), new Vector2(96f, 96f));

            string body =
                "<color=#FFD24D>DEPLOY</color>\nDrag a card onto your half of the arena, or tap a card and then tap where it should go. The red area is off limits.\n\n" +
                "<color=#FFD24D>ELIXIR</color>\nEvery card costs elixir. It refills on its own, twice as fast in the last minute. Don't sit at 10 - that's wasted elixir.\n\n" +
                "<color=#FFD24D>CROWNS</color>\nEach side tower you destroy is a crown. Take the king tower and you win instantly. Your king wakes up when it gets hit or loses a side tower.\n\n" +
                "<color=#FFD24D>TIME</color>\nMost crowns after 2:00 wins. Tied? Overtime: the next crown wins. Still tied after that, it's a draw.\n\n" +
                "<color=#FFD24D>CROSSING</color>\nTroops can only cross the river at the two bridges. Once you take a side tower you can deploy deeper in that lane.";
            var text = UIKit.Text(panel, "Body", body, 36, UIKit.Cream, TextAlignmentOptions.TopLeft, false);
            text.rectTransform.Fill(60, 60, 170, 60);
            text.lineSpacing = 6;

            root.SetActive(false);
            return (root, close);
        }

        // ---------------------------------------------------------------- deck

        private static void BuildDeck()
        {
            var canvas = NewMenuScene($"{Paths.Arena}/MenuBackground.png", new Color(0.45f, 0.38f, 0.5f), 1024f / 1536f);
            var safe = UIKit.SafeArea(canvas.transform).transform;
            var screen = safe.gameObject.AddComponent<DeckScreen>();

            var back = UIKit.IconButton(safe, "Back", "button_purple", "icon_back", new Vector2(110f, 110f));
            ((RectTransform)back.transform).At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(110f, 110f));
            var title = UIKit.Text(safe, "Title", "BATTLE DECK", 72, UIKit.Gold);
            title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(600f, 100f));
            var (_, goldValue) = GoldCounter(safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -27f));

            var deckPanel = UIKit.Panel(safe, "DeckPanel", "panel", null, false);
            deckPanel.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1040f, 400f));
            var slots = new CardView[4];
            for (int i = 0; i < 4; i++)
            {
                var card = UIKit.Card(deckPanel.transform, $"Slot{i}", new Vector2(220f, 275f), true, false, false, false, true);
                ((RectTransform)card.transform).At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 240f, -30f), new Vector2(220f, 275f));
                slots[i] = card;
            }

            var average = UIKit.Text(deckPanel.transform, "AverageCost", "Average elixir: 3.5", 32, UIKit.Muted);
            average.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(900f, 50f));

            // Info
            var infoPanel = UIKit.Panel(safe, "Info", "panel_dark", null, false);
            infoPanel.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -570f), new Vector2(1040f, 470f));
            var infoCard = UIKit.Card(infoPanel.transform, "Card", new Vector2(240f, 300f), false, true, false, false, false);
            ((RectTransform)infoCard.transform).At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -34f), new Vector2(240f, 300f));
            var infoTitle = UIKit.Text(infoPanel.transform, "Title", "BRUMP", 56, UIKit.Gold, TextAlignmentOptions.Left);
            infoTitle.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(310f, -26f), new Vector2(700f, 70f));
            var infoDescription = UIKit.Text(infoPanel.transform, "Description", "", 30, UIKit.Cream, TextAlignmentOptions.TopLeft, false);
            infoDescription.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(310f, -100f), new Vector2(700f, 100f));
            var infoStats = UIKit.Text(infoPanel.transform, "Stats", "", 30, UIKit.Muted, TextAlignmentOptions.TopLeft, false);
            infoStats.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(310f, -200f), new Vector2(700f, 230f));
            infoStats.lineSpacing = -4;
            var infoStatus = UIKit.Text(infoPanel.transform, "Status", "", 30, UIKit.Gold, TextAlignmentOptions.Left);
            infoStatus.rectTransform.At(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 22f), new Vector2(980f, 50f));
            var info = infoPanel.gameObject.AddComponent<CardInfoPanel>();
            EditorKit.Set(info, "card", infoCard);
            EditorKit.Set(info, "title", infoTitle);
            EditorKit.Set(info, "description", infoDescription);
            EditorKit.Set(info, "stats", infoStats);
            EditorKit.Set(info, "status", infoStatus);

            // Collection
            var collectionTitle = UIKit.Text(safe, "CollectionTitle", "YOUR CARDS", 46, UIKit.Cream);
            collectionTitle.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1060f), new Vector2(800f, 60f));
            var grid = UIKit.Rect(safe, "Collection");
            grid.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1130f), new Vector2(760f, 600f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(220f, 275f);
            layout.spacing = new Vector2(30f, 26f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.UpperCenter;
            var template = UIKit.Card(grid, "CardTemplate", new Vector2(220f, 275f), true, true, false, true, true);

            var shop = UIKit.Button(safe, "ShopButton", "GET MORE CARDS", "button_gold", new Vector2(520f, 120f), 46);
            ((RectTransform)shop.transform).At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(520f, 120f));

            BattleSceneBuilder.BuildToast(canvas.transform, new Vector2(0f, 200f));

            EditorKit.Set(screen, "deckSlots", slots);
            EditorKit.Set(screen, "collectionRoot", grid);
            EditorKit.Set(screen, "collectionTemplate", template);
            EditorKit.Set(screen, "info", info);
            EditorKit.Set(screen, "averageCost", average);
            EditorKit.Set(screen, "goldText", goldValue);
            EditorKit.Set(screen, "backButton", back);
            EditorKit.Set(screen, "shopButton", shop);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), $"{Paths.Scenes}/Deck.unity");
        }

        // ---------------------------------------------------------------- shop

        private static void BuildShop()
        {
            var canvas = NewMenuScene($"{Paths.Arena}/ShopBackground.png", new Color(0.62f, 0.55f, 0.62f), 1024f / 1536f);
            var safe = UIKit.SafeArea(canvas.transform).transform;
            var screen = safe.gameObject.AddComponent<ShopScreen>();

            var back = UIKit.IconButton(safe, "Back", "button_purple", "icon_back", new Vector2(110f, 110f));
            ((RectTransform)back.transform).At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(110f, 110f));
            var titleArt = UIKit.Image(safe, "Title", EditorKit.Sprite($"{Paths.Branding}/ShopTitle.png"), Color.white);
            titleArt.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 10f), new Vector2(480f, 320f));
            var (goldRoot, goldValue) = GoldCounter(safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -27f));

            var cardsTitle = UIKit.Text(safe, "CardsTitle", "NEW CARDS", 50, UIKit.Gold);
            cardsTitle.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(800f, 70f));
            var cardList = UIKit.Rect(safe, "CardList");
            cardList.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -380f), new Vector2(1020f, 680f));
            var cardLayout = cardList.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.spacing = 24;
            cardLayout.childAlignment = TextAnchor.UpperCenter;
            cardLayout.childControlWidth = false;
            cardLayout.childControlHeight = false;
            cardLayout.childForceExpandWidth = false;
            cardLayout.childForceExpandHeight = false;
            var cardTemplate = ShopItem(cardList, "CardItem", true);

            var emotesTitle = UIKit.Text(safe, "EmotesTitle", "EMOTES", 50, UIKit.Gold);
            emotesTitle.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1080f), new Vector2(800f, 70f));
            var emoteList = UIKit.Rect(safe, "EmoteList");
            emoteList.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1160f), new Vector2(1020f, 380f));
            var emoteLayout = emoteList.gameObject.AddComponent<HorizontalLayoutGroup>();
            emoteLayout.spacing = 24;
            emoteLayout.childAlignment = TextAnchor.UpperCenter;
            emoteLayout.childControlWidth = false;
            emoteLayout.childControlHeight = false;
            emoteLayout.childForceExpandWidth = false;
            emoteLayout.childForceExpandHeight = false;
            var emoteTemplate = ShopItem(emoteList, "EmoteItem", false);

            var deck = UIKit.Button(safe, "DeckButton", "EDIT DECK", "button_gold", new Vector2(460f, 120f), 46);
            ((RectTransform)deck.transform).At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(460f, 120f));

            BattleSceneBuilder.BuildToast(canvas.transform, new Vector2(0f, 220f));

            EditorKit.Set(screen, "goldText", goldValue);
            EditorKit.Set(screen, "goldBadge", goldRoot);
            EditorKit.Set(screen, "cardList", cardList);
            EditorKit.Set(screen, "emoteList", emoteList);
            EditorKit.Set(screen, "cardTemplate", cardTemplate);
            EditorKit.Set(screen, "emoteTemplate", emoteTemplate);
            EditorKit.Set(screen, "backButton", back);
            EditorKit.Set(screen, "deckButton", deck);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), $"{Paths.Scenes}/Shop.unity");
        }

        private static ShopItemView ShopItem(Transform parent, string name, bool isCard)
        {
            var size = isCard ? new Vector2(1000f, 320f) : new Vector2(310f, 360f);
            var panel = UIKit.Panel(parent, name, "panel", null, false);
            panel.rectTransform.sizeDelta = size;
            var view = panel.gameObject.AddComponent<ShopItemView>();

            Button buy;
            TextMeshProUGUI title, subtitle, price;
            GameObject priceGroup;

            if (isCard)
            {
                var card = UIKit.Card(panel.transform, "Card", new Vector2(208f, 260f), false, false, false, false, false);
                ((RectTransform)card.transform).At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(208f, 260f));
                EditorKit.Set(view, "card", card);

                title = UIKit.Text(panel.transform, "Title", "Archers", 54, UIKit.Gold, TextAlignmentOptions.Left);
                title.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(270f, -30f), new Vector2(680f, 70f));
                subtitle = UIKit.Text(panel.transform, "Subtitle", "", 30, UIKit.Cream, TextAlignmentOptions.TopLeft, false);
                subtitle.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(270f, -104f), new Vector2(420f, 190f));

                buy = UIKit.Button(panel.transform, "Buy", null, "button_green", new Vector2(250f, 130f));
                ((RectTransform)buy.transform).At(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 36f), new Vector2(250f, 130f));
            }
            else
            {
                var icon = UIKit.Image(panel.transform, "Icon", UIKit.S("icon_emote"), Color.white);
                icon.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(150f, 150f));
                var iconText = UIKit.Text(panel.transform, "IconText", "GG!", 60, Color.white);
                iconText.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(260f, 150f));
                EditorKit.Set(view, "icon", icon);
                EditorKit.Set(view, "iconText", iconText);

                title = UIKit.Text(panel.transform, "Title", "Emote", 36, UIKit.Gold);
                title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(280f, 46f));
                subtitle = UIKit.Text(panel.transform, "Subtitle", "", 22, UIKit.Muted, TextAlignmentOptions.Center, false);
                subtitle.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(280f, 34f));

                buy = UIKit.Button(panel.transform, "Buy", null, "button_green", new Vector2(240f, 100f));
                ((RectTransform)buy.transform).At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(240f, 100f));
            }

            var group = UIKit.IconLabel(buy.transform, "Price", "coin", "250", isCard ? 64f : 56f, out price);
            group.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(200f, 64f));
            group.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            priceGroup = group.gameObject;

            var owned = UIKit.Text(buy.transform, "Owned", "OWNED", isCard ? 46 : 40, Color.white);
            owned.rectTransform.Fill(8, 8, 8, 14);
            owned.gameObject.SetActive(false);

            EditorKit.Set(view, "title", title);
            EditorKit.Set(view, "subtitle", subtitle);
            EditorKit.Set(view, "priceText", price);
            EditorKit.Set(view, "buyButton", buy);
            EditorKit.Set(view, "priceGroup", priceGroup);
            EditorKit.Set(view, "ownedBadge", owned.gameObject);
            return view;
        }
    }
}
