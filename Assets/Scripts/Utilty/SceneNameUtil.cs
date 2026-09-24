using System.Collections.Generic;
using UnityEngine;

// 씬 ID → 화면에 보여줄 장소 이름.
// SceneTable.csv의 DisplayKey를 로컬라이제이션으로 옮겨 돌려준다.
// (기록·기록장은 내부 씬 이름 대신 이 값을 쓴다)
public static class SceneNameUtil
{
    private const string TableFileName = "SceneTable.csv";
    private const int ColSceneId = 0;
    private const int ColSceneName = 1;
    private const int ColDisplayKey = 2;

    private static Dictionary<int, string> _displayKeys;

    private static void EnsureLoaded()
    {
        if (_displayKeys != null) return;
        _displayKeys = new Dictionary<int, string>();

        CsvTableLoader.Load(TableFileName, cols =>
        {
            int id = CsvTableLoader.GetInt(cols, ColSceneId, -1);
            if (id < 0) return;
            string key = CsvTableLoader.Get(cols, ColDisplayKey);
            if (!string.IsNullOrEmpty(key)) _displayKeys[id] = key;
        });
    }

    // 언어를 바꾸면 다시 읽을 필요는 없지만, 표를 수정했을 때 쓰려고 열어둔다
    public static void Reload() { _displayKeys = null; EnsureLoaded(); }

    public static string GetDisplayName(int sceneId, string fallbackInternalName = null)
    {
        EnsureLoaded();

        if (_displayKeys.TryGetValue(sceneId, out var key))
        {
            var loc = LocalizationManager.Instance;
            if (loc != null && loc.Has(key)) return loc.Get(key);
#if UNITY_EDITOR
            Debug.LogWarning($"[SceneNameUtil] 씬 {sceneId}의 표시 이름 키 '{key}'가 로컬라이제이션 표에 없음");
#endif
        }

        if (!string.IsNullOrEmpty(fallbackInternalName)) return fallbackInternalName;   // 내부 이름으로 대체
        return $"Scene {sceneId}";
    }
}