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
        style.minWidth = 300;   // ★ 추가 — Knot 이름만 있으니 작게
        style.maxWidth = 300;   // ★ 추가
        title = "시작 (Knot)";
        AddMetaFold();
        var field = new TextField("Knot 이름") { value = Data.knotName };
        field.RegisterValueChangedCallback(e => Data.knotName = e.newValue);
        mainContainer.Add(field);
        AddOutput("다음");
    }

    public void BuildLine()
    {
        AddToClassList("dialogue-node");
        AddToClassList("node-line");
        style.minWidth = 520;   // ★ 추가
        style.maxWidth = 520;   // ★ 추가

        title = "대사";
        AddInput();
        AddMetaFold();

        mainContainer.Add(new Button(() => { Data.lines.Add(new DialogueLine()); RebuildLines(); }) { text = "+ 대사 추가" });

        _lineContainer = new VisualElement();
        mainContainer.Add(_lineContainer);

        if (Data.lines.Count == 0) Data.lines.Add(new DialogueLine());
        RebuildLines();

        AddBattleFold();          // ★ 추가
        AddOutput("다음");
    }

    /// <summary>이 노드에서 전투를 시작할지 (비워두면 전투 없음)</summary>
    private void AddBattleFold()
    {
        var fold = new Foldout { text = BuildBattleSummary(), value = false };
        fold.AddToClassList("compact-fold");
        EnableFoldTextWrap(fold);

        var npcOptions = GraphKeySource.GetNPCNames();
        npcOptions.Insert(0, "(전투 없음)");
        int ni = Mathf.Max(0, npcOptions.IndexOf(string.IsNullOrEmpty(Data.battleNpc) ? "(전투 없음)" : Data.battleNpc));
        var npcDd = new PopupField<string>("상대", npcOptions, ni);
        npcDd.RegisterValueChangedCallback(e =>
        {
            Data.battleNpc = e.newValue == "(전투 없음)" ? "" : e.newValue;
            fold.text = BuildBattleSummary();
        });
        fold.Add(npcDd);

        var tiers = new List<string> { "Training", "Normal", "Hard" };
        int ti = Mathf.Max(0, tiers.IndexOf(string.IsNullOrEmpty(Data.battleDifficulty) ? "Training" : Data.battleDifficulty));
        var tierDd = new PopupField<string>("난이도", tiers, ti);
        tierDd.RegisterValueChangedCallback(e => { Data.battleDifficulty = e.newValue; fold.text = BuildBattleSummary(); });
        fold.Add(tierDd);

        var knots = GraphKeySource.GetKnotNames();
        knots.Insert(0, "(기본값)");

        int wi = Mathf.Max(0, knots.IndexOf(string.IsNullOrEmpty(Data.battleWinKnot) ? "(기본값)" : Data.battleWinKnot));
        var winDd = new PopupField<string>("승리 후", knots, wi);
        winDd.RegisterValueChangedCallback(e => Data.battleWinKnot = e.newValue == "(기본값)" ? "" : e.newValue);
        fold.Add(winDd);

        int li = Mathf.Max(0, knots.IndexOf(string.IsNullOrEmpty(Data.battleLoseKnot) ? "(기본값)" : Data.battleLoseKnot));
        var loseDd = new PopupField<string>("패배 후", knots, li);
        loseDd.RegisterValueChangedCallback(e => Data.battleLoseKnot = e.newValue == "(기본값)" ? "" : e.newValue);
        fold.Add(loseDd);

        // 전투 중 보스 상태로 표시할 문구 키 (봐주는 중 / 전력 / 폭주 등)
        var stateField = new TextField("보스 상태 키") { value = Data.battleStateKey };
        stateField.RegisterValueChangedCallback(e => { Data.battleStateKey = e.newValue; fold.text = BuildBattleSummary(); });
        fold.Add(stateField);

        mainContainer.Add(fold);
    }

    private string BuildBattleSummary()
    {
        if (string.IsNullOrEmpty(Data.battleNpc)) return "전투 (없음)";
        string state = string.IsNullOrEmpty(Data.battleStateKey) ? "" : $", {Data.battleStateKey}";
        return $"전투 — {Data.battleNpc} ({Data.battleDifficulty}{state})";
    }

    private void RebuildLines()
    {
        _lineContainer.Clear();

        for (int i = 0; i < Data.lines.Count; i++)
        {
            int idx = i;
            var line = Data.lines[idx];
            foreach (var l in line.logics) ConditionUtil.NormalizeKey(l);

            var box = new VisualElement();
            box.AddToClassList("line-box");

            // ── 상단: 순번 + 화자 + 이름 + 이동/삭제
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

            var nameField = new TextField { value = line.speakerName };
            nameField.style.width = 110;        // ★ flexGrow 대신 고정 폭
            nameField.style.flexShrink = 0;
            nameField.tooltip = "화면에 표시될 이름 (??? 등)";
            nameField.RegisterValueChangedCallback(e => line.speakerName = e.newValue);
            head.Add(nameField);
            head.Add(new VisualElement { style = { flexGrow = 1 } }); // ★ 남는 공간을 먹는 빈 여백 — 버튼이 오른쪽 끝으로 밀림

            head.Add(new Button(() => MoveLine(idx, -1)) { text = "▲", style = { width = 20 } });
            head.Add(new Button(() => MoveLine(idx, 1)) { text = "▼", style = { width = 20 } });
            head.Add(new Button(() => { Data.lines.RemoveAt(idx); RebuildLines(); }) { text = "×", style = { width = 20 } });
            box.Add(head);

            // ── 본문 2단 (왼쪽: 대사 / 오른쪽: 설정)
            var body = new VisualElement();
            body.AddToClassList("line-body");
            body.style.flexDirection = FlexDirection.Row;        // ★ 인라인
            body.style.alignItems = Align.FlexStart;             // ★ 인라인

            // 왼쪽 — 대사
            var leftCol = new VisualElement();
            leftCol.AddToClassList("line-left");
            leftCol.style.flexGrow = 1;                          // ★ 인라인
            leftCol.style.flexShrink = 1;
            leftCol.style.minWidth = 200;
            leftCol.style.paddingRight = 4;

            var text = new TextField { value = line.text, multiline = true };
            text.AddToClassList("line-text");
            text.style.minHeight = 52;                           // ★ 인라인
            text.style.whiteSpace = WhiteSpace.Normal;
            text.RegisterValueChangedCallback(e => line.text = e.newValue);
            leftCol.Add(text);
            body.Add(leftCol);

            // 오른쪽 — 표시 설정 + 결과 동작
            var rightCol = new VisualElement();
            rightCol.AddToClassList("line-right");
            rightCol.style.width = 210;                          // ★ 인라인
            rightCol.style.flexShrink = 0;

            var settingsFold = new Foldout { text = BuildLineSettingSummary(line), value = false };
            settingsFold.AddToClassList("compact-fold");
            EnableFoldTextWrap(settingsFold);     // ★ 추가

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

            // ★ 이 대화에서 소모할 시간 (거절 분기는 0, -1이면 이벤트 기본값)
            var timeField = new IntegerField("소모 시간(-1=기본)") { value = line.timeTakenOverride };
            timeField.RegisterValueChangedCallback(e =>
            {
                line.timeTakenOverride = Mathf.Max(-1, e.newValue);
                settingsFold.text = BuildLineSettingSummary(line);
            });
            settingsFold.Add(timeField);

            rightCol.Add(settingsFold);

            var logicFold = new Foldout { text = BuildLogicSummary(line), value = false };
            logicFold.AddToClassList("compact-fold");
            EnableFoldTextWrap(logicFold);        // ★ 추가

            logicFold.Add(new Button(() => { line.logics.Add(new GraphLogicEntry()); RebuildLines(); }) { text = "+ 동작 추가" });
            foreach (var row in BuildLogicRows(line, logicFold)) logicFold.Add(row);
            rightCol.Add(logicFold);

            body.Add(rightCol);
            box.Add(body);          // ★ 이게 빠져있었음

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
        if (l.timeTakenOverride >= 0) parts.Add($"시간 {l.timeTakenOverride}");   // ★ 추가
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
            varField.RegisterValueChangedCallback(e =>
            {
                l.varType = (GraphVarType)e.newValue;
                l.key = ""; // ★ 추가 — 타입 바뀌면 키 초기화
                RebuildLines();
            });
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
                if (!string.IsNullOrEmpty(l.key) && !options.Contains(l.key)) options.Insert(0, l.key); // ★ 미등록 키 보존
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
        style.minWidth = 400;   // ★ 추가
        style.maxWidth = 400;   // ★ 추가
        title = "조건 분기";
        AddInput();
        AddMetaFold();

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
            fold.AddToClassList("compact-fold");
            fold.text = $"분기 {idx + 1}: {ConditionUtil.BuildSummary(bc.condition)}";
            EnableFoldTextWrap(fold);           // ★ 개행 + 여백 (1번에서 고친 버전)
            fold.Add(new ConditionGroupElement(bc.condition,
                () => fold.text = $"분기 {idx + 1}: {ConditionUtil.BuildSummary(bc.condition)}"));
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
        style.minWidth = 440;   // ★ 추가
        style.maxWidth = 440;   // ★ 추가
        title = "선택지";
        AddInput();
        AddMetaFold();

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

            var body = new VisualElement();
            body.AddToClassList("line-body");
            body.style.flexDirection = FlexDirection.Row;        // ★
            body.style.alignItems = Align.FlexStart;             // ★

            var left = new VisualElement();
            left.AddToClassList("line-left");
            left.style.flexGrow = 1;                             // ★
            left.style.minWidth = 180;
            left.style.paddingRight = 4;

            var tf = new TextField { value = opt.text, multiline = true };
            tf.AddToClassList("line-text");
            tf.style.minHeight = 44;                             // ★
            tf.style.whiteSpace = WhiteSpace.Normal;
            tf.RegisterValueChangedCallback(e => opt.text = e.newValue);
            left.Add(tf);
            body.Add(left);

            var right = new VisualElement();
            right.AddToClassList("line-right");
            right.style.width = 250;                             // ★ // 조정
            right.style.flexShrink = 0;

            var fold = new Foldout { text = $"표시 조건: {ConditionUtil.BuildSummary(opt.condition)}", value = false };
            fold.AddToClassList("compact-fold");
            EnableFoldTextWrap(fold);             // ★ 제목 줄바꿈 허용
            fold.Add(new ConditionGroupElement(opt.condition,
                () => fold.text = $"표시 조건: {ConditionUtil.BuildSummary(opt.condition)}"));
            right.Add(fold);
            right.Add(new Button(() => { Data.choiceOptions.RemoveAt(idx); RebuildChoiceRows(); }) { text = "× 삭제" });
            body.Add(right);

            box.Add(body);
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

    /// <summary>Foldout 제목이 길어도 잘리지 않고 줄바꿈되게 함</summary>
    private static void EnableFoldTextWrap(Foldout fold)
    {
        var label = fold.Q<Toggle>()?.Q<Label>();
        if (label == null) return;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.flexGrow = 1;
        label.style.flexShrink = 1;
        label.style.paddingRight = 10;      // ★ 오른쪽 여백 — 마지막 글자 잘림 방지
        label.style.overflow = Overflow.Visible;  // ★ 넘쳐도 숨기지 않음
    }

    /// <summary>모든 노드 공통 분류 정보 (필터링용)</summary>
    private void AddMetaFold()
    {
        var fold = new Foldout { text = BuildMetaSummary(), value = false };
        fold.AddToClassList("compact-fold");
        EnableFoldTextWrap(fold);

        var dayField = new IntegerField("Day (0=무관)") { value = Data.day };
        dayField.RegisterValueChangedCallback(e => { Data.day = e.newValue; fold.text = BuildMetaSummary(); });
        fold.Add(dayField);

        var npcOptions = GraphKeySource.GetNPCNames();
        npcOptions.Insert(0, "(무관)");
        int ni = Mathf.Max(0, npcOptions.IndexOf(string.IsNullOrEmpty(Data.npcTag) ? "(무관)" : Data.npcTag));
        var npcDd = new PopupField<string>("관련 NPC", npcOptions, ni);
        npcDd.RegisterValueChangedCallback(e =>
        {
            Data.npcTag = e.newValue == "(무관)" ? "" : e.newValue;
            fold.text = BuildMetaSummary();
        });
        fold.Add(npcDd);

        var startField = new IntegerField("시작 시각 (-1=무관)") { value = Data.startHour };
        startField.RegisterValueChangedCallback(e => { Data.startHour = e.newValue; fold.text = BuildMetaSummary(); });
        fold.Add(startField);

        var endField = new IntegerField("종료 시각 (-1=무관)") { value = Data.endHour };
        endField.RegisterValueChangedCallback(e => { Data.endHour = e.newValue; fold.text = BuildMetaSummary(); });
        fold.Add(endField);

        var colorOptions = new List<string> { "(없음)", "빨강", "주황", "노랑", "초록", "파랑", "보라" };
        int ci = Mathf.Max(0, colorOptions.IndexOf(string.IsNullOrEmpty(Data.colorTag) ? "(없음)" : Data.colorTag));
        var colorDd = new PopupField<string>("색상 태그", colorOptions, ci);
        colorDd.RegisterValueChangedCallback(e =>
        {
            Data.colorTag = e.newValue == "(없음)" ? "" : e.newValue;
            ApplyColorTag();
            fold.text = BuildMetaSummary();
        });
        fold.Add(colorDd);

        var noteField = new TextField("메모") { value = Data.note, multiline = true };
        noteField.style.whiteSpace = WhiteSpace.Normal;
        noteField.RegisterValueChangedCallback(e => Data.note = e.newValue);
        fold.Add(noteField);

        mainContainer.Add(fold);
        ApplyColorTag();
    }

    private string BuildMetaSummary()
    {
        var parts = new List<string>();
        if (Data.day > 0) parts.Add($"Day {Data.day}");
        if (!string.IsNullOrEmpty(Data.npcTag)) parts.Add(Data.npcTag);
        if (Data.startHour >= 0 || Data.endHour >= 0) parts.Add($"{Data.startHour}~{Data.endHour}시");
        if (!string.IsNullOrEmpty(Data.colorTag)) parts.Add($"[{Data.colorTag}]");
        return parts.Count == 0 ? "분류 (미설정)" : "분류 — " + string.Join(", ", parts);
    }

    /// <summary>색상 태그를 노드 테두리에 반영</summary>
    private void ApplyColorTag()
    {
        var titleBar = this.Q("title");      // 제목 영역은 이름이 안정적으로 "title"
        if (titleBar == null) return;

        if (string.IsNullOrEmpty(Data.colorTag))
        {
            titleBar.style.backgroundColor = StyleKeyword.Null;
            return;
        }

        Color c = Data.colorTag switch
        {
            "빨강" => new Color(0.55f, 0.18f, 0.18f),
            "주황" => new Color(0.58f, 0.35f, 0.12f),
            "노랑" => new Color(0.55f, 0.5f, 0.15f),
            "초록" => new Color(0.2f, 0.48f, 0.25f),
            "파랑" => new Color(0.18f, 0.35f, 0.55f),
            "보라" => new Color(0.42f, 0.25f, 0.55f),
            _ => Color.gray,
        };
        titleBar.style.backgroundColor = c;
    }

    public void SetFocused(bool focused)
    {
        style.opacity = focused ? 1f : 0.22f;
        pickingMode = focused ? PickingMode.Position : PickingMode.Ignore; // 흐린 노드는 클릭 안 되게
    }

    public void SetDirtyMark(bool dirty)
    {
        if (dirty && !title.EndsWith(" *")) title += " *";
        else if (!dirty && title.EndsWith(" *")) title = title.Substring(0, title.Length - 2);
    }
}