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
    public void AutoLayout(GraphFilter filter, float spacingX = 600f, float spacingY = 420f, int perRow = 4)
    {
        var targets = nodes.Cast<DialogueGraphNode>()
            .Where(n => filter == null || filter.Matches(n.Data))
            .OrderBy(n => n.Data.day)
            .ThenBy(n => n.Data.startHour)
            .ThenBy(n => n.NodeType == "Start" ? 0 : 1)
            .ToList();

        for (int i = 0; i < targets.Count; i++)
        {
            float x = (i % perRow) * spacingX;
            float y = (i / perRow) * spacingY;
            var rect = targets[i].GetPosition();
            targets[i].SetPosition(new Rect(new Vector2(x, y), rect.size));
            targets[i].Data.position = new Vector2(x, y);
        }
    }
}