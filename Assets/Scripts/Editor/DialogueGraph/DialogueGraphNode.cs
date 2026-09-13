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

    private Foldout _logicFold;
    private VisualElement _logicRows;

    private VisualElement _branchContainer;

    private VisualElement _choiceContainer;

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

        var text = new TextField("대사") { value = Data.text, multiline = true };
        text.style.minHeight = 55;
        text.RegisterValueChangedCallback(e => Data.text = e.newValue);
        mainContainer.Add(text);

        // 화자/표시 설정 — 접이식
        var speakerFold = new Foldout { text = "화자 / 표시 설정", value = false };
        var keyOptions = GraphKeySource.GetNPCNames();
        keyOptions.Insert(0, "Player");
        keyOptions.Insert(0, "(없음)");
        int ki = Mathf.Max(0, keyOptions.IndexOf(Data.speakerKey));
        var keyDd = new PopupField<string>("화자", keyOptions, ki);
        keyDd.RegisterValueChangedCallback(e => Data.speakerKey = e.newValue == "(없음)" ? "" : e.newValue);
        speakerFold.Add(keyDd);

        var nameField = new TextField("표시 이름") { value = Data.speakerName };
        nameField.RegisterValueChangedCallback(e => Data.speakerName = e.newValue);
        speakerFold.Add(nameField);

        var panel = new Toggle("패널로 표시") { value = Data.forcePanel };
        panel.RegisterValueChangedCallback(e => Data.forcePanel = e.newValue);
        speakerFold.Add(panel);

        var system = new Toggle("시스템 전환") { value = Data.isSystem };
        system.RegisterValueChangedCallback(e => Data.isSystem = e.newValue);
        speakerFold.Add(system);

        var auto = new FloatField("자동 진행(초, -1=수동)") { value = Data.autoAdvance };
        auto.RegisterValueChangedCallback(e => Data.autoAdvance = e.newValue);
        speakerFold.Add(auto);

        var lockIn = new Toggle("입력 차단") { value = Data.lockInput };
        lockIn.RegisterValueChangedCallback(e => Data.lockInput = e.newValue);
        speakerFold.Add(lockIn);

        var cueOptions = GraphKeySource.GetCueIds();
        cueOptions.Insert(0, "(없음)");
        int ci = Mathf.Max(0, cueOptions.IndexOf(Data.cueId));
        var cueDd = new PopupField<string>("연출 큐", cueOptions, ci);
        cueDd.RegisterValueChangedCallback(e => Data.cueId = e.newValue == "(없음)" ? "" : e.newValue);
        speakerFold.Add(cueDd);

        mainContainer.Add(speakerFold);

        // ★ 로직을 대사 노드에 통합 — 별도 노드 불필요
        _logicFold = new Foldout { text = BuildLogicFoldTitle(), value = false };
        _logicFold.Add(new Button(() => { Data.logics.Add(new GraphLogicEntry()); RebuildLogicRows(); }) { text = "+ 동작 추가" });
        _logicRows = new VisualElement();
        _logicFold.Add(_logicRows);
        mainContainer.Add(_logicFold);
        RebuildLogicRows();

        AddOutput("다음");
    }

    private string BuildLogicFoldTitle()
        => Data.logics.Count == 0 ? "결과 동작 (없음)" : $"결과 동작 ({Data.logics.Count}개)";

    private void RebuildLogicRows()
    {
        _logicRows.Clear();
        for (int i = 0; i < Data.logics.Count; i++)
        {
            int idx = i;
            var l = Data.logics[idx];
            var row = new VisualElement();
            row.AddToClassList("condition-row");

            var varField = new EnumField(l.varType) { style = { width = 95 } };
            varField.RegisterValueChangedCallback(e => { l.varType = (GraphVarType)e.newValue; RebuildLogicRows(); });
            row.Add(varField);

            if (ConditionUtil.UsesFreeText(l.varType))
            {
                var tf = new TextField { value = l.key, style = { width = 120 } };
                tf.RegisterValueChangedCallback(e => l.key = e.newValue);
                row.Add(tf);
            }
            else
            {
                var options = ConditionUtil.GetKeyOptions(l.varType);
                if (options.Count == 0) options.Add("(없음)");
                int index = Mathf.Max(0, options.IndexOf(l.key));
                l.key = options[index];
                var dd = new PopupField<string>(options, index) { style = { width = 120 } };
                dd.RegisterValueChangedCallback(e => l.key = e.newValue);
                row.Add(dd);
            }

            if (l.varType == GraphVarType.Memory)
            {
                var erase = new Toggle("삭제") { value = l.isErase };
                erase.RegisterValueChangedCallback(e => l.isErase = e.newValue);
                row.Add(erase);
            }
            else if (l.varType != GraphVarType.Counter)
            {
                var amount = new IntegerField { value = l.amount, style = { width = 45 } };
                amount.RegisterValueChangedCallback(e => l.amount = e.newValue);
                row.Add(amount);
            }

            row.Add(new Button(() => { Data.logics.RemoveAt(idx); RebuildLogicRows(); }) { text = "×", style = { width = 20 } });
            _logicRows.Add(row);
        }
        if (_logicFold != null) _logicFold.text = BuildLogicFoldTitle();
        RefreshExpandedState();
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