// DialogueGraphWindow.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class DialogueGraphWindow : EditorWindow
{
    private DialogueGraphView _graph;
    private DialogueGraphData _asset;

    private Dictionary<string, string> _knotNames; // guid → knot 이름

    [MenuItem("LastMarchan/Dialogue Graph Editor")]
    public static void Open() => GetWindow<DialogueGraphWindow>("대화 그래프");

    private void OnEnable()
    {
        _graph = new DialogueGraphView();
        rootVisualElement.Add(_graph);

        var toolbar = new UnityEditor.UIElements.Toolbar();
        var assetField = new UnityEditor.UIElements.ObjectField("그래프 애셋") { objectType = typeof(DialogueGraphData) };
        assetField.RegisterValueChangedCallback(e => _asset = e.newValue as DialogueGraphData);
        toolbar.Add(assetField);
        toolbar.Add(new Button(Save) { text = "저장" });
        toolbar.Add(new Button(Load) { text = "불러오기" });
        toolbar.Add(new Button(ExportInk) { text = "ink 내보내기" });
        rootVisualElement.Add(toolbar);
    }

    private void Save()
    {
        if (_asset == null) { EditorUtility.DisplayDialog("오류", "그래프 애셋을 먼저 지정하세요.", "확인"); return; }
        _asset.nodes.Clear();
        _asset.edges.Clear();

        foreach (var node in _graph.nodes.Cast<DialogueGraphNode>())
        {
            node.Data.position = node.GetPosition().position;
            _asset.nodes.Add(node.Data);
        }

        foreach (var edge in _graph.edges.ToList())
        {
            if (edge.input?.node is not DialogueGraphNode to) continue;
            if (edge.output?.node is not DialogueGraphNode from) continue;
            _asset.edges.Add(new GraphEdgeData
            {
                fromGuid = from.Guid,
                fromPortIndex = from.OutputPorts.IndexOf(edge.output),
                toGuid = to.Guid,
            });
        }

        EditorUtility.SetDirty(_asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[DialogueGraph] 저장 완료 — 노드 {_asset.nodes.Count}개, 연결 {_asset.edges.Count}개");
    }

    private void Load()
    {
        if (_asset == null) return;
        _graph.DeleteElements(_graph.graphElements.ToList());

        var map = new Dictionary<string, DialogueGraphNode>();
        foreach (var data in _asset.nodes)
            map[data.guid] = _graph.CreateNode(data.nodeType, data.position, data);

        foreach (var e in _asset.edges)
        {
            if (!map.TryGetValue(e.fromGuid, out var from) || !map.TryGetValue(e.toGuid, out var to)) continue;
            if (e.fromPortIndex < 0 || e.fromPortIndex >= from.OutputPorts.Count) continue;
            var inPort = to.inputContainer.Q<Port>();
            if (inPort == null) continue;
            var edge = from.OutputPorts[e.fromPortIndex].ConnectTo(inPort);
            _graph.AddElement(edge);
        }
    }

    private void ExportInk()
    {
        if (_asset == null) return;
        Save();

        _knotNames = new Dictionary<string, string>();

        // 1) Start 노드는 지정된 이름 사용
        foreach (var n in _asset.nodes.Where(n => n.nodeType == "Start"))
            _knotNames[n.guid] = string.IsNullOrEmpty(n.knotName) ? $"Knot_{n.guid.Substring(0, 6)}" : n.knotName;

        // 2) 분기 대상이 되는 노드는 자동 knot 부여
        foreach (var e in _asset.edges)
        {
            var from = _asset.nodes.FirstOrDefault(n => n.guid == e.fromGuid);
            if (from == null) continue;
            bool isBranch = from.nodeType is "Choice" or "Condition";
            if (!isBranch) continue;
            if (!_knotNames.ContainsKey(e.toGuid))
                _knotNames[e.toGuid] = $"Auto_{e.toGuid.Substring(0, 6)}";
        }

        var sb = new StringBuilder();
        foreach (var kvp in _knotNames)
        {
            var node = _asset.nodes.FirstOrDefault(n => n.guid == kvp.Key);
            if (node == null) continue;
            sb.AppendLine($"=== {kvp.Value} ===");
            var first = node.nodeType == "Start" ? GetNext(node.guid, 0) : node;
            WriteFlow(sb, first, node.guid);
            sb.AppendLine();
        }

        string path = EditorUtility.SaveFilePanel("ink 내보내기", Application.dataPath + "/Datas", _asset.name, "ink");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        AssetDatabase.Refresh();
        Debug.Log($"[DialogueGraph] ink 내보내기 완료 — knot {_knotNames.Count}개: {path}");
    }

    private void WriteFlow(StringBuilder sb, GraphNodeData current, string ownerGuid)
    {
        int guard = 0;
        while (current != null && guard++ < 500)
        {
            // 다른 knot의 시작점에 닿으면 다이버트하고 종료
            if (current.guid != ownerGuid && _knotNames.ContainsKey(current.guid))
            {
                sb.AppendLine($"-> {_knotNames[current.guid]}");
                return;
            }

            switch (current.nodeType)
            {
                case "Line":
                    sb.AppendLine(current.text + BuildTags(current));
                    current = GetNext(current.guid, 0);
                    break;

                case "Logic":
                    foreach (var l in current.logics) sb.AppendLine("~ " + BuildLogicCall(l));
                    current = GetNext(current.guid, 0);
                    break;

                case "Condition":
                    string cond = string.Join(" and ", current.conditions.Select(BuildConditionExpr));
                    var trueTarget = GetNext(current.guid, 0);
                    var falseTarget = GetNext(current.guid, 1);
                    sb.AppendLine($"{{{cond}:");
                    sb.AppendLine($"    -> {ResolveTarget(trueTarget)}");
                    sb.AppendLine("- else:");
                    sb.AppendLine($"    -> {ResolveTarget(falseTarget)}");
                    sb.AppendLine("}");
                    return;

                case "Choice":
                    for (int i = 0; i < current.choiceTexts.Count; i++)
                    {
                        string prefix = "+ ";
                        if (i < current.choiceConditions.Count && !string.IsNullOrEmpty(current.choiceConditions[i].key))
                            prefix += $"{{{BuildConditionExpr(current.choiceConditions[i])}}} ";
                        sb.AppendLine(prefix + current.choiceTexts[i]);
                        sb.AppendLine($"    -> {ResolveTarget(GetNext(current.guid, i))}");
                    }
                    return;

                default:
                    current = GetNext(current.guid, 0);
                    break;
            }
        }
        sb.AppendLine("-> DONE");
    }

    private string ResolveTarget(GraphNodeData target)
    => target != null && _knotNames.TryGetValue(target.guid, out var name) ? name : "DONE";

    private string BuildConditionExpr(GraphConditionEntry c)
    {
        string expr = c.type switch
        {
            GraphConditionType.HasMemory => $"has_memory(\"{c.key}\")",
            GraphConditionType.CounterAtLeast => $"get_counter(\"{c.key}\") >= {c.value}",
            GraphConditionType.AffectionAtLeast => $"get_affection(\"{c.key}\") >= {c.value}",
            GraphConditionType.SuspicionAtLeast => $"get_suspicion(\"{c.key}\") >= {c.value}",
            GraphConditionType.UnderstandingAtLeast => $"get_understanding_percent(\"{c.key}\") >= {c.value}",
            _ => "true",
        };
        return c.negate ? $"not ({expr})" : expr;
    }

    private string BuildLogicCall(GraphLogicEntry l) => l.type switch
    {
        GraphLogicType.AcquireMemory => $"acquire_memory(\"{l.key}\")",
        GraphLogicType.EraseMemory => $"erase_memory(\"{l.key}\")",
        GraphLogicType.IncrementCounter => $"increment_counter(\"{l.key}\")",
        GraphLogicType.AddAffection => $"add_affection(\"{l.key}\", {l.amount})",
        GraphLogicType.AddSuspicion => $"add_suspicion(\"{l.key}\", {l.amount})",
        GraphLogicType.AddTrust => $"add_trust_earned(\"{l.key}\", {l.amount})",
        GraphLogicType.AddLineCrossed => $"add_line_crossed(\"{l.key}\", {l.amount})",
        GraphLogicType.AddPersonalBond => $"add_personal_bond(\"{l.key}\", {l.amount})",
        _ => "",
    };

    private string BuildTags(GraphNodeData n)
    {
        var tags = new List<string>();
        if (n.isSystem) tags.Add("#system");
        else if (!string.IsNullOrEmpty(n.speakerKey)) tags.Add($"#speak:{n.speakerKey}:{n.speakerName}");
        if (n.forcePanel) tags.Add("#panel");
        if (n.autoAdvance >= 0f) tags.Add($"#auto:{n.autoAdvance}");
        if (n.lockInput) tags.Add("#lockinput");
        return tags.Count > 0 ? " " + string.Join(" ", tags) : "";
    }

    private GraphNodeData GetNext(string fromGuid, int portIndex)
    {
        var edge = _asset.edges.FirstOrDefault(e => e.fromGuid == fromGuid && e.fromPortIndex == portIndex);
        return edge == null ? null : _asset.nodes.FirstOrDefault(n => n.guid == edge.toGuid);
    }
}