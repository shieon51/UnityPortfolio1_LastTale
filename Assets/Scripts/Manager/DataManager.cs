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

    private void Awake()
    {
        // Manager들 중 가장 먼저 초기화되어야 하므로 Script Execution Order에서 우선순위를 높이거나,
        // Boot 씬에서 명시적으로 호출하는 것이 좋을 듯
        LoadAllData();
    }

    public void LoadAllData()
    {
        LoadSceneData();
        LoadPortalData();
        LoadEventData();
        Debug.Log("[DataManager] 모든 CSV 데이터 로드 완료!");
    }

    private void LoadSceneData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "Datas", "SceneTable.csv");
        if (!File.Exists(filePath)) return;

        string[] lines = File.ReadAllLines(filePath);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            string[] values = lines[i].Split(',');
            SceneDict.Add(int.Parse(values[0]), values[1].Trim());
        }
    }

    private void LoadPortalData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "Datas", "PortalTable.csv");
        if (!File.Exists(filePath)) return;

        string[] lines = File.ReadAllLines(filePath);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            string[] values = lines[i].Split(',');
            PortalData data = new PortalData
            {
                portalID = int.Parse(values[0]),
                OwnerSceneID = int.Parse(values[1]),
                TargetPortalID = int.Parse(values[2]),
                Position = new Vector2(float.Parse(values[3]), float.Parse(values[4]))
            };
            PortalDict.Add(data.portalID, data);
        }
    }

    private void LoadEventData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "Datas", "EventTable.csv");
        if (!File.Exists(filePath)) return;

        string[] lines = File.ReadAllLines(filePath);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            string[] values = lines[i].Split(',');
            EventData data = new EventData
            {
                EventID = int.Parse(values[0]),
                EventName = values[1],
                IsAnytime = bool.Parse(values[2]),
                Day = int.Parse(values[3]),
                StartTime = int.Parse(values[4]),
                EndTime = int.Parse(values[5]),
                InkNodeName = values[6],
                SceneID = int.Parse(values[7]),
                Position = new Vector2(float.Parse(values[8]), float.Parse(values[9])),
                TimeTaken = int.Parse(values[10]),
            };
            EventDict.Add(data.EventID, data);
        }
    }

}
