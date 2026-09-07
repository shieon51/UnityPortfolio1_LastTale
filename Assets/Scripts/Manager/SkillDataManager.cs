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
        => CsvTableLoader.Load("SkillTable.csv", v => IdentityDict[v[0]] = new SkillIdentityData
        { skillId = v[0], displayName = v[1], description = v[2], maxLevel = int.Parse(v[3]) });

    private void LoadSkillLevelTable()
        => CsvTableLoader.Load("SkillLevelTable.csv", v => _levelDict[(v[0], int.Parse(v[1]))] = new SkillLevelData
        { skillId = v[0], level = int.Parse(v[1]), manaCost = int.Parse(v[2]), damageMultiplier = float.Parse(v[3]) });



    public SkillLevelData GetLevelData(string skillId, int level)
    {
        if (_levelDict.TryGetValue((skillId, level), out var data)) return data;
        Debug.LogWarning($"[SkillDataManager] {skillId} Lv.{level} 데이터가 없습니다. CSV를 확인하세요.");
        return null;
    }

    /*
     Play 모드 중에 SkillDataManager 오브젝트를 클릭 → 컴포넌트 우클릭 → "CSV 다시 로드"를 누르면, 
    게임을 재시작 안 해도 CSV에서 수정한 숫자가 바로 반영됩니다. 밸런스 튜닝 속도가 크게 빨라집니다.
    */

#if UNITY_EDITOR
    [ContextMenu("CSV 다시 로드")]
    private void ReloadFromMenu() 
    { 
        IdentityDict.Clear(); 
        _levelDict.Clear(); 
        LoadSkillTable(); 
        LoadSkillLevelTable(); 
        Debug.Log("[SkillDataManager] CSV 다시 로드 완료"); 
    }
#endif
}