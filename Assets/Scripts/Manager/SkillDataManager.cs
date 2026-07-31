using System.Collections.Generic;
using System.IO;
using UnityEngine;

// SkillDataManager.cs — DataManager와 완전히 같은 패턴 (스킬 데이터 불러오기)
public class SkillDataManager : Singleton<SkillDataManager>
{
    public Dictionary<string, SkillIdentityData> IdentityDict { get; private set; } = new();
    private Dictionary<(string, int), SkillLevelData> _levelDict = new();

    private void Awake()
    {
        LoadSkillTable();
        LoadSkillLevelTable();
    }

    private void LoadSkillTable()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Datas", "SkillTable.csv");
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            var v = lines[i].Split(',');
            var data = new SkillIdentityData { skillId = v[0], displayName = v[1], description = v[2], maxLevel = int.Parse(v[3]) };
            IdentityDict[data.skillId] = data;
        }
    }

    private void LoadSkillLevelTable()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Datas", "SkillLevelTable.csv");
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            var v = lines[i].Split(',');
            var data = new SkillLevelData { skillId = v[0], level = int.Parse(v[1]), manaCost = int.Parse(v[2]), damageMultiplier = float.Parse(v[3]) };
            _levelDict[(data.skillId, data.level)] = data;
        }
    }

    public SkillLevelData GetLevelData(string skillId, int level)
    {
        if (_levelDict.TryGetValue((skillId, level), out var data)) return data;
        Debug.LogWarning($"[SkillDataManager] {skillId} Lv.{level} 데이터가 없습니다. CSV를 확인하세요.");
        return null;
    }
}