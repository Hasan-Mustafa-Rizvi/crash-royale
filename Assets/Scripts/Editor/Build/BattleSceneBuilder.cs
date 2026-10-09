using CrashRoyale.Battle;
using CrashRoyale.UI;
using CrashRoyale.Visual;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CrashRoyale.EditorTools
{
    internal static class BattleSceneBuilder
    {
        /// <summary>World Y of the river center. Leaves room for the hand at the bottom and the HUD at the top.</summary>
        public const float ArenaY = 1.35f;

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = BuildCamera();
            var layout = BuildArena();
            var towers = BuildTowers(layout);

            var managerGo = new GameObject("BattleManager", typeof(BattleManager));
            var redAi = managerGo.AddComponent<AIController>();
            var blueAi = managerGo.AddComponent<AIController>();
            var manager = managerGo.GetComponent<BattleManager>();
            EditorKit.Set(manager, "layout", layout);
            EditorKit.Set(manager, "assets", ContentBuilder.Assets);
            EditorKit.Set(manager, "towers", towers);
            EditorKit.Set(manager, "redAI", redAi);
            EditorKit.Set(manager, "blueAI", blueAi);

            var ghost = BuildGhost();
            UIKit.EventSystem();
            BuildHud(camera, ghost);

            EditorSceneManager.SaveScene(scene, $"{Paths.Scenes}/Battle.unity");
        }

        private static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraRig));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.05f, 0.09f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 50f;

            var light = new GameObject("Global Light 2D", typeof(Light2D)).GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            return cam;
        }

        private static ArenaLayout BuildArena()
        {
            var background = new GameObject("ArenaBackground", typeof(SpriteRenderer));
            background.transform.position = new Vector3(0f, ArenaY, 0f);
            var sr = background.GetComponent<SpriteRenderer>();
            sr.sprite = EditorKit.Sprite($"{Paths.Arena}/BattleArena.png");
            sr.sortingOrder = -100;

            var layoutGo = new GameObject("ArenaLayout", typeof(ArenaLayout));
            layoutGo.transform.position = new Vector3(0f, ArenaY, 0f);
            var layout = layoutGo.GetComponent<ArenaLayout>();
            layout.halfWidth = 4.55f;
            layout.halfHeight = 6.4f;
            layout.riverHalfThickness = 0.5f;
            layout.laneX = 2.85f;
            layout.bridgeHalfWidth = 0.48f;
            layout.princessY = 4f;
            layout.kingY = 5.46f;
            layout.pocketDepth = 2.3f;

            // Walls: field edges plus the river segments around the two bridges.
            var walls = new GameObject("Walls");
            walls.transform.SetParent(layoutGo.transform, false);
            float w = layout.halfWidth, h = layout.halfHeight, t = 0.6f;
            Wall(walls, "Left", new Vector2(-w - t * 0.5f, 0f), new Vector2(t, h * 2f + t * 2f));
            Wall(walls, "Right", new Vector2(w + t * 0.5f, 0f), new Vector2(t, h * 2f + t * 2f));
            Wall(walls, "Top", new Vector2(0f, h + t * 0.5f), new Vector2(w * 2f + t * 2f, t));
            Wall(walls, "Bottom", new Vector2(0f, -h - t * 0.5f), new Vector2(w * 2f + t * 2f, t));

            float river = layout.riverHalfThickness * 2f;
            float bridgeInner = layout.laneX - layout.bridgeHalfWidth;
            float bridgeOuter = layout.laneX + layout.bridgeHalfWidth;
            Wall(walls, "RiverLeft", new Vector2(-(bridgeOuter + w) * 0.5f, 0f), new Vector2(w - bridgeOuter, river));
            Wall(walls, "RiverMiddle", Vector2.zero, new Vector2(bridgeInner * 2f, river));
            Wall(walls, "RiverRight", new Vector2((bridgeOuter + w) * 0.5f, 0f), new Vector2(w - bridgeOuter, river));
            return layout;
        }

        private static void Wall(GameObject parent, string name, Vector2 localPosition, Vector2 size)
        {
            var go = EditorKit.Child(parent, name, typeof(BoxCollider2D));
            go.transform.localPosition = localPosition;
            go.GetComponent<BoxCollider2D>().size = size;
        }

        private static Tower[] BuildTowers(ArenaLayout layout)
        {
            var root = new GameObject("Towers");
            var towers = new Tower[6];
            int i = 0;
            foreach (var team in new[] { Team.Blue, Team.Red })
            {
                towers[i++] = PlaceTower(root, ContentBuilder.KingTowerPrefab, team, -1, layout.TowerSpot(team, true, -1));
                towers[i++] = PlaceTower(root, ContentBuilder.PrincessTowerPrefab, team, 0, layout.TowerSpot(team, false, 0));
                towers[i++] = PlaceTower(root, ContentBuilder.PrincessTowerPrefab, team, 1, layout.TowerSpot(team, false, 1));
            }

            return towers;
        }

        private static Tower PlaceTower(GameObject parent, GameObject prefab, Team team, int lane, Vector2 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent.transform, true);
            go.transform.position = position;
            var tower = go.GetComponent<Tower>();
            go.name = $"{team} {(lane < 0 ? "King" : lane == 0 ? "Left Princess" : "Right Princess")}";
            EditorKit.Set(tower, "team", (int)team);
            EditorKit.Set(tower, "lane", lane);

            // Show the right color in the editor too.
            var body = go.transform.Find("Body").GetComponent<SpriteRenderer>();
            var so = new SerializedObject(tower);
            body.sprite = (Sprite)so.FindProperty(team == Team.Blue ? "blueSprite" : "redSprite").objectReferenceValue;
            return tower;
        }

        private static DeployGhost BuildGhost()
        {
            var go = new GameObject("DeployGhost", typeof(DeployGhost));

            var preview = EditorKit.Child(go, "Preview", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            preview.sortingOrder = 50;

            var footprint = EditorKit.Child(go, "Footprint", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            footprint.sprite = UIKit.S("soft_circle");
            footprint.sortingOrder = 49;

            var zones = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var zone = new GameObject($"ForbiddenZone{i}", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                zone.transform.SetParent(go.transform, true);
                zone.sprite = UIKit.S("white");
                zone.sortingOrder = -50;
                zone.color = new Color(1f, 0.15f, 0.2f, 0.22f);
                zones[i] = zone;
            }

            var marker = new GameObject("InvalidMarker", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            marker.transform.SetParent(go.transform, true);
            marker.sprite = UIKit.S("ring");
            marker.sortingOrder = 55;

            var ghost = go.GetComponent<DeployGhost>();
            EditorKit.Set(ghost, "unitPreview", preview);
            EditorKit.Set(ghost, "footprint", footprint);
            EditorKit.Set(ghost, "forbiddenZones", zones);
            EditorKit.Set(ghost, "invalidMarker", marker);
            return ghost;
        }

        // ---------------------------------------------------------------- HUD

        private static void BuildHud(Camera camera, DeployGhost ghost)
        {
            var canvas = UIKit.Canvas("HUD");
            var catcher = UIKit.Image(canvas.transform, "ArenaTapCatcher", UIKit.S("white"), new Color(0f, 0f, 0f, 0f), false, true);
            catcher.preserveAspect = false;
            catcher.rectTransform.Fill();
            var tapCatcher = catcher.gameObject.AddComponent<ArenaTapCatcher>();

            var safe = UIKit.SafeArea(canvas.transform).transform;
            var hud = safe.gameObject.AddComponent<BattleHUD>();

            // Enemy badge (top left)
            var enemy = UIKit.Panel(safe, "EnemyBadge", "panel_dark", null, false);
            enemy.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(390f, 128f));
            var redName = UIKit.Text(enemy.transform, "Name", "AI (Normal)", 40, new Color(1f, 0.62f, 0.62f), TextAlignmentOptions.Left);
            redName.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(346f, 56f));
            redName.enableAutoSizing = true;
            redName.fontSizeMin = 22;
            redName.fontSizeMax = 40;
            var redCrowns = CrownRow(enemy.transform, "crown_red", new Vector2(26f, -66f), 52f);

            // Timer (top right)
            var timer = UIKit.Panel(safe, "TimerBadge", "panel_dark", null, false);
            timer.rectTransform.At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(250f, 128f));
            var caption = UIKit.Text(timer.transform, "Caption", "TIME LEFT", 26, UIKit.Muted);
            caption.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(230f, 34f));
            var timerText = UIKit.Text(timer.transform, "Time", "2:00", 64, Color.white);
            timerText.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(230f, 76f));

            var pauseButton = UIKit.IconButton(safe, "PauseButton", "button_purple", "icon_pause", new Vector2(100f, 100f));
            ((RectTransform)pauseButton.transform).At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -160f), new Vector2(100f, 100f));

            // Hand
            var hand = UIKit.Panel(safe, "Hand", "panel");
            hand.rectTransform.Band(true, 430f, -6f, -6f, -6f);
            var handUi = hand.gameObject.AddComponent<CardHandUI>();

            var nextLabel = UIKit.Text(hand.transform, "NextLabel", "NEXT", 30, UIKit.Muted);
            nextLabel.rectTransform.At(new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(92f, -22f), new Vector2(150f, 40f));
            var next = UIKit.Card(hand.transform, "NextCard", new Vector2(124f, 155f), false, false, true, false, false);
            ((RectTransform)next.transform).At(new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(92f, -66f), new Vector2(124f, 155f));

            var slots = new CardSlotUI[3];
            for (int i = 0; i < 3; i++)
            {
                var slotRoot = UIKit.Rect(hand.transform, $"Slot{i}");
                slotRoot.At(new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(320f + i * 262f, -20f), new Vector2(244f, 305f));
                var group = slotRoot.gameObject.AddComponent<CanvasGroup>();
                var lift = UIKit.Rect(slotRoot, "Lift").Fill();
                var view = UIKit.Card(lift, "Card", new Vector2(244f, 305f), false, false, true, false, false);
                ((RectTransform)view.transform).Fill();
                view.GetComponent<Image>().raycastTarget = true;

                var slot = slotRoot.gameObject.AddComponent<CardSlotUI>();
                EditorKit.Set(slot, "view", view);
                EditorKit.Set(slot, "lift", lift);
                EditorKit.Set(slot, "group", group);
                slots[i] = slot;
            }

            // Elixir bar
            var bar = UIKit.Rect(hand.transform, "ElixirBar");
            bar.Band(true, 62f, 24f, 150f, 28f);
            var barBg = UIKit.Image(bar, "Background", UIKit.S("rounded_white"), new Color(0.07f, 0.03f, 0.1f, 0.95f), true);
            barBg.rectTransform.Fill(-6, -6, -6, -6);
            var charging = UIKit.Image(bar, "Charging", UIKit.S("white"), new Color(0.93f, 0.4f, 0.95f, 0.35f));
            charging.preserveAspect = false;
            charging.type = Image.Type.Filled;
            charging.fillMethod = Image.FillMethod.Horizontal;
            charging.rectTransform.Fill();
            var full = UIKit.Image(bar, "Full", UIKit.S("white"), new Color(0.88f, 0.3f, 0.92f, 1f));
            full.preserveAspect = false;
            full.type = Image.Type.Filled;
            full.fillMethod = Image.FillMethod.Horizontal;
            full.rectTransform.Fill();
            for (int i = 1; i < 10; i++)
            {
                var tick = UIKit.Image(bar, $"Tick{i}", UIKit.S("white"), new Color(0.08f, 0.02f, 0.1f, 0.85f));
                tick.preserveAspect = false;
                tick.rectTransform.anchorMin = new Vector2(i / 10f, 0f);
                tick.rectTransform.anchorMax = new Vector2(i / 10f, 1f);
                tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tick.rectTransform.sizeDelta = new Vector2(5f, 0f);
                tick.rectTransform.anchoredPosition = Vector2.zero;
            }

            var drop = UIKit.Image(bar, "Drop", UIKit.S("elixir_drop"), Color.white);
            drop.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-62f, 6f), new Vector2(118f, 118f));
            var amount = UIKit.Text(drop.transform, "Amount", "5", 58, Color.white);
            amount.rectTransform.Fill(0, 0, 26, 0);

            var doubleBadge = UIKit.Image(bar, "DoubleBadge", UIKit.S("pill"), UIKit.Pink, true);
            doubleBadge.rectTransform.At(new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(110f, 52f));
            var dbText = UIKit.Text(doubleBadge.transform, "Text", "2x", 38, Color.white);
            dbText.rectTransform.Fill(0, 0, 0, 4);
            doubleBadge.gameObject.SetActive(false);

            var elixirUi = bar.gameObject.AddComponent<ElixirBarUI>();
            EditorKit.Set(elixirUi, "fullFill", full);
            EditorKit.Set(elixirUi, "chargingFill", charging);
            EditorKit.Set(elixirUi, "amount", amount);
            EditorKit.Set(elixirUi, "amountBadge", drop.transform);
            EditorKit.Set(elixirUi, "doubleBadge", doubleBadge.gameObject);

            EditorKit.Set(handUi, "slots", slots);
            EditorKit.Set(handUi, "nextCard", next);
            EditorKit.Set(handUi, "ghost", ghost);
            EditorKit.Set(handUi, "handArea", hand.rectTransform);
            EditorKit.Set(handUi, "worldCamera", camera);
            EditorKit.Set(tapCatcher, "hand", handUi);

            // Player badge (above the hand, left)
            var player = UIKit.Panel(safe, "PlayerBadge", "panel_dark", null, false);
            player.rectTransform.At(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 446f), new Vector2(390f, 118f));
            var blueName = UIKit.Text(player.transform, "Name", "You", 40, new Color(0.62f, 0.82f, 1f), TextAlignmentOptions.Left);
            blueName.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -8f), new Vector2(346f, 54f));
            blueName.enableAutoSizing = true;
            blueName.fontSizeMin = 22;
            blueName.fontSizeMax = 40;
            var blueCrowns = CrownRow(player.transform, "crown_blue", new Vector2(26f, -60f), 50f);

            // Emotes
            var emotePanel = BuildEmotes(safe);

            // Toast and announcer
            BuildToast(safe, new Vector2(0f, 720f));
            var announcer = BuildAnnouncer(safe);

            // Overlays
            var intro = BuildIntro(canvas.transform, out var introBlue, out var introRed);
            var pause = BuildPause(canvas.transform);
            var results = BuildResults(canvas.transform);

            EditorKit.Set(hud, "timerText", timerText);
            EditorKit.Set(hud, "timerCaption", caption);
            EditorKit.Set(hud, "blueName", blueName);
            EditorKit.Set(hud, "redName", redName);
            EditorKit.Set(hud, "blueCrowns", blueCrowns);
            EditorKit.Set(hud, "redCrowns", redCrowns);
            EditorKit.Set(hud, "intro", intro);
            EditorKit.Set(hud, "introBlue", introBlue);
            EditorKit.Set(hud, "introRed", introRed);
            EditorKit.Set(hud, "announcer", announcer);
            EditorKit.Set(hud, "results", results);
            EditorKit.Set(hud, "pauseMenu", pause);
            EditorKit.Set(hud, "pauseButton", pauseButton);
        }

        private static Image[] CrownRow(Transform parent, string sprite, Vector2 position, float size)
        {
            var row = UIKit.Rect(parent, "Crowns");
            row.At(new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(size * 3.6f, size));
            var crowns = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var crown = UIKit.Image(row, $"Crown{i}", UIKit.S(sprite), new Color(1f, 1f, 1f, 0.2f));
                crown.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * size * 1.2f, 0f), new Vector2(size, size));
                crowns[i] = crown;
            }

            return crowns;
        }

        private static EmotePanel BuildEmotes(Transform safe)
        {
            var root = UIKit.Rect(safe, "Emotes").Fill();
            var panel = root.gameObject.AddComponent<EmotePanel>();

            var toggle = UIKit.IconButton(root, "EmoteButton", "button_purple", "icon_emote", new Vector2(110f, 110f), 0.66f);
            ((RectTransform)toggle.transform).At(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 446f), new Vector2(110f, 110f));

            var menu = UIKit.Panel(root, "Menu", "panel_dark");
            menu.rectTransform.At(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 570f), new Vector2(620f, 150f));
            var layout = menu.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 18, 18);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            var fitter = menu.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var template = UIKit.Button(menu.transform, "EmoteTemplate", null, "button_purple", new Vector2(112f, 112f));
            var icon = UIKit.Image(template.transform, "Icon", UIKit.S("icon_emote"), Color.white);
            icon.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(82f, 82f));
            var text = UIKit.Text(template.transform, "Text", "GG!", 34, Color.white);
            text.rectTransform.Fill(6, 6, 6, 10);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18;
            text.fontSizeMax = 34;
            var emoteButton = template.gameObject.AddComponent<EmoteButton>();
            EditorKit.Set(emoteButton, "button", template);
            EditorKit.Set(emoteButton, "icon", icon);
            EditorKit.Set(emoteButton, "text", text);

            var blueBubble = Bubble(root, "BlueBubble", new Vector2(0.5f, 0f), new Vector2(-250f, 650f));
            var redBubble = Bubble(root, "RedBubble", new Vector2(0.5f, 1f), new Vector2(230f, -262f));

            EditorKit.Set(panel, "toggleButton", toggle);
            EditorKit.Set(panel, "menu", menu.gameObject);
            EditorKit.Set(panel, "buttonContainer", menu.rectTransform);
            EditorKit.Set(panel, "buttonTemplate", emoteButton);
            EditorKit.Set(panel, "blueBubble", blueBubble);
            EditorKit.Set(panel, "redBubble", redBubble);
            return panel;
        }

        private static EmoteBubble Bubble(Transform parent, string name, Vector2 anchor, Vector2 position)
        {
            var bubble = UIKit.Panel(parent, name, "bubble", null, false);
            bubble.rectTransform.At(anchor, new Vector2(0.5f, 0.5f), position, new Vector2(240f, 150f));
            var group = bubble.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var icon = UIKit.Image(bubble.transform, "Icon", UIKit.S("icon_emote"), Color.white);
            icon.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 118f));
            var text = UIKit.Text(bubble.transform, "Text", "GG!", 54, Color.white);
            text.rectTransform.Fill(10, 10, 8, 14);
            var view = bubble.gameObject.AddComponent<EmoteBubble>();
            EditorKit.Set(view, "group", group);
            EditorKit.Set(view, "icon", icon);
            EditorKit.Set(view, "text", text);
            return view;
        }

        public static Toast BuildToast(Transform parent, Vector2 position)
        {
            var toastBg = UIKit.Panel(parent, "Toast", "panel_dark", null, false);
            toastBg.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), position, new Vector2(760f, 96f));
            var group = toastBg.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var label = UIKit.Text(toastBg.transform, "Text", "Not enough elixir!", 40, UIKit.Cream);
            label.rectTransform.Fill(20, 20, 8, 12);
            label.enableAutoSizing = true;
            label.fontSizeMin = 22;
            label.fontSizeMax = 40;
            var toast = toastBg.gameObject.AddComponent<Toast>();
            EditorKit.Set(toast, "label", label);
            EditorKit.Set(toast, "group", group);
            return toast;
        }

        private static Announcer BuildAnnouncer(Transform parent)
        {
            var root = UIKit.Rect(parent, "Announcer");
            root.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1000f, 260f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var label = UIKit.Text(root, "Text", "BATTLE!", 150, UIKit.Gold);
            label.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1000f, 180f));
            var sub = UIKit.Text(root, "Sub", "", 52, Color.white);
            sub.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -88f), new Vector2(1000f, 70f));
            var announcer = root.gameObject.AddComponent<Announcer>();
            EditorKit.Set(announcer, "label", label);
            EditorKit.Set(announcer, "subLabel", sub);
            EditorKit.Set(announcer, "group", group);
            return announcer;
        }

        private static CanvasGroup BuildIntro(Transform parent, out TextMeshProUGUI blue, out TextMeshProUGUI red)
        {
            var dim = UIKit.Image(parent, "Intro", UIKit.S("white"), new Color(0.04f, 0.01f, 0.07f, 0.8f), false, false);
            dim.preserveAspect = false;
            dim.rectTransform.Fill();
            var group = dim.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            var redPlate = UIKit.Panel(dim.transform, "RedPlate", "button_red", null, false);
            redPlate.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(760f, 170f));
            red = UIKit.Text(redPlate.transform, "Name", "AI (Normal)", 64, Color.white);
            red.rectTransform.Fill(30, 30, 10, 18);

            var vs = UIKit.Text(dim.transform, "VS", "VS", 170, UIKit.Gold);
            vs.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 220f));

            var bluePlate = UIKit.Panel(dim.transform, "BluePlate", "button_blue", null, false);
            bluePlate.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(760f, 170f));
            blue = UIKit.Text(bluePlate.transform, "Name", "You", 64, Color.white);
            blue.rectTransform.Fill(30, 30, 10, 18);
            return group;
        }

        private static PauseMenu BuildPause(Transform parent)
        {
            var (root, panel) = UIKit.Modal(parent, "Pause", new Vector2(720f, 820f));
            var title = UIKit.Text(panel, "Title", "PAUSED", 96, UIKit.Gold);
            title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(640f, 120f));

            var resume = UIKit.Button(panel, "Resume", "RESUME", "button_green", new Vector2(520f, 140f), 58);
            ((RectTransform)resume.transform).At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(520f, 140f));
            var restart = UIKit.Button(panel, "Restart", "RESTART", "button_purple", new Vector2(520f, 130f), 52);
            ((RectTransform)restart.transform).At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(520f, 130f));
            var quit = UIKit.Button(panel, "Quit", "MAIN MENU", "button_red", new Vector2(520f, 130f), 52);
            ((RectTransform)quit.transform).At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(520f, 130f));
            var note = UIKit.Text(panel, "Note", "Leaving gives no rewards", 30, UIKit.Muted);
            note.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(640f, 44f));

            root.SetActive(false);
            var holder = new GameObject("PauseMenu", typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var menu = holder.AddComponent<PauseMenu>();
            EditorKit.Set(menu, "panel", root);
            EditorKit.Set(menu, "resumeButton", resume);
            EditorKit.Set(menu, "restartButton", restart);
            EditorKit.Set(menu, "quitButton", quit);
            return menu;
        }

        private static ResultScreen BuildResults(Transform parent)
        {
            var (root, panel) = UIKit.Modal(parent, "Results", new Vector2(900f, 1180f));
            var title = UIKit.Text(panel, "Title", "VICTORY!", 120, UIKit.Gold);
            title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(840f, 150f));
            var subtitle = UIKit.Text(panel, "Subtitle", "Won on crowns", 38, UIKit.Muted);
            subtitle.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(840f, 56f));

            var redRow = UIKit.Panel(panel, "RedRow", "panel_dark", null, false);
            redRow.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -275f), new Vector2(800f, 150f));
            var redName = UIKit.Text(redRow.transform, "Name", "AI (Normal)", 44, new Color(1f, 0.62f, 0.62f), TextAlignmentOptions.Left);
            redName.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(420f, 70f));
            var redCrowns = ResultCrowns(redRow.transform, "crown_red");

            var blueRow = UIKit.Panel(panel, "BlueRow", "panel_dark", null, false);
            blueRow.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -445f), new Vector2(800f, 150f));
            var blueName = UIKit.Text(blueRow.transform, "Name", "You", 44, new Color(0.62f, 0.82f, 1f), TextAlignmentOptions.Left);
            blueName.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(420f, 70f));
            var blueCrowns = ResultCrowns(blueRow.transform, "crown_blue");

            var rewards = UIKit.Rect(panel, "Rewards");
            rewards.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -650f), new Vector2(800f, 120f));
            var goldGroup = UIKit.IconLabel(rewards, "Gold", "coin", "+120", 84f, out var goldText);
            goldGroup.At(new Vector2(0.25f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 90f));
            var trophyGroup = UIKit.IconLabel(rewards, "Trophies", $"{Paths.Branding}/Trophy.png", "+30", 84f, out var trophyText);
            trophyGroup.At(new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 90f));

            var again = UIKit.Button(panel, "PlayAgain", "PLAY AGAIN", "button_green", new Vector2(640f, 150f), 60);
            ((RectTransform)again.transform).At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(640f, 150f));
            var menu = UIKit.Button(panel, "MainMenu", "MAIN MENU", "button_purple", new Vector2(640f, 130f), 52);
            ((RectTransform)menu.transform).At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(640f, 130f));

            var holder = new GameObject("ResultScreen", typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var screen = holder.AddComponent<ResultScreen>();
            EditorKit.Set(screen, "panel", root);
            EditorKit.Set(screen, "title", title);
            EditorKit.Set(screen, "subtitle", subtitle);
            EditorKit.Set(screen, "blueName", blueName);
            EditorKit.Set(screen, "redName", redName);
            EditorKit.Set(screen, "blueCrowns", blueCrowns);
            EditorKit.Set(screen, "redCrowns", redCrowns);
            EditorKit.Set(screen, "rewardsRow", rewards.gameObject);
            EditorKit.Set(screen, "goldText", goldText);
            EditorKit.Set(screen, "trophyText", trophyText);
            EditorKit.Set(screen, "playAgainButton", again);
            EditorKit.Set(screen, "menuButton", menu);
            root.SetActive(false);
            return screen;
        }

        private static Image[] ResultCrowns(Transform parent, string sprite)
        {
            var crowns = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var crown = UIKit.Image(parent, $"Crown{i}", UIKit.S(sprite), Color.white);
                crown.rectTransform.At(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f - (2 - i) * 92f, 0f), new Vector2(84f, 84f));
                crowns[i] = crown;
            }

            return crowns;
        }
    }
}
