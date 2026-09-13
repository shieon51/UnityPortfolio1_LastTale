// DialogueGraphData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum GraphVarType { Memory, Counter, Affection, Suspicion, Understanding, TrustEarned, LineCrossed, PersonalBond, MentalPercent }
public enum CondOp { Has, NotHas, GreaterOrEqual, LessOrEqual, Equal, NotEqual }
public enum CondJoin { And, Or }

[Serializable]
public class ConditionEntry
{
    public GraphVarType varType = GraphVarType.Memory;
    public string key = "";
    public CondOp op = CondOp.Has;
    public int value;
}

[Serializable]
public class ConditionGroup
{
    public List<ConditionEntry> entries = new();
    public CondJoin join = CondJoin.And;
    [Tooltip("비워두면 조건에서 자동 생성된 요약이 표시됨")]
    public string summaryOverride = "";
}

[Serializable]
public class BranchCase
{
    public string label = "";
    public ConditionGroup condition = new();
}

[Serializable]
public class ChoiceOption
{
    public string text = "선택지";
    public ConditionGroup condition = new();
}

[Serializable]
public class GraphLogicEntry
{
    public GraphVarType varType = GraphVarType.Memory;
    public string key = "";
    public int amount = 1;
    public bool isErase; // Memory 타입일 때만: 획득 대신 삭제
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

    public Vector2 size = new Vector2(280, 220);

    // GraphNodeData에 지금 추가해둘 것 (UI는 나중에)
    [Header("분류/필터용 메타데이터")]
    public int day = 0;              // 0 = 날짜 무관
    public string npcTag = "";       // 주로 관련된 NPC
    public int startHour = -1;       // -1 = 시간 무관
    public int endHour = -1;
    public string colorTag = "";     // 사용자 정의 색상 그룹
    public string note = "";         // 작업 메모

    public List<GraphLogicEntry> logics = new();     // Line 노드에 통합
    public List<BranchCase> branchCases = new();     // Branch 노드
    public List<ChoiceOption> choiceOptions = new(); // Choice 노드 (choiceTexts 대체)
    public string cueId = "";                        // Line 노드의 #cue 태그
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