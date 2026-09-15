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
public class DialogueLine
{
    public string text = "";
    public string speakerKey = "";
    public string speakerName = "";
    public bool forcePanel;
    public bool isSystem;
    public float autoAdvance = -1f;
    public bool lockInput;
    public string cueId = "";
    public List<GraphLogicEntry> logics = new();
}

[Serializable]
public class GraphNodeData
{
    public string guid;
    public string nodeType;      // "Start", "Line", "Choice"
    public Vector2 position;

    public string knotName;      // Start 전용

    // GraphNodeData에 지금 추가해둘 것 (UI는 나중에)
    [Header("분류/필터용 메타데이터")]
    public int day = 0;              // 0 = 날짜 무관
    public string npcTag = "";       // 주로 관련된 NPC
    public int startHour = -1;       // -1 = 시간 무관
    public int endHour = -1;
    public string colorTag = "";     // 사용자 정의 색상 그룹
    public string note = "";         // 작업 메모

    public List<BranchCase> branchCases = new();     // Branch 노드
    public List<ChoiceOption> choiceOptions = new(); // Choice 노드 (choiceTexts 대체)
    public List<DialogueLine> lines = new();   // ★ Line 노드: 여러 줄을 한 노드에

    public string lastExportHash = "";
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

    // DialogueGraphData.cs — 노드 생성 헬퍼 (런타임 아닌 에디터에서 호출)
    public GraphNodeData CreateStartNodeFrom(EventMarker marker)
    {
        var node = new GraphNodeData
        {
            guid = System.Guid.NewGuid().ToString(),
            nodeType = "Start",
            knotName = marker.InkNodeName,
            day = marker.Day,
            npcTag = marker.EventName,
            startHour = marker.StartTime,
            endHour = marker.EndTime,
            note = $"이벤트 ID {marker.EventID} / {(marker.AutoTrigger ? "자동 발동" : "수동")}",
            position = new Vector2(0, nodes.Count * 200),
        };
        nodes.Add(node);
        return node;
    }
}