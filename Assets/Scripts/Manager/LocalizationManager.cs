using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LanguageColumn
{
    [Tooltip("언어 코드 (예: KO, EN, JP)")]
    public string code = "KO";
    [Tooltip("LocalizationTable.csv에서 이 언어가 들어있는 열 번호 (0 = 키 열)")]
    public int columnIndex = 1;
}

public class LocalizationManager : Singleton<LocalizationManager>
{
    [Header("테이블")]
    [Tooltip("StreamingAssets/Datas 아래의 파일명")]
    public string tableFileName = "LocalizationTable.csv";

    [Header("언어")]
    public string currentLanguage = "KO";

    [Tooltip("언어 코드와 CSV 열 번호. 언어를 추가할 때 코드를 고치지 않고 여기만 늘리면 된다")]
    public List<LanguageColumn> languageColumns = new();

    [Tooltip("현재 언어에 값이 비어 있으면 이 언어 열의 값을 대신 쓴다")]
    public string fallbackLanguage = "KO";

    // 언어가 바뀌면 화면에 떠 있는 문구들이 스스로 갱신할 수 있게 알림
    public event Action OnLanguageChanged;

    private Dictionary<string, string> _table = new();
    private Dictionary<string, string> _fallbackTable = new();

    private string _appliedLanguage;   // ★ 인스펙터에서 언어를 바꿨는지 감지용

#if UNITY_EDITOR
    // ★ 인스펙터 컴포넌트 우측 상단 ⋮ 메뉴에서 바로 실행할 수 있다
    [ContextMenu("언어: 한국어")]
    private void DebugSetKorean() => SetLanguage("KO");

    [ContextMenu("언어: English")]
    private void DebugSetEnglish() => SetLanguage("EN");

    [ContextMenu("언어: 日本語")]
    private void DebugSetJapanese() => SetLanguage("JP");

    [ContextMenu("테이블 다시 불러오기")]
    private void DebugReload()
    {
        LoadTable();
        OnLanguageChanged?.Invoke();
    }
#endif

#if UNITY_EDITOR
    // ★ 플레이 중 인스펙터에서 currentLanguage 값을 바꾸면 즉시 반영된다
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (_appliedLanguage == currentLanguage) return;

        _appliedLanguage = currentLanguage;
        LoadTable();
        OnLanguageChanged?.Invoke();
    }
#endif

    private void Awake()
    {
        EnsureDefaultColumns();
        LoadTable();
    }

    // 인스펙터에서 비워둔 경우를 대비한 기본값
    private void EnsureDefaultColumns()
    {
        if (languageColumns != null && languageColumns.Count > 0) return;
        languageColumns = new List<LanguageColumn>
        {
            new LanguageColumn { code = "KO", columnIndex = 1 },
            new LanguageColumn { code = "EN", columnIndex = 2 },
            new LanguageColumn { code = "JP", columnIndex = 3 },
        };
    }

    private int GetColumnIndex(string languageCode)
    {
        foreach (var lang in languageColumns)
            if (lang.code == languageCode) return lang.columnIndex;

        Debug.LogWarning($"[LocalizationManager] 등록되지 않은 언어 코드: '{languageCode}' — 첫 번째 언어로 대체");
        return languageColumns.Count > 0 ? languageColumns[0].columnIndex : 1;
    }

    public void LoadTable()
    {
        _table.Clear();
        _fallbackTable.Clear();

        int langIndex = GetColumnIndex(currentLanguage);
        int fallbackIndex = GetColumnIndex(fallbackLanguage);

        CsvTableLoader.Load(tableFileName, cols =>
        {
            string key = CsvTableLoader.Get(cols, 0);
            if (string.IsNullOrEmpty(key)) return;

            string value = CsvTableLoader.Get(cols, langIndex);
            if (!string.IsNullOrEmpty(value)) _table[key] = value;

            string fallback = CsvTableLoader.Get(cols, fallbackIndex);
            if (!string.IsNullOrEmpty(fallback)) _fallbackTable[key] = fallback;
        });

        Debug.Log($"[LocalizationManager] '{currentLanguage}' 문구 {_table.Count}개 로드 완료");
        _appliedLanguage = currentLanguage;   // ★ 추가
    }

    public void SetLanguage(string languageCode)
    {
        if (languageCode == currentLanguage) return;
        currentLanguage = languageCode;
        LoadTable();
        OnLanguageChanged?.Invoke();   // ★ 화면에 떠 있는 UI가 스스로 갱신
    }

    public bool Has(string key) => !string.IsNullOrEmpty(key) && _table.ContainsKey(key);

    // 못 찾으면 키 자체가 보여서 누락을 바로 발견할 수 있다
    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        if (_table.TryGetValue(key, out var text)) return text;
        if (_fallbackTable.TryGetValue(key, out var fallback)) return fallback;

#if UNITY_EDITOR
        Debug.LogWarning($"[LocalizationManager] 문구 없음: '{key}' ({currentLanguage})");
#endif
        return $"[{key}]";
    }

    // "마나가 {0} 부족합니다" 같은 서식 문구용
    public string GetFormat(string key, params object[] args)
    {
        string format = Get(key);
        if (args == null || args.Length == 0) return format;

        try { return string.Format(format, args); }
        catch (FormatException)
        {
            Debug.LogError($"[LocalizationManager] 서식이 인자와 맞지 않음: '{key}' → \"{format}\"");
            return format;
        }
    }
}