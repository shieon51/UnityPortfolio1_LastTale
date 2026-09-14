// DialogueGraphView.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;

public class DialogueGraphView : GraphView
{
    public DialogueGraphView()
    {
        style.flexGrow = 1;
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        // ★ 스타일시트 적용
        var uss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/DialogueGraph/DialogueGraph.uss");
        if (uss != null) styleSheets.Add(uss);
        else Debug.LogWarning("[DialogueGraph] USS 로드 실패 — 경로를 확인하세요"); // ★ 추가

        // ★ 미니맵 — 큰 그래프에서 길 잃지 않게
        var minimap = new MiniMap { anchored = true };
        minimap.SetPosition(new Rect(10, 30, 200, 140));
        Add(minimap);

        serializeGraphElements = OnCopy;
        canPasteSerializedData = data => !string.IsNullOrEmpty(data);
        unserializeAndPaste = OnPaste;
    }

    [System.Serializable]
    private class CopyPayload
    {
        public List<GraphNodeData> nodes = new();
        public List<GraphEdgeData> edges = new();
    }

    private string OnCopy(IEnumerable<GraphElement> elements)
    {
        var payload = new CopyPayload();
        var picked = elements.OfType<DialogueGraphNode>().ToList();
        var guidSet = new HashSet<string>(picked.Select(n => n.Guid));

        foreach (var n in picked)
        {
            n.Data.position = n.GetPosition().position;
            payload.nodes.Add(n.Data);
        }

        // 선택된 노드끼리의 연결만 복사
        foreach (var edge in elements.OfType<Edge>())
        {
            if (edge.output?.node is not DialogueGraphNode from) continue;
            if (edge.input?.node is not DialogueGraphNode to) continue;
            if (!guidSet.Contains(from.Guid) || !guidSet.Contains(to.Guid)) continue;
            payload.edges.Add(new GraphEdgeData
            {
                fromGuid = from.Guid,
                fromPortIndex = from.OutputPorts.IndexOf(edge.output),
                toGuid = to.Guid,
            });
        }

        return JsonUtility.ToJson(payload);
    }

    private void OnPaste(string operationName, string data)
    {
        CopyPayload payload;
        try { payload = JsonUtility.FromJson<CopyPayload>(data); }
        catch { return; }
        if (payload?.nodes == null) return;

        ClearSelection();
        var guidMap = new Dictionary<string, DialogueGraphNode>();
        Vector2 offset = new Vector2(40, 40);

        foreach (var src in payload.nodes)
        {
            // 깊은 복사 — 원본과 데이터를 공유하지 않도록
            var clone = JsonUtility.FromJson<GraphNodeData>(JsonUtility.ToJson(src));
            string oldGuid = clone.guid;
            clone.guid = System.Guid.NewGuid().ToString();
            if (clone.nodeType == "Start" && !string.IsNullOrEmpty(clone.knotName))
                clone.knotName += "_Copy";

            var node = CreateNode(clone.nodeType, clone.position + offset, clone);
            guidMap[oldGuid] = node;
            AddToSelection(node);
        }

        foreach (var e in payload.edges)
        {
            if (!guidMap.TryGetValue(e.fromGuid, out var from) || !guidMap.TryGetValue(e.toGuid, out var to)) continue;
            if (e.fromPortIndex < 0 || e.fromPortIndex >= from.OutputPorts.Count) continue;
            var inPort = to.inputContainer.Q<Port>();
            if (inPort == null) continue;
            AddElement(from.OutputPorts[e.fromPortIndex].ConnectTo(inPort));
        }
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        => ports.ToList().Where(p => p != startPort && p.node != startPort.node && p.direction != startPort.direction).ToList();

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        Vector2 pos = contentViewContainer.WorldToLocal(evt.localMousePosition);
        evt.menu.AppendAction("시작 노드", _ => CreateNode("Start", pos));
        evt.menu.AppendAction("대사 노드", _ => CreateNode("Line", pos));
        evt.menu.AppendAction("선택지 노드", _ => CreateNode("Choice", pos));
        evt.menu.AppendAction("조건 분기 노드", _ => CreateNode("Branch", pos));
        base.BuildContextualMenu(evt);
    }

