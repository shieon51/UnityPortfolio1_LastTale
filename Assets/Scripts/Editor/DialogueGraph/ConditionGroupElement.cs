// ConditionGroupElement.cs (신규, Editor 폴더)
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine;

public class ConditionGroupElement : VisualElement
{
    private readonly ConditionGroup _group;
    private readonly VisualElement _rows;
    private readonly Label _summary;
    private readonly System.Action _onChanged;

    public ConditionGroupElement(ConditionGroup group, System.Action onChanged = null)
    {
        _group = group;
        _onChanged = onChanged;
        AddToClassList("condition-group");

        _summary = new Label(ConditionUtil.BuildSummary(_group));
        _summary.AddToClassList("condition-summary");
        Add(_summary);

        var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        var joinField = new EnumField(_group.join) { style = { width = 70 } };
        joinField.RegisterValueChangedCallback(e => { _group.join = (CondJoin)e.newValue; Refresh(); });
        header.Add(joinField);
        header.Add(new Button(() => { _group.entries.Add(new ConditionEntry()); Refresh(); }) { text = "+ 조건" });
        Add(header);

        _rows = new VisualElement();
        Add(_rows);
        Refresh();
    }

    private void Refresh()
    {
        _rows.Clear();
        for (int i = 0; i < _group.entries.Count; i++)
        {
            int idx = i;
            var e = _group.entries[idx];
            var row = new VisualElement();
            row.AddToClassList("condition-row");

            var varField = new EnumField(e.varType) { style = { width = 95 } };
            varField.RegisterValueChangedCallback(ev =>
            {
                e.varType = (GraphVarType)ev.newValue;
                e.op = ConditionUtil.IsBoolType(e.varType) ? CondOp.Has : CondOp.GreaterOrEqual;
                Refresh();
            });
            row.Add(varField);

            if (ConditionUtil.NeedsKey(e.varType))
                row.Add(BuildKeyField(e));

            var ops = ConditionUtil.IsBoolType(e.varType)
                ? new List<CondOp> { CondOp.Has, CondOp.NotHas }
                : new List<CondOp> { CondOp.GreaterOrEqual, CondOp.LessOrEqual, CondOp.Equal, CondOp.NotEqual };

            var opField = new PopupField<CondOp>(ops, ops.Contains(e.op) ? ops.IndexOf(e.op) : 0,
                v => OpLabel(v), v => OpLabel(v))
            { style = { width = 70 } };
            opField.RegisterValueChangedCallback(ev => { e.op = ev.newValue; Refresh(); });
            row.Add(opField);

            if (!ConditionUtil.IsBoolType(e.varType))
            {
                var val = new IntegerField { value = e.value, style = { width = 45 } };
                val.RegisterValueChangedCallback(ev => { e.value = ev.newValue; Refresh(); });
                row.Add(val);
            }

            row.Add(new Button(() => { _group.entries.RemoveAt(idx); Refresh(); }) { text = "×", style = { width = 20 } });
            _rows.Add(row);
        }

        _summary.text = ConditionUtil.BuildSummary(_group);
        _onChanged?.Invoke();
    }

    private VisualElement BuildKeyField(ConditionEntry e)
    {
        if (ConditionUtil.UsesFreeText(e.varType))
        {
            var tf = new TextField { value = e.key, style = { width = 120 } };
            tf.RegisterValueChangedCallback(ev => { e.key = ev.newValue; _summary.text = ConditionUtil.BuildSummary(_group); });
            return tf;
        }

        var options = ConditionUtil.GetKeyOptions(e.varType);
        if (options.Count == 0) options.Add("(없음)");
        int index = Mathf.Max(0, options.IndexOf(e.key));
        e.key = options[index];

        var dd = new PopupField<string>(options, index) { style = { width = 120 } };
        dd.RegisterValueChangedCallback(ev => { e.key = ev.newValue; Refresh(); });
        return dd;
    }

    private static string OpLabel(CondOp op) => op switch
    {
        CondOp.Has => "있음",
        CondOp.NotHas => "없음",
        CondOp.GreaterOrEqual => "이상",
        CondOp.LessOrEqual => "이하",
        CondOp.Equal => "같음",
        CondOp.NotEqual => "다름",
        _ => "?",
    };
}