// DialogueGraphNode.cs
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;

public class DialogueGraphNode : Node
{
    public string Guid;
    public string NodeType;
    public GraphNodeData Data = new();

    public List<Port> OutputPorts = new();

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

        var speakerKey = new TextField("화자 키") { value = Data.speakerKey };
        speakerKey.RegisterValueChangedCallback(e => Data.speakerKey = e.newValue);
        mainContainer.Add(speakerKey);

        var speakerName = new TextField("표시 이름") { value = Data.speakerName };
        speakerName.RegisterValueChangedCallback(e => Data.speakerName = e.newValue);
        mainContainer.Add(speakerName);

        var text = new TextField("대사") { value = Data.text, multiline = true };
        text.style.minHeight = 60;
        text.RegisterValueChangedCallback(e => Data.text = e.newValue);
        mainContainer.Add(text);

        var panel = new Toggle("패널로 표시(#panel)") { value = Data.forcePanel };
        panel.RegisterValueChangedCallback(e => Data.forcePanel = e.newValue);
        mainContainer.Add(panel);

        var system = new Toggle("시스템 전환(#system)") { value = Data.isSystem };
        system.RegisterValueChangedCallback(e => Data.isSystem = e.newValue);
        mainContainer.Add(system);

        var auto = new FloatField("자동 진행(초, -1=수동)") { value = Data.autoAdvance };
        auto.RegisterValueChangedCallback(e => Data.autoAdvance = e.newValue);
        mainContainer.Add(auto);

        var lockIn = new Toggle("입력 차단(#lockinput)") { value = Data.lockInput };
        lockIn.RegisterValueChangedCallback(e => Data.lockInput = e.newValue);
        mainContainer.Add(lockIn);

        AddOutput("다음");
    }

    public void BuildChoice()
    {
        AddToClassList("dialogue-node");
        AddToClassList("node-choice");
        title = "선택지";
        AddInput();

        var addBtn = new Button(() => AddChoiceRow("새 선택지")) { text = "+ 선택지 추가" };
        mainContainer.Add(addBtn);

        if (Data.choiceTexts.Count == 0) Data.choiceTexts.Add("선택지 1");
        foreach (var t in new List<string>(Data.choiceTexts)) AddChoiceRow(t, true);
    }

    private void AddChoiceRow(string text, bool existing = false)
    {
        int index = OutputPorts.Count;
        if (!existing) Data.choiceTexts.Add(text);

        var port = AddOutput($"선택 {index + 1}");
        var field = new TextField { value = text };
        field.RegisterValueChangedCallback(e =>
        {
            if (index < Data.choiceTexts.Count) Data.choiceTexts[index] = e.newValue;
        });
        port.contentContainer.Add(field);
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

    public void BuildCondition()
    {
        AddToClassList("dialogue-node");
        AddToClassList("node-condition");
        title = "조건 분기";
        AddInput();

        var addBtn = new Button(() => { Data.conditions.Add(new GraphConditionEntry()); RebuildConditionRows(); }) { text = "+ 조건 추가" };
        mainContainer.Add(addBtn);

        _conditionContainer = new VisualElement();
        mainContainer.Add(_conditionContainer);
        if (Data.conditions.Count == 0) Data.conditions.Add(new GraphConditionEntry());
        RebuildConditionRows();

        AddOutput("참 (True)");
        AddOutput("거짓 (False)");
    }

    private VisualElement _conditionContainer;

    private void RebuildConditionRows()
    {
        _conditionContainer.Clear();
        for (int i = 0; i < Data.conditions.Count; i++)
        {
            int idx = i;
            var entry = Data.conditions[idx];
            var row = new VisualElement();
            row.AddToClassList("choice-row");

            var typeField = new EnumField(entry.type);
            typeField.RegisterValueChangedCallback(e =>
            {
                entry.type = (GraphConditionType)e.newValue;
                RebuildConditionRows();
            });
            row.Add(typeField);

            row.Add(BuildKeyField(entry.key, GraphKeySource.UsesFreeText(entry.type),
                GraphKeySource.GetKeysFor(entry.type), v => entry.key = v));

            if (entry.type != GraphConditionType.HasMemory)
            {
                var valField = new IntegerField { value = entry.value, style = { width = 50 } };
                valField.RegisterValueChangedCallback(e => entry.value = e.newValue);
                row.Add(valField);
            }

            var negate = new Toggle("NOT") { value = entry.negate };
            negate.RegisterValueChangedCallback(e => entry.negate = e.newValue);
            row.Add(negate);

            var del = new Button(() => { Data.conditions.RemoveAt(idx); RebuildConditionRows(); }) { text = "×" };
            row.Add(del);

            _conditionContainer.Add(row);
        }
        RefreshExpandedState();
    }

    public void BuildLogic()
    {
        AddToClassList("dialogue-node");
        AddToClassList("node-logic");
        title = "로직 (정보/수치 변경)";
        AddInput();

        var addBtn = new Button(() => { Data.logics.Add(new GraphLogicEntry()); RebuildLogicRows(); }) { text = "+ 동작 추가" };
        mainContainer.Add(addBtn);

        _logicContainer = new VisualElement();
        mainContainer.Add(_logicContainer);
        if (Data.logics.Count == 0) Data.logics.Add(new GraphLogicEntry());
        RebuildLogicRows();

        AddOutput("다음");
    }

    private VisualElement _logicContainer;

    private void RebuildLogicRows()
    {
        _logicContainer.Clear();
        for (int i = 0; i < Data.logics.Count; i++)
        {
            int idx = i;
            var entry = Data.logics[idx];
            var row = new VisualElement();
            row.AddToClassList("choice-row");

            var typeField = new EnumField(entry.type);
            typeField.RegisterValueChangedCallback(e =>
            {
                entry.type = (GraphLogicType)e.newValue;
                RebuildLogicRows();
            });
            row.Add(typeField);

            row.Add(BuildKeyField(entry.key, GraphKeySource.UsesFreeText(entry.type),
                GraphKeySource.GetKeysFor(entry.type), v => entry.key = v));

            if (GraphKeySource.UsesAmount(entry.type))
            {
                var amount = new IntegerField { value = entry.amount, style = { width = 50 } };
                amount.RegisterValueChangedCallback(e => entry.amount = e.newValue);
                row.Add(amount);
            }

            var del = new Button(() => { Data.logics.RemoveAt(idx); RebuildLogicRows(); }) { text = "×" };
            row.Add(del);

            _logicContainer.Add(row);
        }
        RefreshExpandedState();
    }

    /// <summary>드롭다운 또는 자유 입력 필드를 상황에 맞게 생성</summary>
    private VisualElement BuildKeyField(string current, bool freeText, List<string> options, System.Action<string> onChanged)
    {
        if (freeText)
        {
            var tf = new TextField { value = current, style = { width = 130 } };
            tf.RegisterValueChangedCallback(e => onChanged(e.newValue));
            return tf;
        }

        int index = Mathf.Max(0, options.IndexOf(current));
        var dd = new PopupField<string>(options, index) { style = { width = 130 } };
        onChanged(options[index]); // 초기값 동기화
        dd.RegisterValueChangedCallback(e => onChanged(e.newValue));
        return dd;
    }
}