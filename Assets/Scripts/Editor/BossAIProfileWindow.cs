using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using static UnityEngine.Rendering.DebugUI.MessageBox;

// Assets/Scripts/Editor/BossAIProfileWindow.cs 
public class BossAIProfileWindow : EditorWindow
{
    [MenuItem("LastMarchan/Boss AI Profile Manager")]
    public static void Open() => GetWindow<BossAIProfileWindow>("보스 AI 프로필 관리");

    private List<NPCBossProfile> _profiles;
    private string _npcFilter = "";
    private Vector2 _scroll;

    private Dictionary<NPCBossProfile, Liel_AI.LielCombatStyle> _previewStyle = new();


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

        if (Application.isPlaying)
        {
            var liveLiel = Object.FindObjectsOfType<Liel_AI>().FirstOrDefault();
            if (liveLiel != null)
                EditorGUILayout.HelpBox($"현재 씬: {liveLiel.currentDifficultyTier} / {liveLiel.currentCombatStyle} / Phase {liveLiel.bossPhase}", MessageType.Info);
        }

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
        EditorGUILayout.PropertyField(so.FindProperty("phases"), true); // styleOverrides/agilityModifier 자동으로 같이 보임
        so.ApplyModifiedProperties();

        if (!_previewStyle.ContainsKey(profile)) _previewStyle[profile] = Liel_AI.LielCombatStyle.InjuredCommander;
        _previewStyle[profile] = (Liel_AI.LielCombatStyle)EditorGUILayout.EnumPopup("테스트할 스타일", _previewStyle[profile]);


        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("▶ 지금 이 프로필로 즉시 테스트 (Play 모드 전용)"))
                ApplyProfileToLiveNPC(profile, _previewStyle[profile]);
        }

        EditorGUILayout.EndVertical();
    }

    private void ApplyProfileToLiveNPC(NPCBossProfile profile, Liel_AI.LielCombatStyle style)
    {
        var target = Object.FindObjectsOfType<NPC>()
            .FirstOrDefault(n => n.npcName == profile.npcName) as IBossProfileTarget; // ★ NPC로 이름 찾고, 인터페이스로 다룸

        if (target == null) { Debug.LogWarning($"씬에서 {profile.npcName}에 해당하는 IBossProfileTarget을 찾을 수 없습니다."); return; }

        target.CurrentDifficultyTier = profile.difficultyTier;

        if (target is Liel_AI liel) liel.currentCombatStyle = style; // ★ 스타일 축은 아직 리엘 전용이라 여기서만 분기 (나중에 두 번째 보스 생기면 여기만 확장)

        target.ApplyResolvedProfile(profile, isBattleStart: true);

        Debug.Log($"[BossAIProfileWindow] {profile.npcName}에 '{profile.difficultyTier}' 프로필(페이즈 {target.BossPhase})을 즉시 적용했습니다.");
    }
}