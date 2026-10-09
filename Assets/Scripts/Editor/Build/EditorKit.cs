using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrashRoyale.EditorTools
{
    /// <summary>Small helpers for building assets from code.</summary>
    internal static class EditorKit
    {
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>Sets a serialized field (including private [SerializeField] ones) by name.</summary>
        public static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Builder] {target.GetType().Name} has no serialized field '{field}'");
                return;
            }

            Assign(prop, value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(SerializedProperty prop, object value)
        {
            switch (value)
            {
                case null:
                    prop.objectReferenceValue = null;
                    break;
                case Object o:
                    prop.objectReferenceValue = o;
                    break;
                case int i when prop.propertyType == SerializedPropertyType.Enum:
                    prop.enumValueIndex = i;
                    break;
                case int i:
                    prop.intValue = i;
                    break;
                case float f:
                    prop.floatValue = f;
                    break;
                case bool b:
                    prop.boolValue = b;
                    break;
                case string s:
                    prop.stringValue = s;
                    break;
                case Color c:
                    prop.colorValue = c;
                    break;
                case Vector2 v2:
                    prop.vector2Value = v2;
                    break;
                case Vector3 v3:
                    prop.vector3Value = v3;
                    break;
                case Enum e:
                    prop.enumValueIndex = Convert.ToInt32(e);
                    break;
                case System.Collections.IList list:
                    prop.arraySize = list.Count;
                    for (int k = 0; k < list.Count; k++) Assign(prop.GetArrayElementAtIndex(k), list[k]);
                    break;
                default:
                    Debug.LogError($"[Builder] Unsupported value type {value.GetType()} for {prop.propertyPath}");
                    break;
            }
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            EnsureFolder(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static Sprite Sprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null) Debug.LogWarning($"[Builder] Missing sprite at {path}");
            return sprite;
        }

        public static Dictionary<string, Sprite> Sprites(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name, s => s);
        }

        public static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static GameObject Child(GameObject parent, string name, params Type[] components)
        {
            var go = new GameObject(name, components);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        public static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path)) AssetDatabase.DeleteAsset(path);
        }
    }
}
