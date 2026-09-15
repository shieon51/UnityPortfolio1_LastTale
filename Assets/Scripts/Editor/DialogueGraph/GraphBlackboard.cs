// GraphBlackboard.cs (신규, Editor 폴더)
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class GraphBlackboard : VisualElement
{
    private readonly DialogueGraphWindow _owner;
    private DialogueGraphData _asset;
    private readonly ScrollView _body;
    private readonly Dictionary<string, bool> _folds = new();
    private string _search = "";

    public const float DefaultWidth = 280f;
    public float CurrentWidth { get; private set; } = DefaultWidth;
    public bool IsOpen => style.display == DisplayStyle.Flex;

    public System.Action OnLayoutChanged;   // 검증창이 따라오게 알림

    public GraphBlackboard(DialogueGraphWindow owner)
    {
        _owner = owner;
        style.position = Position.Absolute;
        style.right = 0;              // ★ 왼쪽 → 오른쪽
        style.top = 40;
        style.width = DefaultWidth;
        style.bottom = 0;
        style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.97f);
        style.borderLeftWidth = 1;    // ★ 테두리도 왼쪽으로
        style.borderLeftColor = new Color(0, 0, 0, 0.5f);
        style.display = DisplayStyle.None;

        // ── 좌측 가장자리 드래그로 폭 조절
        var resizeHandle = new VisualElement
        {
            style =
            {
                position = Position.Absolute,
                left = 0, top = 0, bottom = 0, width = 5,
                backgroundColor = new Color(1f, 1f, 1f, 0.06f),
            }
        };
        resizeHandle.RegisterCallback<MouseDownEvent>(e =>
        {
            resizeHandle.CaptureMouse();
            e.StopPropagation();
        });
        resizeHandle.RegisterCallback<MouseMoveEvent>(e =>
        {
            if (!resizeHandle.HasMouseCapture()) return;
            float newWidth = Mathf.Clamp(_owner.position.width - e.mousePosition.x, 200f, 600f);
            CurrentWidth = newWidth;
            style.width = newWidth;
            OnLayoutChanged?.Invoke();
            e.StopPropagation();
        });
        resizeHandle.RegisterCallback<MouseUpEvent>(e =>
        {
            if (resizeHandle.HasMouseCapture()) resizeHandle.ReleaseMouse();
            e.StopPropagation();
        });
        Add(resizeHandle);

        // ── 헤더
        var header = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 8, marginTop = 2 }
        };
        header.Add(new Label("변수 목록") { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } });
        header.Add(new Button(() => { GraphKeySource.InvalidateCache(); Refresh(_asset); }) { text = "↻", style = { width = 24 } });
        header.Add(new Button(Close) { text = "×", style = { width = 24 } });
        Add(header);

        // ── 검색창 (폭 넘침 방지)
        var searchField = new UnityEditor.UIElements.ToolbarSearchField();
        searchField.style.marginLeft = 8;
        searchField.style.marginRight = 8;
        searchField.style.width = StyleKeyword.Auto;   // ★ 고정폭 해제
        searchField.style.flexGrow = 0;
        searchField.style.flexShrink = 1;
        searchField.RegisterValueChangedCallback(e => { _search = e.newValue; Refresh(_asset); });
        Add(searchField);

        _body = new ScrollView { style = { flexGrow = 1, paddingLeft = 6, paddingRight = 6 } };
        Add(_body);

        schedule.Execute(() =>
        {
            if (IsOpen && _asset != null) Refresh(_asset);
        }).Every(500);   // 0.5초마다
    }

    public void Open(DialogueGraphData asset)
    {
        style.display = DisplayStyle.Flex;
        Refresh(asset);
        OnLayoutChanged?.Invoke();
    }

    public void Close()
    {
        style.display = DisplayStyle.None;
        OnLayoutChanged?.Invoke();
    }

    public void Toggle(DialogueGraphData asset)
    {
        if (IsOpen) Close();
        else Open(asset);
    }

    public void Refresh(DialogueGraphData asset)
    {
        float scroll = _body.scrollOffset.y;   // ★ 저장
        _asset = asset;
        _body.Clear();
        if (_asset == null) { _body.Add(new Label("그래프 애셋을 지정하세요.")); return; }

        var usage = CountUsage(_asset);

        DrawSection("기억 (Memory)", GraphVarType.Memory, GraphKeySource.GetMemoryFlagIds(), usage,
            new Color(0.45f, 0.8f, 1f), "LastMarchan/Narrative/Memory Fragment", "Assets/Resources/MemoryFragments");
        DrawSection("카운터 (Counter)", GraphVarType.Counter, GraphKeySource.GetCounterIds(), usage,
            new Color(0.6f, 0.9f, 0.6f), "LastMarchan/Narrative/Counter Definition", "Assets/Resources/Counters");
        DrawSection("NPC", GraphVarType.Affection, GraphKeySource.GetNPCNames(), usage,
            new Color(1f, 0.8f, 0.5f), "LastMarchan/NPC/NPC Definition", "Assets/Resources/NPCDefinitions");
        DrawSection("연출 큐 (Cue)", GraphVarType.MentalPercent, GraphKeySource.GetCueIds(), usage,
            new Color(0.9f, 0.6f, 1f), "LastMarchan/Narrative/Narrative Cue", "Assets/Resources/NarrativeCues");

        _body.schedule.Execute(() => _body.scrollOffset = new Vector2(0, scroll)).ExecuteLater(1); // ★ 복원
    }

    private void DrawSection(string title, GraphVarType type, List<string> keys,
        Dictionary<string, List<GraphNodeData>> usage, Color color, string menuPath, string targetFolder)
    {
        if (!_folds.ContainsKey(title)) _folds[title] = true;

        var fold = new Foldout { text = $"{title} ({keys.Count})", value = _folds[title] };
        fold.RegisterValueChangedCallback(e => _folds[title] = e.newValue);
        fold.style.marginTop = 4;

        foreach (var key in keys)
        {
            if (key.StartsWith("(")) continue;
            if (!string.IsNullOrEmpty(_search) && !key.ToLowerInvariant().Contains(_search.ToLowerInvariant())) continue;

            usage.TryGetValue(key, out var users);
            int count = users?.Count ?? 0;

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var dot = new VisualElement
            {
                style = { width = 8, height = 8, marginRight = 4, backgroundColor = color, borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4 }
            };
            row.Add(dot);

            var btn = new Button(() => _owner.HighlightNodesUsing(key)) { text = $"{key}  ({count})" };
            btn.style.flexGrow = 1;
            btn.style.unityTextAlign = TextAnchor.MiddleLeft;
            btn.style.whiteSpace = WhiteSpace.Normal;
            if (count == 0) btn.style.color = new Color(0.55f, 0.55f, 0.55f);
            btn.tooltip = count == 0 ? "아직 어디에서도 사용되지 않음" : $"{count}개 노드에서 사용 중 — 클릭하면 해당 노드로 이동";
            row.Add(btn);

            fold.Add(row);
        }

        fold.Add(new Button(() =>
        {
            EnsureFolder(targetFolder);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetFolder); // ★ 폴더 선택
            EditorApplication.ExecuteMenuItem($"Assets/Create/{menuPath}");
        })
        { text = $"+ 새로 만들기 ({targetFolder.Replace("Assets/", "")})" });

        _body.Add(fold);
    }

    /// <summary>폴더가 없으면 단계별로 생성</summary>
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{cur}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
        AssetDatabase.Refresh();
    }

    /// <summary>각 키가 어떤 노드에서 쓰이는지 집계</summary>
    public static Dictionary<string, List<GraphNodeData>> CountUsage(DialogueGraphData asset)
    {
        var map = new Dictionary<string, List<GraphNodeData>>();
        void Add(string key, GraphNodeData node)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<GraphNodeData>();
            if (!list.Contains(node)) list.Add(node);
        }

        foreach (var n in asset.nodes)
        {
            foreach (var line in n.lines)
            {
                Add(line.speakerKey, n);
                Add(line.cueId, n);
                foreach (var l in line.logics) Add(l.key, n);
            }
            foreach (var bc in n.branchCases)
                foreach (var c in bc.condition.entries) Add(c.key, n);
            foreach (var opt in n.choiceOptions)
                foreach (var c in opt.condition.entries) Add(c.key, n);
        }
        return map;
    }
}