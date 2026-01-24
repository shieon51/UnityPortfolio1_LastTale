using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PortalData
{
    public int portalID;              // 내 포탈 ID
    public int OwnerSceneID;    // 내가 속한 씬 ID
    public int TargetPortalID;  // 목적지 포탈 ID (Edge 정보)

    public Vector2 Position;    // 스폰될 좌표

    // 런타임에 빠른 이동을 위해 연결된 도착지 데이터를 직접 참조
    // CSV 로드 후 BuildGraph 단계에서 채워넣음
    [NonSerialized]
    public PortalData ConnectedTargetData = null;
}

// [방향 그래프] 씬 간의 관계를 정의하는 노드 (길찾기용)
public class SceneNode
{
    public int SceneID;
    public string SceneName;

    // 길찾기 핵심 데이터
    // Key: 가고 싶은 목적지 씬 ID
    // Value: 그곳으로 가기 위해 내가 타야 할 '내 구역의 포탈' 데이터
    public Dictionary<int, PortalData> NavigationMap = new Dictionary<int, PortalData>();
}