    public DialogueGraphNode CreateNode(string type, Vector2 pos, GraphNodeData data = null)
    {
        var node = new DialogueGraphNode
        {
            Guid = data?.guid ?? System.Guid.NewGuid().ToString(),
            NodeType = type,
            Data = data ?? new GraphNodeData(),
        };
        node.Data.guid = node.Guid;
        node.Data.nodeType = type;

        switch (type)
        {
            case "Start": node.BuildStart(); break;
            case "Line": node.BuildLine(); break;
            case "Choice": node.BuildChoice(); break;
            case "Branch": node.BuildBranch(); break;
        }

        node.SetPosition(new Rect(pos, new Vector2(340, 0))); // 너비는 USS가 고정, 높이는 내용에 맞춰 자동
        AddElement(node);
        return node;
    }

    public void ApplyFilter(GraphFilter filter)
    {
        foreach (var node in nodes.Cast<DialogueGraphNode>())
            node.SetFocused(filter == null || filter.Matches(node.Data));

        // 양쪽 노드가 모두 흐리면 연결선도 흐리게
        foreach (var edge in edges.ToList())
        {
            bool a = edge.output?.node is DialogueGraphNode f && (filter == null || filter.Matches(f.Data));
            bool b = edge.input?.node is DialogueGraphNode t && (filter == null || filter.Matches(t.Data));
            edge.style.opacity = (a || b) ? 1f : 0.15f;
        }
    }

    /// <summary>필터에 맞는 노드만 격자로 자동 정렬</summary>
    /// <summary>Day는 행, NPC는 열, 시각은 열 내부 순서로 배치</summary>
    public void AutoLayout(GraphFilter filter, bool createGroups = true)
    {
        const float columnWidth = 620f;   // NPC 열 간격
        const float nodeGap = 380f;       // 같은 열 안 노드 세로 간격
        const float dayGap = 220f;        // Day 블록 사이 여백

        var targets = nodes.Cast<DialogueGraphNode>()
            .Where(n => filter == null || filter.Matches(n.Data))
            .ToList();
        if (targets.Count == 0) return;

        if (createGroups) foreach (var g in graphElements.OfType<Group>().ToList()) RemoveElement(g);

        var days = targets.Select(n => n.Data.day).Distinct().OrderBy(d => d).ToList();
        var npcs = targets.Select(n => string.IsNullOrEmpty(n.Data.npcTag) ? "(미분류)" : n.Data.npcTag)
                          .Distinct().OrderBy(s => s).ToList();

        float yCursor = 0f;

        foreach (int day in days)
        {
            var dayNodes = targets.Where(n => n.Data.day == day).ToList();
            int maxRows = 1;

            for (int col = 0; col < npcs.Count; col++)
            {
                string npc = npcs[col];
                var colNodes = dayNodes
                    .Where(n => (string.IsNullOrEmpty(n.Data.npcTag) ? "(미분류)" : n.Data.npcTag) == npc)
                    .OrderBy(n => n.Data.startHour < 0 ? int.MaxValue : n.Data.startHour)  // 시각 순
                    .ThenBy(n => n.NodeType == "Start" ? 0 : 1)                             // 시작 노드 먼저
                    .ToList();
                if (colNodes.Count == 0) continue;

                maxRows = Mathf.Max(maxRows, colNodes.Count);

                for (int row = 0; row < colNodes.Count; row++)
                {
                    var pos = new Vector2(col * columnWidth, yCursor + row * nodeGap);
                    var rect = colNodes[row].GetPosition();
                    colNodes[row].SetPosition(new Rect(pos, rect.size));
                    colNodes[row].Data.position = pos;
                }
            }

            if (createGroups)
            {
                var group = new Group { title = day > 0 ? $"Day {day}" : "날짜 무관" };
                AddElement(group);
                foreach (var n in dayNodes) group.AddElement(n);
            }

            yCursor += maxRows * nodeGap + dayGap;
        }
    }
}