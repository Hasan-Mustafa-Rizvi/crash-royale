using System.Collections.Generic;
using UnityEngine;

namespace CrashRoyale.Meta
{
    /// <summary>Storage behind the player profile. PlayerPrefs in the game, in-memory in tests.</summary>
    public interface IKeyValueStore
    {
        bool HasKey(string key);
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        float GetFloat(string key, float fallback);
        void SetFloat(string key, float value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }

    public sealed class MemoryStore : IKeyValueStore
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();

        public bool HasKey(string key) => values.ContainsKey(key);
        public int GetInt(string key, int fallback) => values.TryGetValue(key, out var v) && v is int i ? i : fallback;
        public void SetInt(string key, int value) => values[key] = value;
        public float GetFloat(string key, float fallback) => values.TryGetValue(key, out var v) && v is float f ? f : fallback;
        public void SetFloat(string key, float value) => values[key] = value;
        public string GetString(string key, string fallback) => values.TryGetValue(key, out var v) && v is string s ? s : fallback;
        public void SetString(string key, string value) => values[key] = value;
        public void DeleteKey(string key) => values.Remove(key);
        public void Save() { }
    }
}
