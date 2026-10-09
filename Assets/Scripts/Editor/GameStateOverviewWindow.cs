// Assets/Scripts/Editor/GameStateOverviewWindow.cs (신규)
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

public class GameStateOverviewWindow : EditorWindow
{
    [MenuItem("LastMarchan/Game State Overview")]
    public static void Open() => GetWindow<GameStateOverviewWindow>("전체 상태 관리");

    private Vector2 _scroll;
    private Dictionary<string, bool> _categoryFoldouts = new();

    // DrawMemorySection 관련
    private int _groupMode = 0;
    private readonly string[] _groupModeLabels = { "카테고리별", "날짜별" };

    private Dictionary<int, bool> _anchorFoldouts = new();

    private bool _showFullLog = false;
    private bool _currentFlowOnly = false;   // ★ 3-A — 전체 로그에서 이번 흐름만 보기
    private bool _showLoopHistory = false;   // ★ 3-A
    private Dictionary<int, bool> _loopFoldouts = new();   // ★ 3-A — 회차 목록 순번별

    private bool _hideFrequentRecords = true;
    // ★ 너무 잦아서 평소엔 숨기는 기록 종류
    private static readonly HashSet<RecordType> FrequentTypes = new()
    {
        RecordType.TimeAdvance, RecordType.SceneEnter, RecordType.ExpChange,
        RecordType.MentalChange, RecordType.FatigueChange, RecordType.VitalsCheckpoint,
    };

    // ★ 성능 — 예전에는 OnGUI 끝에서 Repaint()를 불러 플레이 중 매 에디터 틱마다 창 전체를 다시 그렸다.
    //   기록이 수백 건 쌓이면 이 창 하나가 프레임을 크게 깎았다 → 정해진 주기로만 다시 그린다
    [SerializeField] private float _repaintInterval = 0.25f;   // 다시 그리는 주기(초). 창 위쪽에서 바꾼다
    [SerializeField] private int _maxLogRows = 200;            // 전체 행적 로그에 그릴 최근 기록 수
    private double _nextRepaintTime;
    private NPC[] _liveNpcs = System.Array.Empty<NPC>();       // ★ 그리기 한 번에 한 번만 찾는다 (예전: NPC마다 씬 전체 검색)

    private void OnEnable() => EditorApplication.update += RepaintOnInterval;
    private void OnDisable() => EditorApplication.update -= RepaintOnInterval;

    private void RepaintOnInterval()
    {
        if (!Application.isPlaying || EditorApplication.timeSinceStartup < _nextRepaintTime) return;
        _nextRepaintTime = EditorApplication.timeSinceStartup + Mathf.Max(0.05f, _repaintInterval);
        Repaint();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 모드에서만 실시간 상태를 볼 수 있습니다.", MessageType.Info);
            return;
        }

        // ★ 갱신 설정 (하드코딩 대신 창에서 조절)
        EditorGUILayout.BeginHorizontal();
        _repaintInterval = EditorGUILayout.FloatField(new GUIContent("갱신 주기(초)", "이 창을 다시 그리는 주기. 짧을수록 게임이 느려진다"), _repaintInterval);
        _maxLogRows = Mathf.Max(10, EditorGUILayout.IntField(new GUIContent("로그 표시 수", "전체 행적 로그에 그릴 최근 기록 수. 많을수록 게임이 느려진다"), _maxLogRows));
        EditorGUILayout.EndHorizontal();

