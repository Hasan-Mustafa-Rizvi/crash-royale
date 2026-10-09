using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrashRoyale.Audio;
using CrashRoyale.Battle;
using CrashRoyale.Data;
using CrashRoyale.Visual;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CrashRoyale.EditorTools
{
    /// <summary>
    /// Imports the art, builds unit skins, effects, projectiles, towers, the unit prefab, all cards,
    /// the card database and the sound library.
    /// </summary>
    internal static class ContentBuilder
    {
        public const float UnitPpu = 88f;
        private static readonly Vector2 FeetPivot = new Vector2(0.5f, 0.31f);

        public static readonly Color BlueTeam = new Color(0.27f, 0.62f, 1f);
        public static readonly Color RedTeam = new Color(1f, 0.32f, 0.36f);

        // Filled in by Build() and used by the scene builders.
        public static BattleAssets Assets;
        public static GameObject PrincessTowerPrefab;
        public static GameObject KingTowerPrefab;
        public static TMP_FontAsset Font;
        public static Material FontOutlined;

        private static Dictionary<string, Sprite> dead;
        private static Dictionary<string, Sprite> explosions;

        public static void Build()
        {
            ImportUiArt();
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Paths.Font);
            FontOutlined = BuildOutlinedFontMaterial();

            var fx = BuildEffects();
            var projectiles = BuildProjectiles(fx);
            var skins = BuildSkins();
            var unitPrefab = BuildUnitPrefab();
            BuildTowers(projectiles["tower_arrow"]);

            var database = BuildCards(skins, projectiles);
            Assets = EditorKit.LoadOrCreate<BattleAssets>(Paths.Data + "/BattleAssets.asset");
            Assets.database = database;
            Assets.unitPrefab = unitPrefab.GetComponent<Unit>();
            Assets.spawnFx = fx["spawn"];
            Assets.deathFx = fx["death"];
            Assets.explosionFx = fx["explosion"];
            Assets.towerExplosionFx = fx["explosion"];
            Assets.hitFx = fx["hit"];
            EditorUtility.SetDirty(Assets);

            BuildSoundLibrary();
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- art import

        private static void ImportUiArt()
        {
            var center = new Vector2(0.5f, 0.5f);

            // 9-sliced UI pieces (border in pixels: left, bottom, right, top)
            ArtPipeline.Single($"{Paths.UI}/panel.png", 100, center, false, new Vector4(40, 40, 40, 40));
            ArtPipeline.Single($"{Paths.UI}/panel_dark.png", 100, center, false, new Vector4(30, 30, 30, 30));
            ArtPipeline.Single($"{Paths.UI}/bubble.png", 100, center, false, new Vector4(30, 30, 30, 30));
            foreach (var b in new[] { "gold", "blue", "green", "red", "purple" })
                ArtPipeline.Single($"{Paths.UI}/button_{b}.png", 100, center, false, new Vector4(40, 40, 40, 40));
            ArtPipeline.Single($"{Paths.UI}/pill.png", 100, center, false, new Vector4(30, 30, 30, 30));
            ArtPipeline.Single($"{Paths.UI}/rounded_white.png", 100, center, false, new Vector4(10, 10, 10, 10));
            foreach (var r in new[] { "common", "rare", "epic" })
                ArtPipeline.Single($"{Paths.UI}/card_frame_{r}.png", 100, center, false, new Vector4(24, 24, 24, 24));
            ArtPipeline.Single($"{Paths.UI}/card_glow.png", 100, center, false, new Vector4(26, 26, 26, 26));
            ArtPipeline.Single($"{Paths.UI}/card_charge.png", 100, center, false, new Vector4(18, 18, 18, 18));

            // World-space sprites
            ArtPipeline.Single($"{Paths.UI}/white.png", 4, center, false);
            ArtPipeline.Single($"{Paths.UI}/soft_circle.png", 64, center, false);
            ArtPipeline.Single($"{Paths.UI}/ring.png", 96, center, false);
            ArtPipeline.Single($"{Paths.UI}/hp_bg.png", 100, center, false, new Vector4(6, 5, 6, 5));
            ArtPipeline.Single($"{Paths.UI}/hp_fill.png", 100, new Vector2(0f, 0.5f), false, new Vector4(5, 5, 5, 5));

            // Icons
            foreach (var icon in new[] { "elixir_drop", "crown_blue", "crown_red", "crown_gold", "coin", "lock", "icon_pause", "icon_close", "icon_back", "icon_edit", "icon_help", "icon_emote", "icon_deck", "icon_battle", "vignette" })
                ArtPipeline.Single($"{Paths.UI}/{icon}.png", 100, center, false);

            foreach (var path in Directory.GetFiles(Paths.Emotes, "*.png"))
                ArtPipeline.Single(path.Replace('\\', '/'), 100, center, false);
            foreach (var path in Directory.GetFiles(Paths.Cards, "*.png"))
                ArtPipeline.Single(path.Replace('\\', '/'), 100, center, false);

            foreach (var path in Directory.GetFiles(Paths.Branding, "*.png"))
                ArtPipeline.Single(path.Replace('\\', '/'), 100, center, false);

            // Arena: the field center (between the two halves) is the pivot so the sprite sits on the ArenaLayout.
            ArtPipeline.Background($"{Paths.Arena}/BattleArena.png", 78f, new Vector2(0.5f, 1f - 916f / 1745f), 2048);
            ArtPipeline.Background($"{Paths.Arena}/MenuBackground.png", 100f, center, 2048);
            ArtPipeline.Background($"{Paths.Arena}/ShopBackground.png", 100f, center, 2048);
        }

        private static Material BuildOutlinedFontMaterial()
        {
            if (Font == null)
            {
                Debug.LogError("[Builder] Font asset missing at " + Paths.Font);
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(Paths.FontOutlined);
            if (material == null)
            {
                material = new Material(Font.material);
                AssetDatabase.CreateAsset(material, Paths.FontOutlined);
            }
            else
            {
                material.shader = Font.material.shader;
                material.CopyPropertiesFromMaterial(Font.material);
            }

            material.EnableKeyword("OUTLINE_ON");
            material.EnableKeyword("UNDERLAY_ON");
            material.SetFloat("_OutlineWidth", 0.22f);
            material.SetColor("_OutlineColor", new Color(0.16f, 0.07f, 0.16f, 1f));
            material.SetFloat("_FaceDilate", 0.12f);
            material.SetColor("_UnderlayColor", new Color(0.08f, 0.02f, 0.1f, 0.75f));
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", -0.85f);
            material.SetFloat("_UnderlayDilate", 0.25f);
            material.SetFloat("_UnderlaySoftness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- effects

        private static Dictionary<string, GameObject> BuildEffects()
        {
            string ts = Paths.TinySwords;
            dead = ArtPipeline.Grid($"{ts}/Units/Dead.png", 128, 128, UnitPpu, new Vector2(0.5f, 0.3f));
            explosions = ArtPipeline.Grid($"{ts}/Effects/Explosions.png", 192, 192, UnitPpu, new Vector2(0.5f, 0.5f));

            var result = new Dictionary<string, GameObject>
            {
                ["spawn"] = FlipbookFx("SpawnFx", Frames(dead, "Dead", 0), 20f, 30, new Color(1f, 1f, 1f, 0.85f)),
                ["death"] = FlipbookFx("DeathFx", Frames(dead, "Dead", 0).Concat(Frames(dead, "Dead", 1)).ToArray(), 14f, 30, Color.white),
                ["explosion"] = FlipbookFx("ExplosionFx", Frames(explosions, "Explosions", 0), 18f, 35, Color.white),
                ["hit"] = FlipbookFx("HitFx", Frames(explosions, "Explosions", 0).Take(4).ToArray(), 26f, 34, new Color(1f, 0.95f, 0.8f, 0.9f))
            };
            return result;
        }

        private static GameObject FlipbookFx(string name, Sprite[] frames, float fps, int order, Color tint)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(SpriteAnimator));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = frames.FirstOrDefault();
            sr.sortingOrder = order;
            sr.color = tint;
            var animator = go.GetComponent<SpriteAnimator>();
            var anim = new SpriteAnim { frames = frames, fps = fps, loop = false };
            animator.Configure(anim, true);
            return EditorKit.SavePrefab(go, $"{Paths.Prefabs}/FX/{name}.prefab");
        }

        // ---------------------------------------------------------------- projectiles

        private static Dictionary<string, Projectile> BuildProjectiles(Dictionary<string, GameObject> fx)
        {
            string ts = Paths.TinySwords;
            var arrow = ArtPipeline.Grid($"{ts}/Projectiles/Arrow.png", 64, 64, UnitPpu, new Vector2(0.5f, 0.45f));
            var dynamite = ArtPipeline.Grid($"{ts}/Projectiles/Dynamite.png", 64, 64, UnitPpu, new Vector2(0.5f, 0.5f));

            return new Dictionary<string, Projectile>
            {
                ["arrow"] = BuildProjectile("Arrow", arrow.GetValueOrDefault("Arrow_0_0"), 1f, 9f, 0.35f, true, 0f, null, 1f, Sfx.ArrowHit),
                ["tower_arrow"] = BuildProjectile("TowerArrow", arrow.GetValueOrDefault("Arrow_0_0"), 1.25f, 11f, 0.2f, true, 0f, null, 1f, Sfx.ArrowHit),
                ["dynamite"] = BuildProjectile("Dynamite", dynamite.GetValueOrDefault("Dynamite_0_0"), 1f, 5.5f, 1.3f, false, 560f, fx["explosion"], 0.75f, Sfx.Explosion)
            };
        }

        private static Projectile BuildProjectile(string name, Sprite sprite, float scale, float speed, float arc, bool face, float spin, GameObject impactFx, float impactScale, string sfx)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(Projectile));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 20;
            go.transform.localScale = Vector3.one * scale;

            var p = go.GetComponent<Projectile>();
            EditorKit.Set(p, "speed", speed);
            EditorKit.Set(p, "arcHeight", arc);
            EditorKit.Set(p, "faceDirection", face);
            EditorKit.Set(p, "spinDegreesPerSecond", spin);
            EditorKit.Set(p, "impactFx", impactFx);
            EditorKit.Set(p, "impactFxScale", impactScale);
            EditorKit.Set(p, "impactSfx", sfx);
            return EditorKit.SavePrefab(go, $"{Paths.Prefabs}/Projectiles/{name}.prefab").GetComponent<Projectile>();
        }

        // ---------------------------------------------------------------- unit skins

        private class Sheet
        {
            public string Path;
            public int Frame;
            public Vector2 Pivot;
        }

        private static Dictionary<string, UnitSkin> BuildSkins()
        {
            string ts = Paths.TinySwords;
            var skins = new Dictionary<string, UnitSkin>();

            foreach (var team in new[] { "Blue", "Red" })
            {
                var warrior = ArtPipeline.Grid($"{ts}/Units/Warrior_{team}.png", 192, 192, UnitPpu, FeetPivot);
                skins[$"warrior_{team}"] = Skin($"Warrior_{team}", s =>
                {
                    s.idle = Anim(warrior, $"Warrior_{team}", 0, 8f, true);
                    s.run = Anim(warrior, $"Warrior_{team}", 1, 10f, true);
                    s.attackSide = Anim(warrior, $"Warrior_{team}", 2, 12f, false, 3);
                    s.attackDown = Anim(warrior, $"Warrior_{team}", 4, 12f, false, 3);
                    s.attackUp = Anim(warrior, $"Warrior_{team}", 6, 12f, false, 3);
                });

                var torch = ArtPipeline.Grid($"{ts}/Units/Torch_{team}.png", 192, 192, UnitPpu, FeetPivot);
                skins[$"torch_{team}"] = Skin($"Torch_{team}", s =>
                {
                    s.idle = Anim(torch, $"Torch_{team}", 0, 9f, true);
                    s.run = Anim(torch, $"Torch_{team}", 1, 12f, true);
                    s.attackSide = Anim(torch, $"Torch_{team}", 2, 14f, false, 3);
                    s.attackDown = Anim(torch, $"Torch_{team}", 3, 14f, false, 3);
                    s.attackUp = Anim(torch, $"Torch_{team}", 4, 14f, false, 3);
                });

                var tnt = ArtPipeline.Grid($"{ts}/Units/TNT_{team}.png", 192, 192, UnitPpu, FeetPivot);
                skins[$"tnt_{team}"] = Skin($"TNT_{team}", s =>
                {
                    s.idle = Anim(tnt, $"TNT_{team}", 0, 8f, true);
                    s.run = Anim(tnt, $"TNT_{team}", 1, 10f, true);
                    s.attackSide = Anim(tnt, $"TNT_{team}", 2, 12f, false, 4);
                });

                var archer = ArtPipeline.Grid($"{ts}/Units/Archer_{team}.png", 192, 192, UnitPpu, FeetPivot);
                skins[$"archer_{team}"] = Skin($"Archer_{team}", s =>
                {
                    s.idle = Anim(archer, $"Archer_{team}", 0, 8f, true);
                    s.run = Anim(archer, $"Archer_{team}", 1, 10f, true);
                    s.attackUp = Anim(archer, $"Archer_{team}", 2, 14f, false, 5);
                    s.attackSide = Anim(archer, $"Archer_{team}", 4, 14f, false, 5);
                    s.attackDown = Anim(archer, $"Archer_{team}", 6, 14f, false, 5);
                });

                var barrel = ArtPipeline.Grid($"{ts}/Units/Barrel_{team}.png", 128, 128, UnitPpu, new Vector2(0.5f, 0.24f));
                skins[$"barrel_{team}"] = Skin($"Barrel_{team}", s =>
                {
                    s.idle = Anim(barrel, $"Barrel_{team}", 0, 6f, true);
                    s.run = Anim(barrel, $"Barrel_{team}", 3, 12f, true);
                    s.special = Anim(barrel, $"Barrel_{team}", 5, 7f, false, 2);
                });

                // Optional art: the Lancer from the Tiny Swords Free Pack (not redistributable, so it may be missing).
                string lancerFolder = $"{Paths.FreePack}/Lancer/{team}";
                if (ArtPipeline.Exists($"{lancerFolder}/Lancer_Idle.png"))
                {
                    var lancerPivot = new Vector2(0.5f, 0.385f);
                    var idle = ArtPipeline.Grid($"{lancerFolder}/Lancer_Idle.png", 320, 320, UnitPpu, lancerPivot);
                    var run = ArtPipeline.Grid($"{lancerFolder}/Lancer_Run.png", 320, 320, UnitPpu, lancerPivot);
                    var side = ArtPipeline.Grid($"{lancerFolder}/Lancer_Right_Attack.png", 320, 320, UnitPpu, lancerPivot);
                    var up = ArtPipeline.Grid($"{lancerFolder}/Lancer_Up_Attack.png", 320, 320, UnitPpu, lancerPivot);
                    var down = ArtPipeline.Grid($"{lancerFolder}/Lancer_Down_Attack.png", 320, 320, UnitPpu, lancerPivot);
                    skins[$"lancer_{team}"] = Skin($"Lancer_{team}", s =>
                    {
                        s.idle = Anim(idle, "Lancer_Idle", 0, 10f, true);
                        s.run = Anim(run, "Lancer_Run", 0, 10f, true);
                        s.attackSide = Anim(side, "Lancer_Right_Attack", 0, 9f, false, 1);
                        s.attackUp = Anim(up, "Lancer_Up_Attack", 0, 9f, false, 1);
                        s.attackDown = Anim(down, "Lancer_Down_Attack", 0, 9f, false, 1);
                    });
                }
            }

            return skins;
        }

        private static UnitSkin Skin(string name, System.Action<UnitSkin> fill)
        {
            var skin = EditorKit.LoadOrCreate<UnitSkin>($"{Paths.Skins}/{name}.asset");
            skin.idle = new SpriteAnim();
            skin.run = new SpriteAnim();
            skin.attackSide = new SpriteAnim();
            skin.attackUp = new SpriteAnim();
            skin.attackDown = new SpriteAnim();
            skin.special = new SpriteAnim();
            fill(skin);
            EditorUtility.SetDirty(skin);
            return skin;
        }

        private static Sprite[] Frames(Dictionary<string, Sprite> sprites, string prefix, int row)
        {
            return sprites.Values
                .Where(s => s.name.StartsWith($"{prefix}_{row}_"))
                .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
                .ToArray();
        }

        private static SpriteAnim Anim(Dictionary<string, Sprite> sprites, string prefix, int row, float fps, bool loop, int eventFrame = -1)
        {
            var frames = Frames(sprites, prefix, row);
            if (frames.Length == 0) Debug.LogWarning($"[Builder] No frames for {prefix} row {row}");
            return new SpriteAnim { frames = frames, fps = fps, loop = loop, eventFrame = Mathf.Min(eventFrame, frames.Length - 1) };
        }

        // ---------------------------------------------------------------- unit prefab

        private static GameObject BuildUnitPrefab()
        {
            var root = new GameObject("Unit", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Health), typeof(Unit));
            var rb = root.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 6f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            root.GetComponent<CircleCollider2D>().radius = 0.22f;

            var body = EditorKit.Child(root, "Body", typeof(SpriteRenderer), typeof(SpriteAnimator));
            var sr = body.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 0;

            var bar = BuildHealthBar(root, 0.62f, 0.1f, false, false);
            bar.transform.localPosition = new Vector3(0f, 1.02f, 0f);

            var unit = root.GetComponent<Unit>();
            EditorKit.Set(unit, "body", sr);
            EditorKit.Set(unit, "animator", body.GetComponent<SpriteAnimator>());
            EditorKit.Set(unit, "healthBar", bar);
            EditorKit.Set(unit, "radius", 0.22f);
            EditorKit.Set(root.GetComponent<Health>(), "flashTarget", sr);

            return EditorKit.SavePrefab(root, $"{Paths.Prefabs}/Battle/Unit.prefab");
        }

        private static WorldHealthBar BuildHealthBar(GameObject parent, float width, float height, bool alwaysVisible, bool withLabel)
        {
            var go = EditorKit.Child(parent, "HealthBar", typeof(WorldHealthBar));
            var bg = EditorKit.Child(go, "Background", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            bg.sprite = EditorKit.Sprite($"{Paths.UI}/hp_bg.png");
            bg.drawMode = SpriteDrawMode.Sliced;
            bg.size = new Vector2(width + 0.07f, height + 0.07f);
            bg.sortingOrder = 40;

            var fill = EditorKit.Child(go, "Fill", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            fill.sprite = EditorKit.Sprite($"{Paths.UI}/hp_fill.png");
            fill.drawMode = SpriteDrawMode.Sliced;
            fill.size = new Vector2(width, height);
            fill.sortingOrder = 41;
            fill.transform.localPosition = new Vector3(-width * 0.5f, 0f, 0f);

            var bar = go.GetComponent<WorldHealthBar>();
            EditorKit.Set(bar, "background", bg);
            EditorKit.Set(bar, "fill", fill);
            EditorKit.Set(bar, "width", width);
            EditorKit.Set(bar, "height", height);
            EditorKit.Set(bar, "alwaysVisible", alwaysVisible);
            EditorKit.Set(bar, "blueColor", BlueTeam);
            EditorKit.Set(bar, "redColor", RedTeam);

            if (withLabel)
            {
                var labelGo = EditorKit.Child(go, "Label", typeof(TextMeshPro));
                labelGo.transform.localPosition = new Vector3(0f, height * 0.5f + 0.17f, 0f);
                var tmp = labelGo.GetComponent<TextMeshPro>();
                tmp.font = Font;
                if (FontOutlined != null) tmp.fontSharedMaterial = FontOutlined;
                tmp.fontSize = 3.2f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.text = "1400";
                tmp.sortingOrder = 42;
                tmp.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
                EditorKit.Set(bar, "label", tmp);
            }

            return bar;
        }

        // ---------------------------------------------------------------- towers

        private static void BuildTowers(Projectile arrow)
        {
            string ts = Paths.TinySwords;
            var towerPivot = new Vector2(0.5f, 0.16f);
            ArtPipeline.Single($"{ts}/Buildings/Tower_Blue.png", 100, towerPivot, true);
            ArtPipeline.Single($"{ts}/Buildings/Tower_Red.png", 100, towerPivot, true);
            ArtPipeline.Single($"{ts}/Buildings/Tower_Destroyed.png", 100, towerPivot, true);
            ArtPipeline.Single($"{ts}/Buildings/Castle_Blue.png", 125, towerPivot, true);
            ArtPipeline.Single($"{ts}/Buildings/Castle_Red.png", 125, towerPivot, true);
            ArtPipeline.Single($"{ts}/Buildings/Castle_Destroyed.png", 125, towerPivot, true);

            PrincessTowerPrefab = BuildTower("PrincessTower", Tower.Kind.Princess, "Tower", 0.62f, 1400, 52, 0.8f, 3.8f, 1.15f, 1.45f, 2.12f, 1.1f, arrow);
            KingTowerPrefab = BuildTower("KingTower", Tower.Kind.King, "Castle", 0.92f, 2400, 58, 1.0f, 3.6f, 0.95f, 1.2f, 1.92f, 1.5f, arrow);
        }

        private static GameObject BuildTower(string name, Tower.Kind kind, string art, float radius, int hp, int damage, float hitSpeed, float range, float aim, float fireHeight, float barHeight, float barWidth, Projectile arrow)
        {
            string ts = Paths.TinySwords;
            var root = new GameObject(name, typeof(CircleCollider2D), typeof(Health), typeof(Tower));
            root.GetComponent<CircleCollider2D>().radius = radius;

            var body = EditorKit.Child(root, "Body", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            body.sprite = EditorKit.Sprite($"{ts}/Buildings/{art}_Blue.png");
            body.sortingOrder = 0;

            var fire = EditorKit.Child(root, "FirePoint");
            fire.transform.localPosition = new Vector3(0f, fireHeight, 0f);

            var bar = BuildHealthBar(root, barWidth, 0.16f, true, true);
            bar.transform.localPosition = new Vector3(0f, barHeight, 0f);

            GameObject sleep = null;
            if (kind == Tower.Kind.King)
            {
                sleep = EditorKit.Child(root, "Sleeping", typeof(TextMeshPro));
                sleep.transform.localPosition = new Vector3(0.95f, 1.55f, 0f);
                var tmp = sleep.GetComponent<TextMeshPro>();
                tmp.font = Font;
                if (FontOutlined != null) tmp.fontSharedMaterial = FontOutlined;
                tmp.fontSize = 4f;
                tmp.text = "Zz";
                tmp.color = new Color(0.85f, 0.9f, 1f);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.sortingOrder = 42;
                tmp.rectTransform.sizeDelta = new Vector2(1f, 0.6f);
            }

            var tower = root.GetComponent<Tower>();
            EditorKit.Set(tower, "kind", (int)kind);
            EditorKit.Set(tower, "radius", radius);
            EditorKit.Set(tower, "body", body);
            EditorKit.Set(tower, "blueSprite", EditorKit.Sprite($"{ts}/Buildings/{art}_Blue.png"));
            EditorKit.Set(tower, "redSprite", EditorKit.Sprite($"{ts}/Buildings/{art}_Red.png"));
            EditorKit.Set(tower, "destroyedSprite", EditorKit.Sprite($"{ts}/Buildings/{art}_Destroyed.png"));
            EditorKit.Set(tower, "healthBar", bar);
            EditorKit.Set(tower, "projectilePrefab", arrow);
            EditorKit.Set(tower, "firePoint", fire.transform);
            EditorKit.Set(tower, "sleepIndicator", sleep);
            EditorKit.Set(tower, "maxHealth", hp);
            EditorKit.Set(tower, "damage", damage);
            EditorKit.Set(tower, "hitSpeed", hitSpeed);
            EditorKit.Set(tower, "range", range);
            EditorKit.Set(tower, "aimHeight", aim);
            EditorKit.Set(root.GetComponent<Health>(), "flashTarget", body);

            return EditorKit.SavePrefab(root, $"{Paths.Prefabs}/Battle/{name}.prefab");
        }

        // ---------------------------------------------------------------- cards

        private static CardDatabase BuildCards(Dictionary<string, UnitSkin> skins, Dictionary<string, Projectile> projectiles)
        {
            UnitSkin S(string key) => skins.TryGetValue(key, out var s) ? s : null;

            var brump = Card("brump", "Brump", "A walking wall of armor. Ignores troops and marches straight for your towers.", Rarity.Rare, CardRole.Tank, 5, c =>
            {
                c.maxHealth = 2100; c.damage = 130; c.hitSpeed = 1.5f; c.attackRange = 0.3f; c.sightRange = 4.5f; c.moveSpeed = 0.42f;
                c.radius = 0.32f; c.mass = 6f; c.targets = TargetMode.BuildingsOnly; c.attackKind = AttackKind.Melee;
                c.visualScale = 1.35f; c.impactSfx = Sfx.SwordHit;
                c.blueSkin = S("warrior_Blue"); c.redSkin = S("warrior_Red");
            });

            var jitter = Card("jitter", "Jitter", "Three hyperactive goblins with torches. Fast, fragile, and great at swarming big targets.", Rarity.Common, CardRole.Swarm, 2, c =>
            {
                c.unitCount = 3; c.formationSpacing = 0.55f;
                c.maxHealth = 120; c.damage = 55; c.hitSpeed = 1.0f; c.attackRange = 0.22f; c.sightRange = 4.5f; c.moveSpeed = 0.95f;
                c.radius = 0.18f; c.mass = 0.6f; c.targets = TargetMode.Any; c.attackKind = AttackKind.Melee;
                c.visualScale = 0.85f; c.impactSfx = Sfx.Hit;
                c.blueSkin = S("torch_Blue"); c.redSkin = S("torch_Red");
            });

            var lancer = Card("lancer", "Lancer", "Hits hard from behind his spear. Shreds tanks if you keep the swarm off him.", Rarity.Rare, CardRole.MeleeDps, 4, c =>
            {
                c.maxHealth = 900; c.damage = 190; c.hitSpeed = 1.3f; c.attackRange = 0.55f; c.sightRange = 4.5f; c.moveSpeed = 0.55f;
                c.radius = 0.24f; c.mass = 2f; c.targets = TargetMode.Any; c.attackKind = AttackKind.Melee;
                c.visualScale = 1.05f; c.impactSfx = Sfx.SwordHit;
                c.blueSkin = S("lancer_Blue"); c.redSkin = S("lancer_Red");
                c.fallbackBlueSkin = S("warrior_Blue"); c.fallbackRedSkin = S("warrior_Red");
                c.fallbackTint = new Color(1f, 0.86f, 0.58f);
            });

            var kaboom = Card("kaboom", "Kaboom", "Lobs lit dynamite over the fight. Wrecks groups, but can't take a punch.", Rarity.Rare, CardRole.Splash, 3, c =>
            {
                c.maxHealth = 330; c.damage = 150; c.hitSpeed = 1.8f; c.attackRange = 2.4f; c.sightRange = 4.8f; c.moveSpeed = 0.5f;
                c.radius = 0.22f; c.mass = 1f; c.targets = TargetMode.Any; c.attackKind = AttackKind.Projectile;
                c.splashRadius = 0.85f; c.projectilePrefab = projectiles["dynamite"]; c.projectileOrigin = new Vector2(0.24f, 0.6f);
                c.attackSfx = Sfx.Throw;
                c.blueSkin = S("tnt_Blue"); c.redSkin = S("tnt_Red");
            });

            var archers = Card("archers", "Archers", "A pair of sharpshooters. Keep them behind a tank or a tower.", Rarity.Common, CardRole.Ranged, 3, c =>
            {
                c.unitCount = 2; c.formationSpacing = 0.55f;
                c.maxHealth = 230; c.damage = 55; c.hitSpeed = 1.0f; c.attackRange = 2.6f; c.sightRange = 5f; c.moveSpeed = 0.55f;
                c.radius = 0.2f; c.mass = 0.8f; c.targets = TargetMode.Any; c.attackKind = AttackKind.Projectile;
                c.projectilePrefab = projectiles["arrow"]; c.projectileOrigin = new Vector2(0.26f, 0.52f);
                c.attackSfx = Sfx.ArrowShoot; c.visualScale = 0.95f;
                c.unlockedByDefault = false; c.unlockPrice = 250;
                c.blueSkin = S("archer_Blue"); c.redSkin = S("archer_Red");
            });

            var barrel = Card("barrelbro", "Barrel Bro", "A barrel with legs and a short fuse. Sprints at the nearest building and blows up.", Rarity.Epic, CardRole.Kamikaze, 2, c =>
            {
                c.maxHealth = 300; c.damage = 400; c.hitSpeed = 0.6f; c.attackRange = 0.2f; c.sightRange = 5f; c.moveSpeed = 1.25f;
                c.radius = 0.24f; c.mass = 1.2f; c.targets = TargetMode.BuildingsOnly; c.attackKind = AttackKind.Kamikaze;
                c.splashRadius = 1.0f;
                c.unlockedByDefault = false; c.unlockPrice = 400;
                c.blueSkin = S("barrel_Blue"); c.redSkin = S("barrel_Red");
            });

            var db = EditorKit.LoadOrCreate<CardDatabase>($"{Paths.Resources}/{CardDatabase.ResourcePath}.asset");
            db.cards = new List<CardData> { brump, jitter, lancer, kaboom, archers, barrel };
            db.defaultDeck = new List<string> { "brump", "jitter", "lancer", "kaboom" };
            db.aiDecks = new List<DeckPreset>
            {
                new DeckPreset { cardIds = new List<string> { "brump", "archers", "jitter", "kaboom" } },
                new DeckPreset { cardIds = new List<string> { "lancer", "barrelbro", "archers", "jitter" } },
                new DeckPreset { cardIds = new List<string> { "brump", "lancer", "kaboom", "jitter" } },
                new DeckPreset { cardIds = new List<string> { "barrelbro", "lancer", "kaboom", "archers" } }
            };
            db.emotes = new List<EmoteData>
            {
                new EmoteData { id = "gg", text = "GG!", price = 0 },
                new EmoteData { id = "oops", text = "Oops!", price = 0 },
                new EmoteData { id = "wow", text = "Wow!", icon = EditorKit.Sprite($"{Paths.Emotes}/emote_wow.png"), price = 0 },
                new EmoteData { id = "laugh", text = "Hehehe", icon = EditorKit.Sprite($"{Paths.Emotes}/emote_laugh.png"), price = 100 },
                new EmoteData { id = "angry", text = "Grr!", icon = EditorKit.Sprite($"{Paths.Emotes}/emote_angry.png"), price = 100 },
                new EmoteData { id = "cry", text = "Noooo", icon = EditorKit.Sprite($"{Paths.Emotes}/emote_cry.png"), price = 120 }
            };
            EditorUtility.SetDirty(db);
            return db;
        }

        private static CardData Card(string id, string name, string description, Rarity rarity, CardRole role, int cost, System.Action<CardData> fill)
        {
            var card = EditorKit.LoadOrCreate<CardData>($"{Paths.CardData}/{name.Replace(" ", "")}.asset");
            card.id = id;
            card.displayName = name;
            card.description = description;
            card.rarity = rarity;
            card.role = role;
            card.elixirCost = cost;
            // Portraits made from optional art (the Lancer) aren't in the repo; a stand-in portrait is.
            string portrait = $"{Paths.Cards}/portrait_{id}.png";
            string fallback = $"{Paths.Cards}/portrait_{id}_fallback.png";
            card.portrait = File.Exists(portrait) ? EditorKit.Sprite(portrait) : null;
            card.fallbackPortrait = File.Exists(fallback) ? EditorKit.Sprite(fallback) : null;
            card.fallbackTint = Color.white;
            card.unlockedByDefault = true;
            card.unlockPrice = 0;
            card.unitCount = 1;
            card.formationSpacing = 0.45f;
            card.deployTime = 1f;
            card.splashRadius = 0f;
            card.projectilePrefab = null;
            card.visualScale = 1f;
            card.attackSfx = null;
            card.impactSfx = null;
            card.fallbackBlueSkin = null;
            card.fallbackRedSkin = null;
            fill(card);
            EditorUtility.SetDirty(card);
            return card;
        }

        // ---------------------------------------------------------------- audio

        private static void BuildSoundLibrary()
        {
            foreach (var path in Directory.GetFiles(Paths.Sfx, "*.wav")) ArtPipeline.Audio(path.Replace('\\', '/'), false);
            if (Directory.Exists(Paths.Music))
            {
                foreach (var path in Directory.GetFiles(Paths.Music).Where(p => !p.EndsWith(".meta")))
                    ArtPipeline.Audio(path.Replace('\\', '/'), true);
            }

            AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Paths.Sfx}/{name}.wav");

            SoundLibrary.Entry E(string id, float volume, float pitchMin, float pitchMax, float interval, params string[] files) => new SoundLibrary.Entry
            {
                id = id,
                clips = files.Select(Clip).Where(c => c != null).ToArray(),
                volume = volume,
                pitchMin = pitchMin,
                pitchMax = pitchMax,
                minInterval = interval
            };

            var library = EditorKit.LoadOrCreate<SoundLibrary>($"{Paths.Resources}/{SoundLibrary.ResourcePath}.asset");
            library.entries = new List<SoundLibrary.Entry>
            {
                E(Sfx.Deploy, 0.7f, 0.95f, 1.08f, 0.05f, "deploy"),
                E(Sfx.Hit, 0.55f, 0.9f, 1.12f, 0.04f, "hit_0", "hit_1", "hit_2"),
                E(Sfx.SwordHit, 0.42f, 0.92f, 1.1f, 0.05f, "sword_0", "sword_1"),
                E(Sfx.ArrowShoot, 0.38f, 0.92f, 1.1f, 0.05f, "arrow_shoot_0", "arrow_shoot_1"),
                E(Sfx.ArrowHit, 0.32f, 0.9f, 1.15f, 0.04f, "arrow_hit"),
                E(Sfx.Throw, 0.45f, 0.95f, 1.1f, 0.05f, "throw"),
                E(Sfx.Explosion, 0.75f, 0.9f, 1.08f, 0.06f, "explosion_0", "explosion_1"),
                E(Sfx.TowerShot, 0.22f, 0.95f, 1.1f, 0.05f, "tower_shot"),
                E(Sfx.TowerDestroyed, 0.95f, 0.97f, 1.02f, 0.2f, "tower_destroyed"),
                E(Sfx.UnitDeath, 0.4f, 0.9f, 1.15f, 0.04f, "unit_death"),
                E(Sfx.Click, 0.55f, 0.98f, 1.04f, 0.03f, "click"),
                E(Sfx.CardSelect, 0.5f, 1f, 1.06f, 0.03f, "card_select"),
                E(Sfx.Invalid, 0.45f, 1f, 1f, 0.15f, "invalid"),
                E(Sfx.Coin, 0.6f, 1f, 1.04f, 0.05f, "coin"),
                E(Sfx.Purchase, 0.7f, 1f, 1f, 0.1f, "purchase"),
                E(Sfx.Crown, 0.75f, 1f, 1f, 0.1f, "crown"),
                E(Sfx.Emote, 0.6f, 0.95f, 1.1f, 0.1f, "emote"),
                E(Sfx.Countdown, 0.5f, 1f, 1f, 0.2f, "countdown"),
                E(Sfx.BattleStart, 0.75f, 1f, 1f, 0.5f, "battle_start"),
                E(Sfx.DoubleElixir, 0.7f, 1f, 1f, 0.5f, "double_elixir"),
                E(Sfx.Overtime, 0.7f, 1f, 1f, 0.5f, "overtime"),
                E(Sfx.Victory, 0.85f, 1f, 1f, 1f, "victory"),
                E(Sfx.Defeat, 0.8f, 1f, 1f, 1f, "defeat"),
                E(Sfx.Draw, 0.8f, 1f, 1f, 1f, "draw")
            };

            library.menuMusic = FirstMusic("menu");
            library.battleMusic = FirstMusic("battle");
            library.musicGain = 0.75f;
            EditorUtility.SetDirty(library);
        }

        private static AudioClip FirstMusic(string prefix)
        {
            if (!Directory.Exists(Paths.Music)) return null;
            var file = Directory.GetFiles(Paths.Music).FirstOrDefault(p => !p.EndsWith(".meta") && Path.GetFileName(p).ToLowerInvariant().StartsWith(prefix));
            return file == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(file.Replace('\\', '/'));
        }
    }
}
