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

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 모드에서만 실시간 상태를 볼 수 있습니다.", MessageType.Info);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawPlayerSection();
        EditorGUILayout.Space(10);
        DrawNPCSection();
        EditorGUILayout.Space(10);
        DrawMemoryTopicSection(); // 공용(NPC 무관) 정보 주제
        EditorGUILayout.Space(10);
        DrawMemorySection(); // 날짜/카테고리별 전체 뷰
        EditorGUILayout.EndScrollView();

        DrawDebugToolsSection();
        DrawTimeAnchorSection();

        Repaint(); // 실시간 갱신
    }

    private void DrawPlayerSection()
    {
        EditorGUILayout.LabelField("플레이어", EditorStyles.boldLabel);
        var c = PlayerManager.Instance?.CurrentCharacter;
        if (c == null) { EditorGUILayout.LabelField("(없음)"); return; }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"레벨: {c.level}   HP: {c.currentHealth}/{c.maxHealth}   MP: {c.currentMana}/{c.maxMana}");
        if (c is SoraStats sora)
        {
            EditorGUI.BeginChangeCheck();
            int newLoop = EditorGUILayout.IntField("회귀 횟수", sora.loopCount);
            if (EditorGUI.EndChangeCheck()) sora.loopCount = newLoop;
            EditorGUILayout.LabelField($"피로도: {sora.currentFatigue}/{sora.maxFatigue}   정신력: {sora.currentMental}/{sora.maxMental}   요정화: {sora.fairyStage}단계");
            EditorGUILayout.LabelField($"시간결정체: {sora.timeCrystals}개");
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
            if (data.hiddenAffection != newA) { data.hiddenAffection = Mathf.Clamp(newA, -50, 100); NPCManager.Instance.SaveNPCData(data); }

            EditorGUILayout.LabelField($"모드: {data.currentMode}   관계 등급: {data.GetRelationshipTier()}");

            var live = Object.FindObjectsOfType<NPC>().FirstOrDefault(n => n.npcName == kvp.Key);
            if (live != null) EditorGUILayout.LabelField($"HP: {live.currentHealth}/{live.maxHealth}   MP: {live.currentMana}/{live.maxMana}");

            DrawNPCMemoryHierarchy(kvp.Key);

            EditorGUILayout.EndVertical();
        }
    }

    // ★ 신규 — NPC별 "단계별 정보" / "단일 정보" 계층 구조 표시
    private void DrawNPCMemoryHierarchy(string npcName)
    {
        if (MemoryManager.Instance == null || LocalizationManager.Instance == null) return;

        string foldKey = $"npc_memory_{npcName}";
        if (!_categoryFoldouts.ContainsKey(foldKey)) _categoryFoldouts[foldKey] = false;

        var npcFragments = MemoryManager.Instance.GetAllRegistered().Where(d => d.category == npcName).ToList();
        var npcTopics = Resources.LoadAll<MemoryTopicData>("MemoryTopics").Where(t => t.category == npcName).ToList();
        var flagsInTopics = new HashSet<string>(npcTopics.SelectMany(t => t.stages.Select(s => s.requiredFlagId)));
        var standalone = npcFragments.Where(f => !flagsInTopics.Contains(f.flagId)).ToList();
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
                GUI.color = isFinal ? Color.white : (stage != null ? Color.gray : new Color(0.5f, 0.5f, 0.5f, 0.6f));
                EditorGUILayout.LabelField($"[{topic.topicId}] {label}");
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
                string label = has ? LocalizationManager.Instance.Get(frag.localizationKey) : "(아직 모름)";
                GUI.color = has ? Color.green : Color.gray;
                EditorGUILayout.LabelField($"{(has ? "✔" : "✘")} {label} ({frag.flagId})");
                GUI.color = Color.white;
            }
        }

        EditorGUI.indentLevel--;
    }

    private void DrawMemoryTopicSection()
    {
        EditorGUILayout.LabelField("공용 정보 주제 (NPC 무관)", EditorStyles.boldLabel);
        if (MemoryManager.Instance == null || LocalizationManager.Instance == null) return;

        var npcNames = new HashSet<string>(NPCManager.Instance?.AllNPCData.Keys ?? Enumerable.Empty<string>());
        foreach (var topic in Resources.LoadAll<MemoryTopicData>("MemoryTopics"))
        {
            if (npcNames.Contains(topic.category)) continue; // NPC 쪽에서 이미 보여줌
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
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"#{i + 1}  Scene {a.sceneID}  Day {a.day} {a.hour}시  Lv.{a.level}");
            if (GUILayout.Button("이동", GUILayout.Width(60))) TimeLoopManager.Instance.TravelToAnchor(a);
            if (GUILayout.Button("삭제", GUILayout.Width(60))) TimeLoopManager.Instance.RemoveAnchor(a);
            EditorGUILayout.EndHorizontal();
        }
    }


}