        if (Event.current.type == EventType.Layout)   // ★ Layout과 Repaint가 같은 목록을 쓰도록 Layout 때만 갱신
            _liveNpcs = FindObjectsByType<NPC>(FindObjectsSortMode.None);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawPlayerSection();
        EditorGUILayout.Space(10);
        DrawNPCSection();
        EditorGUILayout.Space(10);
        DrawMemoryTopicSection(); // 공용(NPC 무관) 정보 주제
        EditorGUILayout.Space(10);
        DrawMemorySection(); // 날짜/카테고리별 전체 뷰
        EditorGUILayout.Space(10);
        DrawFullActionLogSection();
        EditorGUILayout.Space(10);
        DrawDebugToolsSection();
        EditorGUILayout.Space(10);
        DrawTimeAnchorSection();
        EditorGUILayout.Space(10);
        DrawLoopHistorySection();   // ★ 3-A
        EditorGUILayout.EndScrollView();
        // ★ 여기서 매번 부르던 Repaint()는 없앴다 — RepaintOnInterval이 주기마다 다시 그린다
    }

    private void DrawPlayerSection()
    {
        EditorGUILayout.LabelField("플레이어", EditorStyles.boldLabel);
        var c = PlayerManager.Instance?.CurrentCharacter;
        if (c == null) { EditorGUILayout.LabelField("(없음)"); return; }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"레벨: {c.level}   HP: {c.currentHealth}/{c.maxHealth}   MP: {c.currentMana}/{c.maxMana}");
        // ★ 공·방·민 — 현재값(요정화 등 보정 포함)과 기본값(훈련으로 오른 값, 경로 2·3에서 되돌아가는 값)
        EditorGUILayout.LabelField($"공격: {c.attack.GetValue()} (기본 {c.attack.BaseValue})   " +
                                   $"방어: {c.defense.GetValue()} (기본 {c.defense.BaseValue})   " +
                                   $"민첩: {c.agility.GetValue()} (기본 {c.agility.BaseValue})");
        if (TimeManager.Instance != null)   // ★ 회귀 후 시각 확인용. 표시는 GameTimeFormatter를 거친다
            EditorGUILayout.LabelField($"현재 시각: {GameTimeFormatter.FormatDayTime(TimeManager.Instance.currentDay, TimeManager.Instance.currentHour)}");
        if (c is SoraStats sora)
        {
            EditorGUI.BeginChangeCheck();
            int newLoop = EditorGUILayout.IntField("회귀 횟수", sora.loopCount);
            if (EditorGUI.EndChangeCheck()) DebugLoopTools.SetLoopCount(newLoop);   // ★ 2-B — 직접 대입 대신 기록을 남기는 경로
            EditorGUILayout.LabelField($"피로도: {sora.currentFatigue}/{sora.maxFatigue}   정신력: {sora.currentMental}/{sora.maxMental}   요정화: {sora.fairyStage}단계");
            EditorGUILayout.LabelField($"시간결정체: {sora.timeCrystals}개");
            EditorGUILayout.LabelField($"영혼 레벨: {sora.highestLevelReached}   " +
                $"노련미 보정: ×{sora.CatchUpMultiplier:F2}{(sora.IsCatchingUp ? "" : " (없음)")}");
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawNPCSection()
    {
        EditorGUILayout.LabelField("NPC", EditorStyles.boldLabel);
        if (NPCManager.Instance == null) return;

        foreach (var kvp in NPCManager.Instance.AllNPCData)
        {
            var data = kvp.Value;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(kvp.Key, EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"이해도: {data.CurrentUnderstandingCount} (상한 {data.maxObtainableUnderstanding}, {data.UnderstandingPercent:F0}%)");
            int newA = EditorGUILayout.IntField("호감도", data.hiddenAffection);
            // ★ 직접 대입하지 않고 AddAffection을 거친다 — 범위는 NPCManager 설정을 따르고 기록에도 남는다
            if (data.hiddenAffection != newA) data.AddAffection(newA - data.hiddenAffection);

            EditorGUILayout.LabelField($"모드: {data.currentMode}   관계 등급: {data.GetRelationshipTier()}");

            var live = _liveNpcs.FirstOrDefault(n => n != null && n.npcName == kvp.Key);   // ★ 미리 찾아 둔 목록에서
            if (live != null) EditorGUILayout.LabelField($"HP: {live.currentHealth}/{live.maxHealth}   MP: {live.currentMana}/{live.maxMana}");

            DrawNPCMemoryHierarchy(kvp.Key);
            DrawNPCObservedActions(kvp.Key);

            EditorGUILayout.EndVertical();
        }
    }

    // ★ 신규 — NPC별 "단계별 정보" / "단일 정보" 계층 구조 표시
    private void DrawNPCMemoryHierarchy(string npcName)
    {
        if (MemoryManager.Instance == null || LocalizationManager.Instance == null) return;

        string foldKey = $"npc_memory_{npcName}";
        if (!_categoryFoldouts.ContainsKey(foldKey)) _categoryFoldouts[foldKey] = false;

        // 변경 후 — 매니저의 조회를 그대로 쓴다 (relatedNpcs 기준, 주제 단계 조각 제외까지 처리됨)
        var npcTopics = MemoryManager.Instance.GetAllTopics()
            .Where(t => (t.relatedNpcs != null && t.relatedNpcs.Contains(npcName)) || t.category == npcName).ToList();
        var standalone = MemoryManager.Instance.GetAllRegistered()
            .Where(f => !MemoryManager.Instance.IsUsedInTopic(f.flagId)
                     && ((f.relatedNpcs != null && f.relatedNpcs.Contains(npcName)) || f.category == npcName)).ToList();
        var acquiredOrder = MemoryManager.Instance.GetAllAcquired().ToList();

        int totalHave = standalone.Count(f => MemoryManager.Instance.HasMemory(f.flagId)) + npcTopics.Count(t => MemoryManager.Instance.GetCurrentStage(t) != null);
        int totalCount = standalone.Count + npcTopics.Count;

        _categoryFoldouts[foldKey] = EditorGUILayout.Foldout(_categoryFoldouts[foldKey], $"획득 정보 ({totalHave}/{totalCount})", true);
        if (!_categoryFoldouts[foldKey]) return;

        EditorGUI.indentLevel++;

        if (npcTopics.Count > 0)
        {
            EditorGUILayout.LabelField("— 단계별 정보 —", EditorStyles.miniBoldLabel);
            foreach (var topic in npcTopics)
            {
                var stage = MemoryManager.Instance.GetCurrentStage(topic);
                string label = stage != null ? LocalizationManager.Instance.Get(stage.localizationKey) : "(아직 모름)";
                bool isFinal = stage != null && stage.isFinal;

                // ★ 색은 isFinal이 아니라 확실도 기준 (기록장과 같은 규칙)
                GUI.color = stage == null ? new Color(0.5f, 0.5f, 0.5f, 0.6f)
                          : stage.certainty == MemoryCertainty.Confirmed ? Color.white : Color.gray;

                string extra = stage == null ? "" :
                    $"  [{topic.cardCategory}]{(stage.sourceType != MemorySourceType.None ? $" 출처 {stage.sourceType} {stage.sourceKey}" : "")}{(isFinal ? " (최종)" : "")}";
                EditorGUILayout.LabelField($"[{topic.topicId}] {label}{extra}");
                GUI.color = Color.white;
            }
        }

        if (standalone.Count > 0)
        {
            EditorGUILayout.LabelField("— 단일 정보 —", EditorStyles.miniBoldLabel);
            var ordered = standalone.OrderBy(f => acquiredOrder.Contains(f.flagId) ? acquiredOrder.IndexOf(f.flagId) : int.MaxValue).ThenBy(f => f.flagId);
            foreach (var frag in ordered)
            {
                bool has = MemoryManager.Instance.HasMemory(frag.flagId);
                bool refuted = has && MemoryManager.Instance.IsRefuted(frag.flagId);     // ★ 추가
                string label = has ? LocalizationManager.Instance.Get(frag.localizationKey) : "(아직 모름)";

                GUI.color = !has ? Color.gray
                          : refuted ? new Color(1f, 0.6f, 0.6f)
                          : frag.certainty == MemoryCertainty.Confirmed ? Color.green : new Color(0.7f, 0.9f, 0.7f);

                string extra = has ? $"  [{frag.cardCategory}]{(refuted ? " (반증됨)" : "")}" : "";
                EditorGUILayout.LabelField($"{(has ? "✔" : "✘")} {label} ({frag.flagId}){extra}");
                GUI.color = Color.white;
            }
        }

        EditorGUI.indentLevel--;
    }

    private void DrawNPCObservedActions(string npcName)
    {
        if (MemoryManager.Instance == null) return;
        string foldKey = $"npc_actions_{npcName}";
        if (!_categoryFoldouts.ContainsKey(foldKey)) _categoryFoldouts[foldKey] = false;

        var data = NPCManager.Instance.GetNPCData(npcName);
        var observable = data.observableCounterKeys ?? new string[0];

        _categoryFoldouts[foldKey] = EditorGUILayout.Foldout(_categoryFoldouts[foldKey],
            $"이 NPC가 기억하는 행적 ({observable.Length}개 구독)", true);
        if (!_categoryFoldouts[foldKey]) return;

        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField($"의심도: {SuspicionManager.Instance?.GetSuspicion(npcName) ?? 0} / 100");

        if (observable.Length == 0) EditorGUILayout.LabelField("(구독 중인 행적 없음)");
        foreach (var key in observable)
        {
            int count = MemoryManager.Instance.GetCounter(key);
            GUI.color = count > 0 ? Color.green : Color.gray;
            EditorGUILayout.LabelField($"{(count > 0 ? "✔" : "✘")} {key}: {count}회");
            GUI.color = Color.white;
        }

        // 시간순 행적 (이 NPC가 관찰 가능한 것만)
        if (PlayerActionLog.Instance != null)
        {
            EditorGUILayout.LabelField("— 시간순 기록 —", EditorStyles.miniBoldLabel);
            bool any = false;
            foreach (var r in PlayerActionLog.Instance.Records)
            {
                if (r.type != RecordType.Counter) continue;                      // ★ 카운터 기록만
                if (System.Array.IndexOf(observable, r.key) < 0) continue;       // ★ actionKey → key
                EditorGUILayout.LabelField($"[{r.loopCount}회차 Day{r.day} {r.hour}시] {r.key} @{PlayerActionLog.ResolvePlaceName(r)}");
                any = true;
            }
            if (!any) EditorGUILayout.LabelField("(기록 없음)");
        }
        EditorGUI.indentLevel--;
    }

    private void DrawMemoryTopicSection()
    {
        EditorGUILayout.LabelField("공용 정보 주제 (NPC 무관)", EditorStyles.boldLabel);
        if (MemoryManager.Instance == null || LocalizationManager.Instance == null) return;

        var npcNames = new HashSet<string>(NPCManager.Instance?.AllNPCData.Keys ?? Enumerable.Empty<string>());
        foreach (var topic in MemoryManager.Instance.GetAllTopics())
        {
            bool belongsToNpc = topic.category != null && npcNames.Contains(topic.category)
                || (topic.relatedNpcs != null && topic.relatedNpcs.Any(n => npcNames.Contains(n)));
            if (belongsToNpc) continue; // NPC 쪽에서 이미 보여줌
            var stage = MemoryManager.Instance.GetCurrentStage(topic);
            string label = stage != null ? LocalizationManager.Instance.Get(stage.localizationKey) : "(아직 모름)";
            bool isFinal = stage != null && stage.isFinal;
            GUI.color = isFinal ? Color.white : (stage != null ? Color.gray : new Color(0.5f, 0.5f, 0.5f, 0.6f));
            EditorGUILayout.LabelField($"[{topic.category}] {topic.topicId}: {label}");
            GUI.color = Color.white;
        }
    }

    private void DrawMemorySection()
    {
        EditorGUILayout.LabelField("전체 정보 현황 (날짜/카테고리별)", EditorStyles.boldLabel);
        if (MemoryManager.Instance == null || LocalizationManager.Instance == null) return;

        _groupMode = GUILayout.Toolbar(_groupMode, _groupModeLabels);
        var acquired = new HashSet<string>(MemoryManager.Instance.GetAllAcquired());
        var all = MemoryManager.Instance.GetAllRegistered();
        var grouped = _groupMode == 0
            ? all.GroupBy(d => string.IsNullOrEmpty(d.category) ? "(미분류)" : d.category)
            : all.GroupBy(d => d.day > 0 ? $"Day {d.day}" : "(날짜 무관)");

        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            if (!_categoryFoldouts.ContainsKey(group.Key)) _categoryFoldouts[group.Key] = false;
            int haveCount = group.Count(d => acquired.Contains(d.flagId));
            _categoryFoldouts[group.Key] = EditorGUILayout.Foldout(_categoryFoldouts[group.Key], $"{group.Key} ({haveCount}/{group.Count()})", true);

            if (_categoryFoldouts[group.Key])
            {
                EditorGUI.indentLevel++;
                foreach (var data in group.OrderBy(d => d.flagId))
                {
                    bool has = acquired.Contains(data.flagId);
                    string label = has ? LocalizationManager.Instance.Get(data.localizationKey) : "(아직 모름)"; // ★ displayName → localizationKey
                    GUI.color = has ? Color.green : Color.gray;
                    EditorGUILayout.LabelField($"{(has ? "✔" : "✘")} {label} ({data.flagId})");
                    GUI.color = Color.white;
                }
                EditorGUI.indentLevel--;
            }
        }
    }

    private void DrawDebugToolsSection()
    {
        EditorGUILayout.LabelField("디버그 도구", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (GUILayout.Button("다음 회차로 (기억 유지)"))
            DebugLoopTools.AdvanceToNextLoop();

        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("완전 리셋"))
        {
            if (EditorUtility.DisplayDialog("완전 리셋", "모든 진행 상황이 초기화됩니다. 계속할까요?", "네", "취소"))
                DebugLoopTools.FullReset();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndVertical();
    }

    private void DrawTimeAnchorSection()
    {
        EditorGUILayout.LabelField("시간 고정(앵커) 목록", EditorStyles.boldLabel);
        if (TimeLoopManager.Instance == null) return;

        var anchors = TimeLoopManager.Instance.Anchors;
        for (int i = 0; i < anchors.Count; i++)
        {
            var a = anchors[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (!_anchorFoldouts.ContainsKey(a.anchorId)) _anchorFoldouts[a.anchorId] = false;   // ★ 순번 대신 누적 번호
            // ★ 2-C-2 — 레벨은 닻의 손 나열 필드(level)가 아니라 범용 스냅샷의 몸 덩어리에서 읽는다
            string lv = TryReadInt(a.recordSnapshot, RecordIds.SoraBody, SoraStats.StateKeyLevel, out int level) ? $"Lv.{level}" : "Lv.?";
            _anchorFoldouts[a.anchorId] = EditorGUILayout.Foldout(_anchorFoldouts[a.anchorId],
                $"{a.anchorId}번 닻  Scene {a.sceneID}  {GameTimeFormatter.FormatDayTime(a.day, a.hour)}  {lv}  ({a.loopCountAtSave}회차에 설치)", true);
            if (GUILayout.Button("이동", GUILayout.Width(60))) TimeLoopManager.Instance.TravelToAnchor(a);
            if (GUILayout.Button("삭제", GUILayout.Width(60))) TimeLoopManager.Instance.RemoveAnchor(a);
            EditorGUILayout.EndHorizontal();

            if (_anchorFoldouts[a.anchorId])
            {
                EditorGUI.indentLevel++;
                DrawSnapshotBlocks(a.recordSnapshot);   // ★ 2-C-2 — 항목별 손 표시 대신 덩어리를 모두 펼친다
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }
    }

    // ★ 2-C-2 — 범용 스냅샷 표시. 등록된 시스템의 덩어리를 층위 순으로 모두 펼친다.
    //   예전에는 호감도·의심·카운터·기억을 항목마다 손으로 그려, 시스템이 늘면 이 창도 고쳐야 했다.
    //   이제 IRecordable을 등록하기만 하면 여기에 저절로 나온다
    private static readonly string[] LayerNames = { "세계", "몸", "의지", "플레이어" };   // RecordLayer 순서

    private void DrawSnapshotBlocks(Snapshot snapshot)
    {
        if (snapshot == null || snapshot.blocks.Count == 0) { EditorGUILayout.LabelField("(범용 스냅샷 없음)"); return; }

        foreach (var block in snapshot.blocks.Values.OrderBy(b => b.layer).ThenBy(b => b.recordId))
        {
            int li = (int)block.layer;
            string layerName = li >= 0 && li < LayerNames.Length ? LayerNames[li] : block.layer.ToString();
            bool restored = block.layer == RecordLayer.World || block.layer == RecordLayer.Body;
            EditorGUILayout.LabelField($"[{layerName}] {block.recordId}{(restored ? "" : "  (회귀해도 유지)")}", EditorStyles.miniBoldLabel);

            var parts = new List<string>();
            foreach (var kv in block.ints.OrderBy(k => k.Key)) parts.Add($"{kv.Key} {kv.Value}");
            foreach (var kv in block.floats.OrderBy(k => k.Key)) parts.Add($"{kv.Key} {kv.Value:F2}");
            if (parts.Count > 0) EditorGUILayout.LabelField(string.Join("  ·  ", parts), EditorStyles.wordWrappedLabel);

            foreach (var map in block.intMaps.OrderBy(k => k.Key))
            {
                string items = map.Value.Count == 0 ? "(없음)" : string.Join(", ", map.Value.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}"));
                EditorGUILayout.LabelField($"{map.Key}: {items}", EditorStyles.wordWrappedLabel);
            }

            foreach (var list in block.stringLists.OrderBy(k => k.Key))
            {
                string items = list.Value.Count == 0 ? "(없음)" : string.Join(", ", list.Value);
                EditorGUILayout.LabelField($"{list.Key} ({list.Value.Count}): {items}", EditorStyles.wordWrappedLabel);
            }
        }
    }

    private static bool TryReadInt(Snapshot snapshot, string recordId, string key, out int value)
    {
        value = 0;
        return snapshot != null && snapshot.blocks.TryGetValue(recordId, out var b) && b.ints.TryGetValue(key, out value);
    }

    // ★ 3-A — 회차·닻 이력 (LoopHistory). 이야기의 행적이 그릴 데이터가 맞게 쌓이는지 확인하는 칸
    private static readonly string[] EndTypeNames = { "진행 중", "사망", "불완전 결말", "자발적 회귀", "결말", "디버그 넘김" };   // LoopEndType 순서
    private static readonly string[] PathNames = { "자발적", "경로 1", "경로 2", "Day 1" };                                   // ReturnPath 순서
    private static readonly string[] AnchorStatusNames = { "사용 전", "사용", "강제 사용", "사라짐", "거둠" };                  // AnchorStatus 순서

    private static string NameOf(string[] names, int i) => i >= 0 && i < names.Length ? names[i] : i.ToString();

    private void DrawLoopHistorySection()
    {
        EditorGUILayout.LabelField("회차·닻 기록 (이야기의 행적 데이터)", EditorStyles.boldLabel);
        var loops = LoopHistory.Loops;
        var current = LoopHistory.Current;
        _showLoopHistory = EditorGUILayout.Foldout(_showLoopHistory,
            $"회차 {loops.Count}개, 닻 이력 {LoopHistory.Anchors.Count}개 (진행 중: {(current != null ? $"{current.loopNumber}회차" : "없음")})", true);
        if (!_showLoopHistory) return;

        var all = PlayerActionLog.Instance != null ? PlayerActionLog.Instance.Records : null;
        EditorGUI.indentLevel++;

        for (int i = loops.Count - 1; i >= 0; i--)   // 최근 회차부터
        {
            var l = loops[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string from = l.parentLoop < 0 ? "첫 회차"
                        : l.branchAnchorId > 0 ? $"{l.parentLoop}회차의 닻 #{l.branchAnchorId}에서 ({GameTimeFormatter.FormatDayTime(l.branchDay, l.branchHour)})"
                        : $"{l.parentLoop}회차 뒤 Day 1에서";
            string end = NameOf(EndTypeNames, (int)l.endType)
                       + (l.returnPath >= 0 ? $" → {NameOf(PathNames, l.returnPath)}{(l.returnAnchorId > 0 ? $" 닻 #{l.returnAnchorId}" : "")}" : "");

            if (!_loopFoldouts.ContainsKey(i)) _loopFoldouts[i] = false;
            GUI.color = l.IsOpen ? Color.yellow : Color.white;
            _loopFoldouts[i] = EditorGUILayout.Foldout(_loopFoldouts[i], $"{l.loopNumber}회차  |  {from}  |  {end}", true);
            GUI.color = Color.white;

            if (_loopFoldouts[i])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"자기 구간: #{l.startSeq} ~ {(l.IsOpen ? "진행 중" : $"#{l.endSeq}")}   물려받은 흐름: {(l.branchSeq > 0 ? $"#{l.branchSeq} 미만" : "없음")}");
                EditorGUILayout.LabelField($"스냅샷: 회차 시작 {(l.startSnapshot != null ? "✔" : "✘")}   결말 직전 {(l.endSnapshot != null ? "✔" : "✘")}");
                if (!string.IsNullOrEmpty(l.endingTitleKey) || !string.IsNullOrEmpty(l.deathCauseKey))
                    EditorGUILayout.LabelField($"결말 제목 {l.endingTitleKey ?? "-"}   사인 {l.deathCauseKey ?? "-"}");

                if (all != null)   // 펼쳤을 때만 계산한다 (부모를 따라 올라가므로 매번 하면 무겁다)
                {
                    var flow = LoopHistory.FlowRecords(l, all);
                    int own = flow.Count(r => r.seq >= l.startSeq && (l.IsOpen || r.seq <= l.endSeq));
                    int visible = flow.Count(r => PlayerActionLog.IsPlayerVisible(r.type));
                    EditorGUILayout.LabelField($"이 회차의 흐름: {flow.Count}건 (자기 구간 {own}건, 기록장에 보이는 것 {visible}건)");
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.LabelField("— 닻 이력 (누적 번호순) —", EditorStyles.miniBoldLabel);
        if (LoopHistory.Anchors.Count == 0) EditorGUILayout.LabelField("(없음)");
        foreach (var a in LoopHistory.Anchors)
        {
            GUI.color = a.status == AnchorStatus.Unused ? new Color(0.75f, 0.65f, 1f) : Color.white;
            EditorGUILayout.LabelField($"#{a.anchorId}  {a.loopSet}회차에 설치  {GameTimeFormatter.FormatDayTime(a.day, a.hour)}  Scene {a.sceneId}  " +
                                       $"[{NameOf(AnchorStatusNames, (int)a.status)}]{(a.usedByLoop >= 0 ? $" → {a.usedByLoop}회차" : "")}");
            GUI.color = Color.white;
        }

        EditorGUI.indentLevel--;
    }

    private void DrawFullActionLogSection()
    {
        EditorGUILayout.LabelField("전체 행적 로그 (시간순)", EditorStyles.boldLabel);
        if (PlayerActionLog.Instance == null) return;

        var allRecords = PlayerActionLog.Instance.Records;
        _showFullLog = EditorGUILayout.Foldout(_showFullLog, $"전체 기록 ({allRecords.Count}건, 다음 순번 {PlayerActionLog.Instance.NextSeq})", true);
        if (!_showFullLog) return;

        _hideFrequentRecords = EditorGUILayout.ToggleLeft("잦은 기록 숨기기 (시각·씬·경험치·정신력·피로도)", _hideFrequentRecords);
        // ★ 3-A — 행적을 되돌리지 않으므로 전체 기록에는 지난 회차까지 모두 있다. 기록장과 같은 범위만 볼 때 켠다
        _currentFlowOnly = EditorGUILayout.ToggleLeft("이번 흐름만 (기록장 이번 흐름과 같은 범위)", _currentFlowOnly);
        IReadOnlyList<ActionRecord> records = _currentFlowOnly ? PlayerActionLog.Instance.CurrentFlowRecords : allRecords;

        // ★ 성능 — 최근 _maxLogRows건만 그린다 (예전: 전체 기록을 매번 문자열로 조립)
        var shown = new List<ActionRecord>(Mathf.Min(_maxLogRows, records.Count));
        int skipped = 0;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            var rec = records[i];
            if (_hideFrequentRecords && FrequentTypes.Contains(rec.type)) continue;
            if (shown.Count < _maxLogRows) shown.Add(rec);
            else skipped++;
        }
        shown.Reverse();   // 시간순으로

        EditorGUI.indentLevel++;
        if (skipped > 0) EditorGUILayout.LabelField($"… 이전 기록 {skipped}건 생략 (로그 표시 수를 늘리면 보인다)", EditorStyles.miniLabel);
        foreach (var r in shown)
        {

            string detail = r.valueBefore == r.valueAfter ? r.key : $"{r.key}: {r.valueBefore} → {r.valueAfter}";
            string extra = (string.IsNullOrEmpty(r.source) ? "" : $" ← {r.source}")
                         + (string.IsNullOrEmpty(r.payload) ? "" : $" [{r.payload}]");

            GUI.color = r.type switch
            {
                RecordType.MemoryAcquired => Color.cyan,
                RecordType.SuspicionChange => new Color(1f, 0.6f, 0.6f),
                RecordType.AffectionChange => new Color(1f, 0.85f, 0.5f),
                RecordType.AnchorSet or RecordType.AnchorUsed
                    or RecordType.AnchorVanished or RecordType.AnchorRetracted => new Color(0.75f, 0.65f, 1f),   // ★
                RecordType.Loop => Color.yellow,                                                                // ★
                _ => Color.white,
            };
            EditorGUILayout.LabelField(
                $"#{r.seq} [{r.loopCount}회차 {GameTimeFormatter.FormatDayTime(r.day, r.hour)}] ({r.type}/{r.op}) {detail}{extra} @{PlayerActionLog.ResolvePlaceName(r)}");
            GUI.color = Color.white;
        }
        EditorGUI.indentLevel--;
    }
}