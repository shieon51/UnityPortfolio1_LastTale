// DialogueGraphView.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;

public class DialogueGraphView : GraphView
{
    public System.Action<string> OnAnalyzeRequested;

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
        if (evt.target is DialogueGraphNode node && node.NodeType == "Start")
        {
            evt.menu.AppendAction("이 지점부터 경로 분석", _ => OnAnalyzeRequested?.Invoke(node.Guid));
            evt.menu.AppendSeparator();
        }
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
    /// <summary>시작 노드에서 뻗어나가는 흐름을 가로로, 분기는 세로로 정렬</summary>
    public void AutoLayout(GraphFilter filter, bool createGroups = true)
    {
        const float colGap = 620f;    // 깊이(가로) 간격
        const float rowGap = 340f;    // 형제(세로) 간격
        const float treeGap = 200f;   // 트리 사이 여백

        var all = nodes.Cast<DialogueGraphNode>()
        .Where(n => filter == null || filter.Matches(n.Data))
        .ToList();
        if (all.Count == 0) return;

        if (createGroups) foreach (var g in graphElements.OfType<Group>().ToList()) RemoveElement(g);

        var byGuid = all.ToDictionary(n => n.Guid, n => n);
        var placed = new HashSet<string>();
        float yCursor = 0f;

        // 연결 정보 (포트 순서 유지)
        List<DialogueGraphNode> ChildrenOf(DialogueGraphNode node)
        {
            var result = new List<DialogueGraphNode>();
            foreach (var port in node.OutputPorts)
                foreach (var edge in port.connections)
                    if (edge.input?.node is DialogueGraphNode child && byGuid.ContainsKey(child.Guid))
                        result.Add(child);
            return result;
        }

        // 트리 하나를 배치하고, 사용한 세로 높이를 반환
        // ★ 배치한 노드 목록과 사용한 높이를 함께 반환
        (List<DialogueGraphNode> members, float height) PlaceTree(DialogueGraphNode root, float baseY)
        {
            var depth = new Dictionary<string, int>();
            var order = new List<DialogueGraphNode>();
            var queue = new Queue<(DialogueGraphNode node, int d)>();
            queue.Enqueue((root, 0));

            // BFS로 깊이 계산 (이미 배치된 노드는 건너뜀 = 합류 지점 중복 방지)
            while (queue.Count > 0)
            {
                var (node, d) = queue.Dequeue();
                if (!placed.Add(node.Guid))
                {
                    // 이미 다른 경로에서 배치됨 — 더 깊은 쪽으로 밀어줌
                    if (depth.TryGetValue(node.Guid, out int prev) && d > prev) depth[node.Guid] = d;
                    continue;
                }
                depth[node.Guid] = d;
                order.Add(node);
                foreach (var child in ChildrenOf(node)) queue.Enqueue((child, d + 1));
            }

            // 깊이별로 묶어서 세로로 쌓기
            var byDepth = order.GroupBy(n => depth[n.Guid]).OrderBy(g => g.Key).ToList();
            int maxRows = byDepth.Count > 0 ? byDepth.Max(g => g.Count()) : 1;

            foreach (var group in byDepth)
            {
                var list = group.ToList();
                // 이 열의 노드들을 세로 중앙 정렬
                float columnHeight = (list.Count - 1) * rowGap;
                float startY = baseY + ((maxRows - 1) * rowGap - columnHeight) * 0.5f;

                for (int i = 0; i < list.Count; i++)
                {
                    var pos = new Vector2(group.Key * colGap, startY + i * rowGap);
                    var rect = list[i].GetPosition();
                    list[i].SetPosition(new Rect(pos, rect.size));
                    list[i].Data.position = pos;
                }
            }

            return (order, maxRows * rowGap);
        }

        // 시작 노드부터 트리 단위로 배치 (Day/시각 순)
        var roots = all
            .Where(n => n.NodeType == "Start")
            .OrderBy(n => n.Data.day)
            .ThenBy(n => n.Data.startHour < 0 ? int.MaxValue : n.Data.startHour)
            .ThenBy(n => n.Data.npcTag)
            .ToList();

        foreach (var root in roots)
        {
            if (placed.Contains(root.Guid)) continue;
            var (members, used) = PlaceTree(root, yCursor);   // ★

            if (createGroups && members.Count > 0)
            {
                string title = root.Data.day > 0
                    ? $"Day {root.Data.day} · {(string.IsNullOrEmpty(root.Data.npcTag) ? "?" : root.Data.npcTag)} · {root.Data.knotName}"
                    : root.Data.knotName;
                var group = new Group { title = title };
                AddElement(group);
                foreach (var m in members) group.AddElement(m);   // ★ 이 트리의 노드만 정확히
            }

            yCursor += used + treeGap;
        }

        // 어느 트리에도 안 붙은 고아 노드들을 맨 아래에
        var orphans = all.Where(n => !placed.Contains(n.Guid)).ToList();
        for (int i = 0; i < orphans.Count; i++)
        {
            var pos = new Vector2(i * colGap, yCursor);
            orphans[i].SetPosition(new Rect(pos, orphans[i].GetPosition().size));
            orphans[i].Data.position = pos;
        }
    }
}