// LocalizationManager.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public class LocalizationManager : Singleton<LocalizationManager>
{
    private Dictionary<string, string> _table = new();
    public string currentLanguage = "KO";

    private void Awake() => LoadTable();

    public void LoadTable()
    {
        _table.Clear();
        int langIndex = currentLanguage switch { "KO" => 1, "EN" => 2, "JP" => 3, _ => 1 };
        CsvTableLoader.Load("LocalizationTable.csv", v => { if (v.Length > langIndex) _table[v[0]] = v[langIndex]; });
    }

    public void SetLanguage(string lang) { currentLanguage = lang; LoadTable(); }

    public string Get(string key) => _table.TryGetValue(key, out var text) ? text : $"[{key}]"; // 못 찾으면 키 자체가 보여서 누락 바로 발견됨
}