using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

// 씬은 여러 개의 포탈을 가지고 있음
// 각 포탈은 이동할 다음 포탈 정보를 가지고 있음 -> 이걸 방향성 있는 엣지라 하자
// 즉 씬은 포탈 엣지를 여러 개 지닐 수 있다

public class PortalManager : Singleton<PortalManager>
{
    // 1. 검색용 딕셔너리 (Save/Load, Debug용)
    private Dictionary<int, PortalData> _allPortalData = new Dictionary<int, PortalData>();

    // 2. 길찾기용 그래프 (BFS용)
    private Dictionary<int, SceneNode> _sceneGraph = new Dictionary<int, SceneNode>();

    // Manager가 가장 먼저 초기화되어야 함
    private void Awake()
    {
        LoadAndBuildData();
    }

    private void Start()
    {
        
    }

    private void LoadAndBuildData()
    {
        _allPortalData.Clear();
        _sceneGraph.Clear();

        string filePath = Path.Combine(Application.streamingAssetsPath, "Datas", "PortalTable.csv");
        if (!File.Exists(filePath)) return;

        string[] lines = File.ReadAllLines(filePath);

        // 1. 포탈 데이터 로드 (딕셔너리에 넣기) - 포탈 객체 생성부터
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            string[] values = lines[i].Split(',');

            // 1-1. PortalData 객체 생성
            PortalData data = new PortalData
            {
                portalID = int.Parse(values[0]),
                OwnerSceneID = int.Parse(values[1]),
                TargetPortalID = int.Parse(values[2]),
                Position = new Vector2(float.Parse(values[3]), float.Parse(values[4]))
            };

            // 1-2. 딕셔너리에 포탈 데이터 등록 (ID - PortalData 대응)
            _allPortalData[data.portalID] = data; // (-> 아직 ConnectedTargetData는 null임)
        }

        // 2. 그래프 구축 및 참조 연결 
        foreach (var myData in _allPortalData.Values)
        {
            // 2-1. SceneGraph 노드 생성 (길찾기용)
            if (!_sceneGraph.ContainsKey(myData.OwnerSceneID)) // 아직 해당 씬노드가 그래프에 없으면 만들기
            {
                SceneNode node = new SceneNode();
                node.SceneID = myData.OwnerSceneID;
                node.SceneName = SceneLoader.Instance.GetSceneName(myData.OwnerSceneID);
                _sceneGraph.Add(myData.OwnerSceneID, node);
            }

            // 2-2. PortalData끼리 직접 연결 (검색 없는 이동을 위해)
            if (myData.TargetPortalID != 0 && _allPortalData.ContainsKey(myData.TargetPortalID))
            {
                // 해당 포탈의 도착지 데이터(객체) 'ConnectedTargetData'에 연결
                myData.ConnectedTargetData = _allPortalData[myData.TargetPortalID];
            }
        }

        // 2-3 (보완) 씬 연결은 모든 노드가 생성된 후 수행하는 것이 안전
        BuildSceneConnections();
    }

    // 씬 그래프 연결 함수 (길찾기용 지도)
    private void BuildSceneConnections()
    {
        foreach (var myData in _allPortalData.Values)
        {
            if (myData.ConnectedTargetData == null) continue;

            int fromSceneID = myData.OwnerSceneID;
            int toSceneID = myData.ConnectedTargetData.OwnerSceneID;

            if (fromSceneID != toSceneID)
            {
                if (_sceneGraph.ContainsKey(fromSceneID) && _sceneGraph.ContainsKey(toSceneID))
                {
                    SceneNode myNode = _sceneGraph[fromSceneID];

                    // 가는 방법을 기록 (ex. toSceneID(2번 숲)로 가고 싶으면 myData(1001번 포탈)를 타라)
                    if (!myNode.NavigationMap.ContainsKey(toSceneID))
                    {
                        myNode.NavigationMap.Add(toSceneID, myData);
                    }
                }
            }
        }
    }

    // [기능 1] 외부(Portal 스크립트)에서 데이터 요청용
    public PortalData GetData(int portalID)
    {
        if (_allPortalData.ContainsKey(portalID)) return _allPortalData[portalID];
        return null;
    }

    // 프리펩을 받아서 생성하고 초기화하는 함수
    public void SpawnPortalsForScene(int sceneID, GameObject portalPrefab)
    {
        // 1. 전체 데이터 중에서 '현재 씬(sceneID)'에 있는 포탈만 골라내기
        foreach (var data in _allPortalData.Values)
        {
            if (data.OwnerSceneID == sceneID)
            {
                // 2. CSV에 적힌 좌표(Position)에 프리펩 생성 (Instantiate)
                GameObject go = Instantiate(portalPrefab, data.Position, Quaternion.identity);

                // 3. 생성된 오브젝트에서 Portal 컴포넌트 가져오기
                Portal portalScript = go.GetComponent<Portal>();

                // 4. ID 주입 (초기화)
                if (portalScript != null)
                {
                    portalScript.Init(data.portalID);
                }

                // (선택) 하이어라키에서 보기 좋게 이름 변경
                go.name = $"Portal_{data.portalID}";
            }
        }
    }

    //public void ShowGuideArrow(List<int> path)  //++ 길찾기 테스트
    //{
    //    int currentSceneID = 1;      // 현재 씬
    //    int nextSceneID = path[1];   // 다음 가야 할 씬 (2번)

    //    SceneNode currentNode = _sceneGraph[currentSceneID];

    //    // 특정 씬으로 가려면 어느 포탈을 타야하는지?
    //    if (currentNode.NavigationMap.TryGetValue(nextSceneID, out PortalData portalToTake))
    //    {
    //        Debug.Log($"화살표 표시: {portalToTake.Position} 위치에 있는 {portalToTake.portalID}번 포탈로 가세요!");

    //        // 인게임 구현: 화살표 UI를 portalToTake.Position 좌표에 띄워줌
    //    }
    //}
}
