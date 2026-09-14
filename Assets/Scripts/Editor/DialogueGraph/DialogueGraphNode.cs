// DialogueGraphNode.cs
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;
using System.Linq;

public class DialogueGraphNode : Node
{
    public string Guid;
    public string NodeType;
    public GraphNodeData Data = new();

    public List<Port> OutputPorts = new();

    //private Foldout _logicFold;
    //private VisualElement _logicRows;

    private VisualElement _branchContainer;

    private VisualElement _choiceContainer;

    private VisualElement _lineContainer;

    public void BuildStart()
    {
        AddToClassList("dialogue-node");
        AddToClassList("node-start");
        title = "시작 (Knot)";
        var field = new TextField("Knot 이름") { value = Data.knotName };
        field.RegisterValueChangedCallback(e => Data.knotName = e.newValue);
        mainContainer.Add(field);
        AddOutput("다음");
    }

    public void BuildLine()
    {
        AddToClassList("dialogue-node");
        AddToClassList("node-line");
        title = "대사";
        AddInput();

        mainContainer.Add(new Button(() => { Data.lines.Add(new DialogueLine()); RebuildLines(); }) { text = "+ 대사 추가" });

        _lineContainer = new VisualElement();
        mainContainer.Add(_lineContainer);

        if (Data.lines.Count == 0) Data.lines.Add(new DialogueLine());
        RebuildLines();

        AddOutput("다음");
    }

    private void RebuildLines()
    {
        _lineContainer.Clear();

        for (int i = 0; i < Data.lines.Count; i++)
        {
            int idx = i;
            var line = Data.lines[idx];

            var box = new VisualElement();
            box.AddToClassList("line-box");

            // ── 상단: 순번 + 화자 (4번 — 폴드 밖으로 노출)
            var head = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            head.Add(new Label($"{idx + 1}") { style = { width = 16, unityFontStyleAndWeight = FontStyle.Bold } });

            var keyOptions = GraphKeySource.GetNPCNames();
            keyOptions.Insert(0, "Player");
            keyOptions.Insert(0, "(내레이션)");
            int ki = Mathf.Max(0, keyOptions.IndexOf(string.IsNullOrEmpty(line.speakerKey) ? "(내레이션)" : line.speakerKey));
            var keyDd = new PopupField<string>(keyOptions, ki) { style = { width = 90 } };
            keyDd.RegisterValueChangedCallback(e =>
            {
                line.speakerKey = e.newValue == "(내레이션)" ? "" : e.newValue;
                if (string.IsNullOrEmpty(line.speakerKey)) line.isSystem = true;
            });
            head.Add(keyDd);

            var nameField = new TextField { value = line.speakerName, style = { flexGrow = 1 } };
            nameField.tooltip = "화면에 표시될 이름 (??? 등)";
            nameField.RegisterValueChangedCallback(e => line.speakerName = e.newValue);
            head.Add(nameField);

            // 순서 이동 / 삭제
            head.Add(new Button(() => MoveLine(idx, -1)) { text = "▲", style = { width = 20 } });
            head.Add(new Button(() => MoveLine(idx, 1)) { text = "▼", style = { width = 20 } });
            head.Add(new Button(() => { Data.lines.RemoveAt(idx); RebuildLines(); }) { text = "×", style = { width = 20 } });
            box.Add(head);

            // ── 대사 본문
            var text = new TextField { value = line.text, multiline = true };
            text.AddToClassList("line-text");
            text.RegisterValueChangedCallback(e => line.text = e.newValue);
            box.Add(text);

            // ── 표시 설정 (접이식)
            var settingsFold = new Foldout { text = BuildLineSettingSummary(line), value = false };
            settingsFold.AddToClassList("compact-fold");

            var panel = new Toggle("패널로 표시") { value = line.forcePanel };
            panel.RegisterValueChangedCallback(e => { line.forcePanel = e.newValue; settingsFold.text = BuildLineSettingSummary(line); });
            settingsFold.Add(panel);

            var system = new Toggle("시스템 전환") { value = line.isSystem };
            system.RegisterValueChangedCallback(e => { line.isSystem = e.newValue; settingsFold.text = BuildLineSettingSummary(line); });
            settingsFold.Add(system);

            var auto = new FloatField("자동 진행(초, -1=수동)") { value = line.autoAdvance };
            auto.RegisterValueChangedCallback(e => { line.autoAdvance = e.newValue; settingsFold.text = BuildLineSettingSummary(line); });
            settingsFold.Add(auto);

            var lockIn = new Toggle("입력 차단") { value = line.lockInput };
            lockIn.RegisterValueChangedCallback(e => { line.lockInput = e.newValue; settingsFold.text = BuildLineSettingSummary(line); });
            settingsFold.Add(lockIn);

            var cueOptions = GraphKeySource.GetCueIds();
            cueOptions.Insert(0, "(없음)");
            int ci = Mathf.Max(0, cueOptions.IndexOf(string.IsNullOrEmpty(line.cueId) ? "(없음)" : line.cueId));
            var cueDd = new PopupField<string>("연출 큐", cueOptions, ci);
            cueDd.RegisterValueChangedCallback(e =>
            {
                line.cueId = e.newValue == "(없음)" ? "" : e.newValue;
                settingsFold.text = BuildLineSettingSummary(line);
            });
            settingsFold.Add(cueDd);
            box.Add(settingsFold);

            // ── 결과 동작 (5번 — 요약 표시)
            var logicFold = new Foldout { text = BuildLogicSummary(line), value = false };
            logicFold.AddToClassList("compact-fold");
            logicFold.Add(new Button(() => { line.logics.Add(new GraphLogicEntry()); RebuildLines(); }) { text = "+ 동작 추가" });
            foreach (var row in BuildLogicRows(line, logicFold)) logicFold.Add(row);
            box.Add(logicFold);

            _lineContainer.Add(box);
        }

        RefreshExpandedState();
    }

