using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace CrashRoyale.EditorTools
{
    /// <summary>Import settings and sprite-sheet slicing.</summary>
    internal static class ArtPipeline
    {
        public static bool Exists(string path) => File.Exists(path);

        public static void Single(string path, float pixelsPerUnit, Vector2 pivot, bool pixelArt, Vector4 border = default)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Builder] No texture at {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = pixelArt ? FilterMode.Point : FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteBorder = border;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        /// <summary>Large non-sprite textures (menu backgrounds) as sprites with bilinear filtering and a size cap.</summary>
        public static void Background(string path, float pixelsPerUnit, Vector2 pivot, int maxSize = 2048)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            Single(path, pixelsPerUnit, pivot, false);
            importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Slices a grid sheet into frames named "{file}_{row}_{col}" (row 0 = top), skipping empty cells.
        /// Existing sprite ids are reused so references survive a rebuild.
        /// </summary>
        public static Dictionary<string, Sprite> Grid(string path, int frameWidth, int frameHeight, float pixelsPerUnit, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Builder] No sheet at {path}");
                return new Dictionary<string, Sprite>();
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(path));
            var pixels = source.GetPixels32();
            int width = source.width;
            int height = source.height;
            Object.DestroyImmediate(source);

            string baseName = Path.GetFileNameWithoutExtension(path);
            int cols = width / frameWidth;
            int rows = height / frameHeight;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.First().spriteID);

            var rects = new List<SpriteRect>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int x = c * frameWidth;
                    int y = height - (r + 1) * frameHeight;
                    if (IsEmpty(pixels, width, x, y, frameWidth, frameHeight)) continue;

                    string name = $"{baseName}_{r}_{c}";
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(x, y, frameWidth, frameHeight),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        spriteID = existing.TryGetValue(name, out var id) ? id : GUID.Generate()
                    });
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            provider.Apply();
            importer.SaveAndReimport();

            return EditorKit.Sprites(path);
        }

        private static bool IsEmpty(Color32[] pixels, int width, int x0, int y0, int w, int h)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                int row = y * width;
                for (int x = x0; x < x0 + w; x++)
                {
                    if (pixels[row + x].a > 8) return false;
                }
            }

            return true;
        }

        public static void Audio(string path, bool music)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;

            importer.forceToMono = !music;
            importer.loadInBackground = music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.55f : 0.75f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
