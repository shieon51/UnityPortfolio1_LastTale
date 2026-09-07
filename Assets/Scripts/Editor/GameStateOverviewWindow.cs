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
        EditorGUILayout.EndScrollView();
        DrawDebugToolsSection(); //?
        DrawMemorySection(); //?
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

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField($"이해도: {data.CurrentUnderstandingCount} (상한 {data.maxObtainableUnderstanding}, {data.UnderstandingPercent:F0}%)");
            int newA = EditorGUILayout.IntField("호감도", data.hiddenAffection); // 호감도만 편집 가능하게 남김
            if (data.hiddenAffection != newA) 
            { 
                data.hiddenAffection = newA;
                NPCManager.Instance.SaveNPCData(data);
            }

            EditorGUILayout.LabelField($"모드: {data.currentMode}   관계 등급: {data.GetRelationshipTier()}");

            var live = Object.FindObjectsOfType<NPC>().FirstOrDefault(n => n.npcName == kvp.Key);
            if (live != null)
                EditorGUILayout.LabelField($"HP: {live.currentHealth}/{live.maxHealth}   MP: {live.currentMana}/{live.maxMana}");

            if (MemoryManager.Instance != null)
            {
                string foldKey = $"npc_memory_{kvp.Key}";
                if (!_categoryFoldouts.ContainsKey(foldKey)) _categoryFoldouts[foldKey] = false;

                var npcFragments = MemoryManager.Instance.GetAllRegistered().Where(d => d.category == kvp.Key).ToList();
                int haveCount = npcFragments.Count(d => MemoryManager.Instance.HasMemory(d.flagId));
                _categoryFoldouts[foldKey] = EditorGUILayout.Foldout(_categoryFoldouts[foldKey], $"획득 정보 ({haveCount}/{npcFragments.Count})", true);

                if (_categoryFoldouts[foldKey])
                {
                    EditorGUI.indentLevel++;
                    foreach (var frag in npcFragments.OrderBy(d => d.flagId))
                    {
                        bool has = MemoryManager.Instance.HasMemory(frag.flagId);
                        GUI.color = has ? Color.green : Color.gray;
                        EditorGUILayout.LabelField($"{(has ? "✔" : "✘")} {frag.displayName} ({frag.flagId})");
                        GUI.color = Color.white;
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();
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

    private void DrawMemorySection()
    {
        EditorGUILayout.LabelField("기억(정보) 현황", EditorStyles.boldLabel);
        if (MemoryManager.Instance == null) return;

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
                    GUI.color = has ? Color.green : Color.gray;
                    EditorGUILayout.LabelField($"{(has ? "✔" : "✘")} {data.displayName} ({data.flagId})");
                    GUI.color = Color.white;
                }
                EditorGUI.indentLevel--;
            }
        }
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