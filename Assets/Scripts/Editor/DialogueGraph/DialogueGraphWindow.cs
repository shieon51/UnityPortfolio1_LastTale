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

    private ScrollView _issuePanel;

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
        toolbar.Add(new Button(Validate) { text = "검증" });
        rootVisualElement.Add(toolbar);

        //결과 패널
        _issuePanel = new ScrollView();
        _issuePanel.style.position = Position.Absolute;
        _issuePanel.style.right = 0;
        _issuePanel.style.top = 20;
        _issuePanel.style.width = 340;
        _issuePanel.style.maxHeight = 400;
        _issuePanel.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.96f);
        _issuePanel.style.display = DisplayStyle.None;
        rootVisualElement.Add(_issuePanel);
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

        var issues = GraphValidator.Validate(_asset);
        int errorCount = issues.Count(i => i.severity == ValidationIssue.Severity.Error);
        if (errorCount > 0)
        {
            bool proceed = EditorUtility.DisplayDialog("검증 오류",
                $"오류 {errorCount}개가 발견되었습니다.\n그래도 내보낼까요?\n\n(검증 버튼으로 상세 내용을 확인할 수 있습니다)",
                "그래도 내보내기", "취소");
            if (!proceed) return;
        }

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

        // 3) 여러 곳에서 들어오는 노드도 별도 knot으로 (중복 출력 방지)
        var inboundCount = new Dictionary<string, int>();
        foreach (var e in _asset.edges)
        {
            inboundCount.TryGetValue(e.toGuid, out int c);
            inboundCount[e.toGuid] = c + 1;
        }
        foreach (var kvp in inboundCount)
        {
            if (kvp.Value < 2 || _knotNames.ContainsKey(kvp.Key)) continue;
            _knotNames[kvp.Key] = $"Auto_{kvp.Key.Substring(0, 6)}";
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
        RegisterToMainInk(path);   // ★ 추가
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

    private void RegisterToMainInk(string exportedPath)
    {
        string mainInkPath = Path.Combine(Application.dataPath, "Datas", "main.ink");
        if (!File.Exists(mainInkPath))
        {
            Debug.LogWarning($"[DialogueGraph] main.ink를 찾을 수 없어 INCLUDE 등록을 건너뜁니다: {mainInkPath}");
            return;
        }

        // Assets/Datas 기준 상대 경로 계산
        string datasRoot = Path.Combine(Application.dataPath, "Datas");
        string relative = Path.GetRelativePath(datasRoot, exportedPath).Replace('/', '\\');
        string includeLine = $"INCLUDE {relative}";

        string allText = File.ReadAllText(mainInkPath);
        if (allText.Contains(includeLine))
        {
            Debug.Log($"[DialogueGraph] main.ink에 이미 등록됨: {includeLine}");
            return;
        }

        File.AppendAllText(mainInkPath, "\n" + includeLine, new UTF8Encoding(true));
        Debug.Log($"[DialogueGraph] main.ink에 등록: {includeLine}");
    }

    private void Validate()
    {
        if (_asset == null) { EditorUtility.DisplayDialog("오류", "그래프 애셋을 먼저 지정하세요.", "확인"); return; }
        Save();

        var issues = GraphValidator.Validate(_asset);
        _issuePanel.Clear();
        _issuePanel.style.display = DisplayStyle.Flex;

        var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
        int errors = issues.Count(i => i.severity == ValidationIssue.Severity.Error);
        int warns = issues.Count - errors;
        header.Add(new Label(issues.Count == 0 ? "문제 없음" : $"오류 {errors} / 경고 {warns}")
        { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 6 } });
        header.Add(new Button(() => _issuePanel.style.display = DisplayStyle.None) { text = "×" });
        _issuePanel.Add(header);

        foreach (var issue in issues.OrderBy(i => i.severity))
        {
            var btn = new Button(() => FocusNode(issue.nodeGuid)) { text = (issue.severity == ValidationIssue.Severity.Error ? "● " : "▲ ") + issue.message };
            btn.style.whiteSpace = WhiteSpace.Normal;
            btn.style.unityTextAlign = TextAnchor.MiddleLeft;
            btn.style.color = issue.severity == ValidationIssue.Severity.Error
                ? new Color(1f, 0.5f, 0.5f) : new Color(1f, 0.85f, 0.5f);
            _issuePanel.Add(btn);
        }

        Debug.Log($"[DialogueGraph] 검증 완료 — 오류 {errors}개, 경고 {warns}개");
    }

    private void FocusNode(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return;
        var node = _graph.nodes.Cast<DialogueGraphNode>().FirstOrDefault(n => n.Guid == guid);
        if (node == null) return;
        _graph.ClearSelection();
        _graph.AddToSelection(node);
        _graph.FrameSelection();
    }
}