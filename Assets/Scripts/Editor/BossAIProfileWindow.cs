using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// Assets/Scripts/Editor/BossAIProfileWindow.cs 
public class BossAIProfileWindow : EditorWindow
{
    [MenuItem("LastMarchan/Boss AI Profile Manager")]
    public static void Open() => GetWindow<BossAIProfileWindow>("보스 AI 프로필 관리");

    private List<NPCBossProfile> _profiles;
    private string _npcFilter = "";
    private Vector2 _scroll;

    private void OnEnable() => Refresh();

    private void Refresh()
    {
        _profiles = AssetDatabase.FindAssets("t:NPCBossProfile")
            .Select(g => AssetDatabase.LoadAssetAtPath<NPCBossProfile>(AssetDatabase.GUIDToAssetPath(g)))
            .OrderBy(p => p.npcName).ThenBy(p => p.difficultyTier).ToList();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70))) Refresh();
        GUILayout.FlexibleSpace();
        _npcFilter = EditorGUILayout.TextField(_npcFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (var npcGroup in _profiles.Where(p => string.IsNullOrEmpty(_npcFilter) || p.npcName.ToLower().Contains(_npcFilter.ToLower()))
                                           .GroupBy(p => p.npcName))
        {
            EditorGUILayout.LabelField(npcGroup.Key, EditorStyles.boldLabel);
            foreach (var profile in npcGroup) DrawProfile(profile);
            EditorGUILayout.Space(10);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawProfile(NPCBossProfile profile)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"{profile.difficultyTier}  ({profile.name})", EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(profile.storyBranchNote))
            EditorGUILayout.LabelField("메모: " + profile.storyBranchNote, EditorStyles.miniLabel);

        var so = new SerializedObject(profile);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("phases"), true);
        so.ApplyModifiedProperties();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("▶ 지금 이 프로필로 즉시 테스트 (Play 모드 전용)"))
                ApplyProfileToLiveNPC(profile);
        }

        EditorGUILayout.EndVertical();
    }

    private void ApplyProfileToLiveNPC(NPCBossProfile profile)
    {
        var liveNpc = Object.FindObjectsOfType<NPC>().FirstOrDefault(n => n.npcName == profile.npcName);
        if (liveNpc == null) { Debug.LogWarning($"씬에서 {profile.npcName}을 찾을 수 없습니다."); return; }

        var ai = liveNpc.GetComponent<NPCUtilityAI>();
        if (ai == null) { Debug.LogWarning($"{profile.npcName}에 NPCUtilityAI가 없습니다."); return; }

        int currentPhase = (liveNpc as Liel_AI)?.bossPhase ?? 1;
        ai.ApplyProfile(profile, currentPhase);
        Debug.Log($"[BossAIProfileWindow] {profile.npcName}에 '{profile.difficultyTier}' 프로필(페이즈 {currentPhase})을 즉시 적용했습니다.");
    }
}