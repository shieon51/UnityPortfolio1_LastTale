// DialogueGraphData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum GraphConditionType { HasMemory, CounterAtLeast, AffectionAtLeast, SuspicionAtLeast, UnderstandingAtLeast }
public enum GraphLogicType { AcquireMemory, EraseMemory, IncrementCounter, AddAffection, AddSuspicion, AddTrust, AddLineCrossed, AddPersonalBond, PlayCue }

[Serializable]
public class GraphConditionEntry
{
    public GraphConditionType type;
    public string key = "";     // flagId / 카운터 키 / NPC 이름
    public int value;           // 비교값
    public bool negate;         // "~가 아닐 때"
}

[Serializable]
public class GraphLogicEntry
{
    public GraphLogicType type;
    public string key = "";
    public int amount = 1;
}

[Serializable]
public class GraphNodeData
{
    public string guid;
    public string nodeType;      // "Start", "Line", "Choice"
    public Vector2 position;

    public string knotName;      // Start 전용
    public string text;          // Line 전용 — 대사 본문
    public string speakerKey;    // Line 전용 — #speak 대상
    public string speakerName;   // Line 전용 — 표시명
    public bool forcePanel;
    public bool isSystem;
    public float autoAdvance = -1f;
    public bool lockInput;

    public List<string> choiceTexts = new(); // Choice 전용

    // GraphNodeData에 지금 추가해둘 것 (UI는 나중에)
    [Header("분류/필터용 메타데이터")]
    public int day = 0;              // 0 = 날짜 무관
    public string npcTag = "";       // 주로 관련된 NPC
    public int startHour = -1;       // -1 = 시간 무관
    public int endHour = -1;
    public string colorTag = "";     // 사용자 정의 색상 그룹
    public string note = "";         // 작업 메모

    public List<GraphConditionEntry> conditions = new();   // Condition 노드 전용
    public List<GraphLogicEntry> logics = new();           // Logic 노드 전용
    public List<GraphConditionEntry> choiceConditions = new(); // Choice 노드 — 선택지별 조건(인덱스 매칭)
}

[Serializable]
public class GraphEdgeData
{
    public string fromGuid;
    public int fromPortIndex;    // Choice 노드는 선택지 인덱스
    public string toGuid;
}

[CreateAssetMenu(menuName = "LastMarchan/Narrative/Dialogue Graph")]
public class DialogueGraphData : ScriptableObject
{
    public List<GraphNodeData> nodes = new();
    public List<GraphEdgeData> edges = new();
}