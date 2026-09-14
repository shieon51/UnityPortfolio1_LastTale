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
            bool isBranch = from.nodeType is "Choice" or "Branch";
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
                    foreach (var line in current.lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line.text)) sb.AppendLine(line.text + BuildTags(line));
                        foreach (var l in line.logics) sb.AppendLine("~ " + BuildLogicCall(l));
                    }
                    current = GetNext(current.guid, 0);
                    break;

                case "Branch":
                    sb.AppendLine("{");
                    for (int i = 0; i < current.branchCases.Count; i++)
                    {
                        sb.AppendLine($"    - {ConditionUtil.ToInkExpr(current.branchCases[i].condition)}:");
                        sb.AppendLine($"        -> {ResolveTarget(GetNext(current.guid, i))}");
                    }
                    sb.AppendLine("    - else:");
                    sb.AppendLine($"        -> {ResolveTarget(GetNext(current.guid, current.branchCases.Count))}");
                    sb.AppendLine("}");
                    return;

                case "Choice":
                    for (int i = 0; i < current.choiceOptions.Count; i++)
                    {
                        var opt = current.choiceOptions[i];
                        string prefix = "+ ";
                        if (opt.condition.entries.Count > 0) prefix += $"{{{ConditionUtil.ToInkExpr(opt.condition)}}} ";
                        sb.AppendLine(prefix + opt.text);
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

    private string BuildLogicCall(GraphLogicEntry l) => l.varType switch
    {
        GraphVarType.Memory => l.isErase ? $"erase_memory(\"{l.key}\")" : $"acquire_memory(\"{l.key}\")",
        GraphVarType.Counter => $"increment_counter(\"{l.key}\")",
        GraphVarType.Affection => $"add_affection(\"{l.key}\", {l.amount})",
        GraphVarType.Suspicion => $"add_suspicion(\"{l.key}\", {l.amount})",
        GraphVarType.TrustEarned => $"add_trust_earned(\"{l.key}\", {l.amount})",
        GraphVarType.LineCrossed => $"add_line_crossed(\"{l.key}\", {l.amount})",
        GraphVarType.PersonalBond => $"add_personal_bond(\"{l.key}\", {l.amount})",
        _ => "",
    };

    private string BuildTags(DialogueLine n)
    {
        var tags = new List<string>();
        if (n.isSystem) tags.Add("#system");
        else if (!string.IsNullOrEmpty(n.speakerKey)) tags.Add($"#speak:{n.speakerKey}:{n.speakerName}");
        if (n.forcePanel) tags.Add("#panel");
        if (n.autoAdvance >= 0f) tags.Add($"#auto:{n.autoAdvance}");
        if (n.lockInput) tags.Add("#lockinput");
        if (!string.IsNullOrEmpty(n.cueId)) tags.Add($"#cue:{n.cueId}");
        return tags.Count > 0 ? " " + string.Join(" ", tags) : "";
    }

    private GraphNodeData GetNext(string fromGuid, int portIndex)
    {
        var edge = _asset.edges.FirstOrDefault(e => e.fromGuid == fromGuid && e.fromPortIndex == portIndex);
        return edge == null ? null : _asset.nodes.FirstOrDefault(n => n.guid == edge.toGuid);
    }
}