    private void MoveLine(int idx, int dir)
    {
        int target = idx + dir;
        if (target < 0 || target >= Data.lines.Count) return;
        (Data.lines[idx], Data.lines[target]) = (Data.lines[target], Data.lines[idx]);
        RebuildLines();
    }

    /// <summary>표시 설정 요약 — 접었을 때 한눈에</summary>
    private string BuildLineSettingSummary(DialogueLine l)
    {
        var parts = new List<string>();
        if (l.isSystem) parts.Add("시스템");
        if (l.forcePanel) parts.Add("패널");
        if (l.autoAdvance >= 0f) parts.Add($"자동 {l.autoAdvance}초");
        if (l.lockInput) parts.Add("입력차단");
        if (!string.IsNullOrEmpty(l.cueId)) parts.Add($"큐:{l.cueId}");
        return parts.Count == 0 ? "표시 설정 (기본)" : "표시 설정 — " + string.Join(", ", parts);
    }

    /// <summary>결과 동작 요약 — "리엘 의심 +5" 형태</summary>
    private string BuildLogicSummary(DialogueLine l)
    {
        if (l.logics.Count == 0) return "결과 동작 (없음)";
        var parts = l.logics.Select(x =>
        {
            if (x.varType == GraphVarType.Memory) return x.isErase ? $"'{x.key}' 삭제" : $"'{x.key}' 획득";
            if (x.varType == GraphVarType.Counter) return $"{x.key} +1";
            string sign = x.amount >= 0 ? "+" : "";
            return $"{x.key} {ConditionUtil.GetVarLabel(x.varType)} {sign}{x.amount}";
        });
        return "결과 동작 — " + string.Join(", ", parts);
    }

    private List<VisualElement> BuildLogicRows(DialogueLine line, Foldout owner)
    {
        var rows = new List<VisualElement>();
        for (int i = 0; i < line.logics.Count; i++)
        {
            int idx = i;
            var l = line.logics[idx];
            var row = new VisualElement();
            row.AddToClassList("condition-row");

            var varField = new EnumField(l.varType) { style = { width = 95 } };
            varField.RegisterValueChangedCallback(e => { l.varType = (GraphVarType)e.newValue; RebuildLines(); });
            row.Add(varField);

            if (ConditionUtil.UsesFreeText(l.varType))
            {
                var tf = new TextField { value = l.key, style = { width = 110 } };
                tf.RegisterValueChangedCallback(e => { l.key = e.newValue; owner.text = BuildLogicSummary(line); });
                row.Add(tf);
            }
            else
            {
                var options = ConditionUtil.GetKeyOptions(l.varType);
                if (options.Count == 0) options.Add("(없음)");
                int index = Mathf.Max(0, options.IndexOf(l.key));
                l.key = options[index];
                var dd = new PopupField<string>(options, index) { style = { width = 110 } };
                dd.RegisterValueChangedCallback(e => { l.key = e.newValue; owner.text = BuildLogicSummary(line); });
                row.Add(dd);
            }

            if (l.varType == GraphVarType.Memory)
            {
                var erase = new Toggle("삭제") { value = l.isErase };
                erase.RegisterValueChangedCallback(e => { l.isErase = e.newValue; owner.text = BuildLogicSummary(line); });
                row.Add(erase);
            }
            else if (l.varType != GraphVarType.Counter)
            {
                var amount = new IntegerField { value = l.amount, style = { width = 45 } };
                amount.RegisterValueChangedCallback(e => { l.amount = e.newValue; owner.text = BuildLogicSummary(line); });
                row.Add(amount);
            }

            row.Add(new Button(() => { line.logics.RemoveAt(idx); RebuildLines(); }) { text = "×", style = { width = 20 } });
            rows.Add(row);
        }
        return rows;
    }

