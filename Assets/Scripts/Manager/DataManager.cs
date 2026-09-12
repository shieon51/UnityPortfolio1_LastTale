using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 게임 내 모든 데이터를 로드하고 보관하는 중앙 저장소 (Repository)
public class DataManager : Singleton<DataManager>
{
    // 씬 정보, 포탈 정보, 이벤트 정보  ++ 나중에 몬스터 정보도 추가 예정
    public Dictionary<int, string> SceneDict { get; private set; } = new Dictionary<int, string>();
    public Dictionary<int, PortalData> PortalDict { get; private set; } = new Dictionary<int, PortalData>();
    public Dictionary<int, EventData> EventDict { get; private set; } = new Dictionary<int, EventData>();

    private void Awake() => LoadAllData();

    public void LoadAllData()
    {
        SceneDict.Clear(); PortalDict.Clear(); EventDict.Clear(); // ★ 재로드 대비 초기화 추가
        LoadSceneData();
        LoadPortalData();
        LoadEventData();
        Debug.Log("[DataManager] 모든 CSV 데이터 로드 완료!");
    }

    private void LoadSceneData()
        => CsvTableLoader.Load("SceneTable.csv", v => SceneDict[int.Parse(v[0])] = v[1].Trim());

    private void LoadPortalData()
        => CsvTableLoader.Load("PortalTable.csv", v => PortalDict[int.Parse(v[0])] = new PortalData
        {
            portalID = int.Parse(v[0]),
            OwnerSceneID = int.Parse(v[1]),
            TargetPortalID = int.Parse(v[2]),
            Position = new Vector2(float.Parse(v[3]), float.Parse(v[4]))
        });

    private void LoadEventData()
        => CsvTableLoader.Load("EventTable.csv", v => EventDict[int.Parse(v[0])] = new EventData
        {
            EventID = int.Parse(v[0]),
            EventName = v[1],
            IsAnytime = bool.Parse(v[2]),
            Day = int.Parse(v[3]),
            StartTime = int.Parse(v[4]),
            EndTime = int.Parse(v[5]),
            InkNodeName = v[6],
            SceneID = int.Parse(v[7]),
            Position = new Vector2(float.Parse(v[8]), float.Parse(v[9])),
            TimeTaken = int.Parse(v[10]),
            AutoTrigger = CsvTableLoader.GetBool(v, 11),        // ★ 빈 값 안전
            maxTriggerCount = CsvTableLoader.GetInt(v, 12, 0),  // ★ 빈 값이면 0 = 무제한
            exhaustedInkNode = CsvTableLoader.Get(v, 13, ""),   // ★ 빈 값이면 숨김 처리
            summonNPCs = CsvTableLoader.Get(v, 14, ""),              // ★ 추가
            despawnAfterEvent = CsvTableLoader.GetBool(v, 15, true), // ★ 추가
            triggerZoneSize = new Vector2(CsvTableLoader.GetFloat(v, 16, 0f), CsvTableLoader.GetFloat(v, 17, 0f)),
            triggerZoneOffset = new Vector2(CsvTableLoader.GetFloat(v, 18, 0f), CsvTableLoader.GetFloat(v, 19, 0f)),
        });

#if UNITY_EDITOR
    [ContextMenu("CSV 다시 로드")]
    private void ReloadFromMenu() => LoadAllData();
#endif
}
