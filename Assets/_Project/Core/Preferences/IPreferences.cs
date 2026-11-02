namespace Kturker.UnityBase.Core.Preferences
{
    // Small key-value settings store. Reads return defaultValue for a missing or unreadable key;
    // writes stay in memory until Save(). Faz 2's save system plugs in another implementation.
    public interface IPreferences
    {
        bool GetBool(string key, bool defaultValue);
        void SetBool(string key, bool value);
        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);
        float GetFloat(string key, float defaultValue);
        void SetFloat(string key, float value);
        string GetString(string key, string defaultValue);
        void SetString(string key, string value);
        // The key reads as missing again (every Get returns its default)
        void Delete(string key);
        void Save();
    }
}
