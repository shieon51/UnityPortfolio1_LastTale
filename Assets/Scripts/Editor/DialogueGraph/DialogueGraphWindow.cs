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
    // DialogueGraphWindow.cs — 분할 내보내기 추가
    private enum SplitMode { Single, ByDay, ByNPC }

    private DialogueGraphView _graph;
    private DialogueGraphData _asset;

    private Dictionary<string, string> _knotNames; // guid → knot 이름

    private ScrollView _issuePanel;

    private GraphBlackboard _blackboard;

    private GraphFilter _filter = new();

    [MenuItem("LastMarchan/Dialogue Graph Editor")]
    public static void Open() => GetWindow<DialogueGraphWindow>("대화 그래프");

    private void OnEnable()
    {
        _graph = new DialogueGraphView();
        rootVisualElement.Add(_graph);

        var toolbar = new UnityEditor.UIElements.Toolbar();
        var assetField = new UnityEditor.UIElements.ObjectField("그래프 애셋") { objectType = typeof(DialogueGraphData) };
        assetField.RegisterValueChangedCallback(e =>
        {
            _asset = e.newValue as DialogueGraphData;
            if (_blackboard != null && _blackboard.IsOpen) _blackboard.Refresh(_asset);
        });
        toolbar.Add(assetField);
        toolbar.Add(new Button(Save) { text = "저장" });
        toolbar.Add(new Button(Load) { text = "불러오기" });
        toolbar.Add(new Button(ImportInk) { text = "ink 가져오기" });
        toolbar.Add(new Button(ExportInk) { text = "ink 내보내기" });
        toolbar.Add(new Button(() => ExportInkSplit(SplitMode.ByDay)) { text = "Day별 내보내기" });
        toolbar.Add(new Button(() => ExportInkSplit(SplitMode.ByNPC)) { text = "NPC별 내보내기" });
        toolbar.Add(new Button(Validate) { text = "검증" });
        toolbar.Add(new Button(() => _blackboard.Toggle(_asset)) { text = "변수 목록" });

        rootVisualElement.Add(toolbar);

        // 필터 관련
        var filterBar = new UnityEditor.UIElements.Toolbar();
        filterBar.style.position = Position.Absolute;
        filterBar.style.top = 20;   // 기존 toolbar 아래
        filterBar.style.left = 0;
        filterBar.style.right = 0;

        var dayFilter = new IntegerField("Day") { value = 0, style = { width = 80 } };
        dayFilter.labelElement.style.minWidth = 30;   // ★ 라벨 폭 고정
        dayFilter.labelElement.style.width = 30;
        dayFilter.RegisterValueChangedCallback(e => { _filter.day = e.newValue; _graph.ApplyFilter(_filter); });
        filterBar.Add(dayFilter);

        var npcOptions = GraphKeySource.GetNPCNames();
        npcOptions.Insert(0, "(전체)");
        var npcFilter = new PopupField<string>(npcOptions, 0) { style = { width = 110 } };
        npcFilter.RegisterValueChangedCallback(e =>
        {
            _filter.npcTag = e.newValue == "(전체)" ? "" : e.newValue;
            _graph.ApplyFilter(_filter);
        });
        filterBar.Add(npcFilter);

        var colorOptions = new List<string> { "(전체)", "빨강", "주황", "노랑", "초록", "파랑", "보라" };
        var colorFilter = new PopupField<string>(colorOptions, 0) { style = { width = 80 } };
        colorFilter.RegisterValueChangedCallback(e =>
        {
            _filter.colorTag = e.newValue == "(전체)" ? "" : e.newValue;
            _graph.ApplyFilter(_filter);
        });
        filterBar.Add(colorFilter);

        var hourFilter = new IntegerField("시각") { value = -1, style = { width = 80 } };
        hourFilter.labelElement.style.minWidth = 36;  // ★
        hourFilter.labelElement.style.width = 36;
        hourFilter.RegisterValueChangedCallback(e => { _filter.hour = e.newValue; _graph.ApplyFilter(_filter); });
        filterBar.Add(hourFilter);

        var search = new UnityEditor.UIElements.ToolbarSearchField();
        search.RegisterValueChangedCallback(e => { _filter.searchText = e.newValue; _graph.ApplyFilter(_filter); });
        filterBar.Add(search);

        filterBar.Add(new Button(() =>
        {
            _filter = new GraphFilter();
            dayFilter.SetValueWithoutNotify(0);
            npcFilter.SetValueWithoutNotify("(전체)");
            colorFilter.SetValueWithoutNotify("(전체)");
            hourFilter.SetValueWithoutNotify(-1);
            search.SetValueWithoutNotify("");
            _graph.ApplyFilter(_filter);
        })
        { text = "필터 해제" });

        filterBar.Add(new Button(() => _graph.AutoLayout(_filter)) { text = "자동 정렬" });

        filterBar.Add(new Button(() =>
        {
            foreach (var g in _graph.graphElements.OfType<Group>().ToList()) _graph.RemoveElement(g);
        })
        { text = "그룹 해제" });

        filterBar.Add(new Button(() =>
        {
            GraphKeySource.InvalidateCache();
            var fresh = GraphKeySource.GetNPCNames();
            fresh.Insert(0, "(전체)");
            npcFilter.choices = fresh;   // ★ PopupField의 목록 교체
        })
        { text = "목록 갱신" });

        rootVisualElement.Add(filterBar);


        // 결과 패널
        _issuePanel = new ScrollView();
        _issuePanel.style.position = Position.Absolute;
        _issuePanel.style.right = 0;
        _issuePanel.style.top = 40;
        _issuePanel.style.width = 340;
        _issuePanel.style.maxHeight = 400;
        _issuePanel.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.96f);
        _issuePanel.style.display = DisplayStyle.None;
        rootVisualElement.Add(_issuePanel);

        // 블랙보드
        _blackboard = new GraphBlackboard(this);
        _blackboard.OnLayoutChanged = UpdateIssuePanelPosition;   // ★ 검증창 연동
        rootVisualElement.Add(_blackboard);

        // QA 관련
        _graph.OnAnalyzeRequested = AnalyzePath;
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
        RefreshDirtyMarks(); //?
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

        _graph.ApplyFilter(_filter);
        RefreshDirtyMarks();
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

        BuildKnotNames();   // ★ 기존의 1) 2) 3) 블록 전체를 이 한 줄로 교체

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

    private void ExportInkSplit(SplitMode mode)
    {
        if (_asset == null) { EditorUtility.DisplayDialog("오류", "그래프 애셋을 먼저 지정하세요.", "확인"); return; }
        Save();

        var issues = GraphValidator.Validate(_asset);
        int errorCount = issues.Count(i => i.severity == ValidationIssue.Severity.Error);
        if (errorCount > 0)
        {
            bool proceed = EditorUtility.DisplayDialog("검증 오류",
                $"오류 {errorCount}개가 발견되었습니다.\n그래도 내보낼까요?", "그래도 내보내기", "취소");
            if (!proceed) return;
        }

        string folder = EditorUtility.SaveFolderPanel("내보낼 폴더 선택", Application.dataPath + "/Datas", "");
        if (string.IsNullOrEmpty(folder)) return;

        BuildKnotNames();

        // 그룹 키별로 knot 노드를 분류
        var groups = new Dictionary<string, List<GraphNodeData>>();
        foreach (var kvp in _knotNames)
        {
            var node = _asset.nodes.FirstOrDefault(n => n.guid == kvp.Key);
            if (node == null) continue;
            string key = mode switch
            {
                SplitMode.ByDay => node.day > 0 ? $"Day{node.day}" : "Common",
                SplitMode.ByNPC => string.IsNullOrEmpty(node.npcTag) ? "Common" : node.npcTag,
                _ => _asset.name,
            };
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<GraphNodeData>();
            list.Add(node);
        }

        int exported = 0, skipped = 0;

        foreach (var g in groups)
        {
            // ★ 이 그룹에 속한 knot들과, 거기서 도달 가능한 모든 노드를 검사 대상으로
            var affected = CollectReachable(g.Value);
            bool anyDirty = affected.Any(IsNodeDirty);
            if (!anyDirty) { skipped++; continue; }

            var sb = new StringBuilder();
            foreach (var node in g.Value)
            {
                sb.AppendLine($"=== {_knotNames[node.guid]} ===");
                var first = node.nodeType == "Start" ? GetNext(node.guid, 0) : node;
                WriteFlow(sb, first, node.guid);
                sb.AppendLine();
            }

            // NPC별 모드면 하위 폴더 생성
            string dir = folder;
            if (mode == SplitMode.ByNPC && g.Key != "Common")
            {
                dir = Path.Combine(folder, g.Key);
                Directory.CreateDirectory(dir);
            }

            string path = Path.Combine(dir, $"{g.Key}.ink");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            RegisterToMainInk(path);

            // ★ 내보낸 노드들의 해시 갱신
            foreach (var n in affected) n.lastExportHash = ComputeHash(n);
            exported++;
        }

        EditorUtility.SetDirty(_asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshDirtyMarks();

        Debug.Log($"[DialogueGraph] 분할 내보내기 — 생성 {exported}개, 변경 없어 건너뜀 {skipped}개");
    }

    /// <summary>주어진 knot 노드들에서 도달 가능한 모든 노드 수집</summary>
    private List<GraphNodeData> CollectReachable(List<GraphNodeData> roots)
    {
        var visited = new HashSet<string>();
        var stack = new Stack<string>(roots.Select(r => r.guid));

        while (stack.Count > 0)
        {
            string guid = stack.Pop();
            if (!visited.Add(guid)) continue;
            foreach (var e in _asset.edges.Where(e => e.fromGuid == guid))
                stack.Push(e.toGuid);
        }
        return _asset.nodes.Where(n => visited.Contains(n.guid)).ToList();
    }

    /// <summary>노드 내용이 마지막 내보내기 이후 변경되었는지</summary>
    private bool IsNodeDirty(GraphNodeData n) => n.lastExportHash != ComputeHash(n);

    private string ComputeHash(GraphNodeData node)
    {
        var sb = new StringBuilder();
        sb.Append(node.knotName).Append('|').Append(node.day).Append('|').Append(node.npcTag).Append('|');

        foreach (var l in node.lines)
        {
            sb.Append(l.text).Append(l.speakerKey).Append(l.speakerName)
              .Append(l.forcePanel).Append(l.isSystem).Append(l.autoAdvance).Append(l.lockInput).Append(l.cueId);
            foreach (var lg in l.logics) sb.Append(lg.varType).Append(lg.key).Append(lg.amount).Append(lg.isErase);
        }
        foreach (var o in node.choiceOptions)
        {
            sb.Append(o.text);
            foreach (var c in o.condition.entries) sb.Append(c.varType).Append(c.key).Append(c.op).Append(c.value);
        }
        foreach (var b in node.branchCases)
            foreach (var c in b.condition.entries) sb.Append(c.varType).Append(c.key).Append(c.op).Append(c.value);

        foreach (var e in _asset.edges.Where(e => e.fromGuid == node.guid).OrderBy(e => e.fromPortIndex))
            sb.Append(e.fromPortIndex).Append(e.toGuid);

        return sb.ToString().GetHashCode().ToString();
    }

    /// <summary>변경된 노드 제목에 * 표시</summary>
    private void RefreshDirtyMarks()
    {
        if (_asset == null) return;
        foreach (var node in _graph.nodes.Cast<DialogueGraphNode>())
            node.SetDirtyMark(IsNodeDirty(node.Data));
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
        UpdateIssuePanelPosition();

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

    private void BuildKnotNames()
    {
        _knotNames = new Dictionary<string, string>();

        // 1) Start 노드는 지정된 이름 사용
        foreach (var n in _asset.nodes.Where(n => n.nodeType == "Start"))
            _knotNames[n.guid] = string.IsNullOrEmpty(n.knotName) ? $"Knot_{n.guid.Substring(0, 6)}" : n.knotName;

        // 2) 분기 대상이 되는 노드는 자동 knot 부여
        foreach (var e in _asset.edges)
        {
            var from = _asset.nodes.FirstOrDefault(n => n.guid == e.fromGuid);
            if (from == null) continue;
            if (from.nodeType is not ("Choice" or "Branch")) continue;
            if (!_knotNames.ContainsKey(e.toGuid))
                _knotNames[e.toGuid] = $"Auto_{e.toGuid.Substring(0, 6)}";
        }

        // 3) 여러 곳에서 진입하는 노드도 별도 knot으로 (중복 출력 방지)
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
    }

    private void ImportInk()
    {
        if (_asset == null) { EditorUtility.DisplayDialog("오류", "그래프 애셋을 먼저 지정하세요.", "확인"); return; }

        string path = EditorUtility.OpenFilePanel("가져올 ink 파일", Application.dataPath + "/Datas", "ink");
        if (string.IsNullOrEmpty(path)) return;

        var warnings = new List<string>();
        Undo.RecordObject(_asset, "Import Ink");
        InkGraphImporter.Import(_asset, path, warnings);
        EditorUtility.SetDirty(_asset);
        AssetDatabase.SaveAssets();
        GraphKeySource.InvalidateCache();

        Load();

        foreach (var w in warnings) Debug.LogWarning($"[ink 가져오기] {w}");
        EditorUtility.DisplayDialog("가져오기 완료",
            warnings.Count == 0
                ? "문제 없이 가져왔습니다."
                : $"가져왔으나 {warnings.Count}건은 수동 정리가 필요합니다.\n콘솔 로그를 확인하세요.\n\n주로 조건 블록({{...}})이 해당됩니다.",
            "확인");
    }

    /// <summary>특정 키를 사용하는 노드만 선명하게 표시하고 화면에 맞춤</summary>
    public void HighlightNodesUsing(string key)
    {
        if (_asset == null) return;
        var usage = GraphBlackboard.CountUsage(_asset);
        if (!usage.TryGetValue(key, out var users) || users.Count == 0)
        {
            Debug.Log($"[DialogueGraph] '{key}'를 사용하는 노드가 없습니다.");
            return;
        }

        var guids = new HashSet<string>(users.Select(u => u.guid));
        foreach (var node in _graph.nodes.Cast<DialogueGraphNode>())
            node.SetFocused(guids.Contains(node.Guid));

        _graph.ClearSelection();
        foreach (var node in _graph.nodes.Cast<DialogueGraphNode>().Where(n => guids.Contains(n.Guid)))
            _graph.AddToSelection(node);
        _graph.FrameSelection();
    }

    private void AnalyzePath(string startGuid)
    {
        Save();
        var paths = GraphPathAnalyzer.Trace(_asset, startGuid);
        string report = GraphPathAnalyzer.Format(paths);

        _issuePanel.Clear();
        _issuePanel.style.display = DisplayStyle.Flex;
        UpdateIssuePanelPosition();

        var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
        header.Add(new Label("경로 분석") { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 6 } });
        header.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = report) { text = "복사" });
        header.Add(new Button(() => _issuePanel.style.display = DisplayStyle.None) { text = "×" });
        _issuePanel.Add(header);

        var label = new Label(report);
        label.enableRichText = true;   // ★ 추가
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.paddingLeft = 6;
        label.style.paddingRight = 10;
        _issuePanel.Add(label);

        Debug.Log($"[DialogueGraph] 경로 분석 완료 — {paths.Count}개\n{report}");
    }

    private void UpdateIssuePanelPosition()
    {
        if (_issuePanel == null) return;
        // 변수 목록이 열려 있으면 그 왼쪽에, 닫혀 있으면 화면 오른쪽 끝에
        _issuePanel.style.right = (_blackboard != null && _blackboard.IsOpen) ? _blackboard.CurrentWidth : 0f;
    }
}