using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// LevelDataManager.cs (신규) — SkillDataManager와 완전히 같은 패턴
public class LevelDataManager : Singleton<LevelDataManager>
{
    private Dictionary<int, PlayerLevelData> _levelDict = new();

    private void Awake() => LoadLevelTable();

    private void LoadLevelTable()
    {
        CsvTableLoader.Load("PlayerLevelTable.csv", v =>
        _levelDict[int.Parse(v[0])] = new PlayerLevelData
        {
            level = int.Parse(v[0]),
            maxHealth = int.Parse(v[1]),
            maxMana = int.Parse(v[2]),
            expToNextLevel = int.Parse(v[3])
        });
    }

    public PlayerLevelData GetLevelData(int level)
    {
        if (_levelDict.TryGetValue(level, out var data)) return data;
        Debug.LogWarning($"[LevelDataManager] 레벨 {level} 데이터가 없습니다. CSV를 확인하세요.");
        return null;
    }

    public int MaxDefinedLevel => _levelDict.Count > 0 ? _levelDict.Keys.Max() : 1;

#if UNITY_EDITOR
    [ContextMenu("CSV 다시 로드")]
    private void ReloadFromMenu() { _levelDict.Clear(); LoadLevelTable(); Debug.Log("[LevelDataManager] CSV 다시 로드 완료"); }
#endif
}