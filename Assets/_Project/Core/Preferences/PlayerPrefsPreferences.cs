using UnityEngine;

namespace Kturker.UnityBase.Core.Preferences
{
    // PlayerPrefs-backed store. Bools are stored as 1/0; any other stored int reads as the default.
    public sealed class PlayerPrefsPreferences : IPreferences
    {
        public bool GetBool(string key, bool defaultValue)
        {
            switch (PlayerPrefs.GetInt(key, defaultValue ? 1 : 0))
            {
                case 1: return true;
                case 0: return false;
                default: return defaultValue;
            }
        }

        public void SetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);

        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);

        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);

        public string GetString(string key, string defaultValue) => PlayerPrefs.GetString(key, defaultValue);

        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);

        public void Delete(string key) => PlayerPrefs.DeleteKey(key);

        public void Save() => PlayerPrefs.Save();
    }
}