    public void BuildBranch()
    {
        AddToClassList("dialogue-node");
        AddToClassList("node-branch");
        title = "조건 분기";
        AddInput();

        var countField = new IntegerField("분기 개수") { value = Mathf.Max(1, Data.branchCases.Count) };
        countField.RegisterValueChangedCallback(e =>
        {
            int n = Mathf.Clamp(e.newValue, 1, 10);
            while (Data.branchCases.Count < n) Data.branchCases.Add(new BranchCase());
            while (Data.branchCases.Count > n) Data.branchCases.RemoveAt(Data.branchCases.Count - 1);
            RebuildBranchPorts();
        });
        mainContainer.Add(countField);

        _branchContainer = new VisualElement();
        mainContainer.Add(_branchContainer);

        if (Data.branchCases.Count == 0) Data.branchCases.Add(new BranchCase());
        RebuildBranchPorts();
    }

    private void RebuildBranchPorts()
    {
        RemoveAllOutputPorts();
        _branchContainer.Clear();

        for (int i = 0; i < Data.branchCases.Count; i++)
        {
            int idx = i;
            var bc = Data.branchCases[idx];
            AddOutput($"분기 {idx + 1}");

            var fold = new Foldout { value = false };
            fold.text = $"분기 {idx + 1}: {ConditionUtil.BuildSummary(bc.condition)}";
            fold.Add(new ConditionGroupElement(bc.condition,
                () => fold.text = $"분기 {idx + 1}: {ConditionUtil.BuildSummary(bc.condition)}")); // ★ 하나만
            _branchContainer.Add(fold);
        }

        AddOutput("그 외 (else)"); // 마지막은 항상 else
        RefreshExpandedState();
        RefreshPorts();
    }

    public void BuildChoice()
    {
        AddToClassList("dialogue-node");
        AddToClassList("node-choice");
        title = "선택지";
        AddInput();

        mainContainer.Add(new Button(() => { Data.choiceOptions.Add(new ChoiceOption()); RebuildChoiceRows(); }) { text = "+ 선택지 추가" });
        _choiceContainer = new VisualElement();
        mainContainer.Add(_choiceContainer);

        if (Data.choiceOptions.Count == 0) Data.choiceOptions.Add(new ChoiceOption());
        RebuildChoiceRows();
    }

    private void RebuildChoiceRows()
    {
        RemoveAllOutputPorts();
        _choiceContainer.Clear();

        for (int i = 0; i < Data.choiceOptions.Count; i++)
        {
            int idx = i;
            var opt = Data.choiceOptions[idx];
            AddOutput($"선택 {idx + 1}");

            var box = new VisualElement();
            box.AddToClassList("choice-box");

            var head = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var tf = new TextField { value = opt.text, style = { flexGrow = 1 } };
            tf.RegisterValueChangedCallback(e => opt.text = e.newValue);
            head.Add(tf);
            head.Add(new Button(() => { Data.choiceOptions.RemoveAt(idx); RebuildChoiceRows(); }) { text = "×", style = { width = 20 } }); // ★ 3번 — 삭제 버튼
            box.Add(head);

            var fold = new Foldout { text = $"표시 조건: {ConditionUtil.BuildSummary(opt.condition)}", value = false };
            fold.Add(new ConditionGroupElement(opt.condition,
                () => fold.text = $"표시 조건: {ConditionUtil.BuildSummary(opt.condition)}"));
            box.Add(fold);

            _choiceContainer.Add(box);
        }
        RefreshExpandedState();
        RefreshPorts();
    }

    private void AddInput()
    {
        var p = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        p.portName = "입력";
        inputContainer.Add(p);
    }

    private Port AddOutput(string name)
    {
        var p = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        p.portName = name;
        outputContainer.Add(p);
        OutputPorts.Add(p);
        RefreshExpandedState();
        RefreshPorts();
        return p;
    }

    private void RemoveAllOutputPorts()
    {
        foreach (var p in OutputPorts)
        {
            // 연결된 엣지부터 정리
            foreach (var edge in p.connections.ToList())
            {
                edge.input?.Disconnect(edge);
                edge.output?.Disconnect(edge);
                edge.RemoveFromHierarchy();
            }
            outputContainer.Remove(p);
        }
        OutputPorts.Clear();
    }
}