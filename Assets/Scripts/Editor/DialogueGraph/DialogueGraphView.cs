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

        // ★ 미니맵 — 큰 그래프에서 길 잃지 않게
        var minimap = new MiniMap { anchored = true };
        minimap.SetPosition(new Rect(10, 30, 200, 140));
        Add(minimap);
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        => ports.ToList().Where(p => p != startPort && p.node != startPort.node && p.direction != startPort.direction).ToList();

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        Vector2 pos = contentViewContainer.WorldToLocal(evt.localMousePosition);
        evt.menu.AppendAction("시작 노드", _ => CreateNode("Start", pos));
        evt.menu.AppendAction("대사 노드", _ => CreateNode("Line", pos));
        evt.menu.AppendAction("선택지 노드", _ => CreateNode("Choice", pos));
        evt.menu.AppendAction("조건 분기 노드", _ => CreateNode("Condition", pos));
        evt.menu.AppendAction("로직 노드", _ => CreateNode("Logic", pos));
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
            case "Condition": node.BuildCondition(); break;  // ★ 2단계에서 추가
            case "Logic": node.BuildLogic(); break;          // ★ 2단계에서 추가
        }

        node.capabilities |= Capabilities.Resizable; // ★ 여기
        node.SetPosition(new Rect(pos, new Vector2(280, 220)));
        AddElement(node);
        return node;
    }
}