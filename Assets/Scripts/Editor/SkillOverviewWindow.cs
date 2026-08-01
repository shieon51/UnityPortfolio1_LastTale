using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class SkillOverviewWindow : EditorWindow
{
    private enum CharacterTab { Player, Liel } // 새 캐릭터 생기면 여기 추가
    private CharacterTab _characterTab = CharacterTab.Player;

    private enum SlotTab { All, Q, W, E, R }
    private SlotTab _slotTab = SlotTab.All;

    private string _searchText = "";
    private Vector2 _scroll;

    private List<SkillBase> _playerSkills;
    private List<NPCSkillBase> _npcSkills;
    private Dictionary<Object, bool> _foldoutState = new();
    private PoolManager _poolManager;

    private List<SkillSequenceData> _sequences;

    [MenuItem("LastMarchan/Skill Overview")]
    public static void Open() => GetWindow<SkillOverviewWindow>("스킬 전체 관리");

    private void OnEnable() => RefreshList();

    private void RefreshList()
    {
        _playerSkills = AssetDatabase.FindAssets("t:SkillBase")
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillBase>(AssetDatabase.GUIDToAssetPath(g)))
            .OrderBy(s => s.name).ToList();

        _npcSkills = AssetDatabase.FindAssets("t:NPCSkillBase")
            .Select(g => AssetDatabase.LoadAssetAtPath<NPCSkillBase>(AssetDatabase.GUIDToAssetPath(g)))
            .OrderBy(s => s.name).ToList();

        _sequences = AssetDatabase.FindAssets("t:SkillSequenceData")
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillSequenceData>(AssetDatabase.GUIDToAssetPath(g)))
            .OrderBy(s => s.name).ToList();

        _poolManager = FindObjectOfType<PoolManager>();
    }

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(6);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        if (_characterTab == CharacterTab.Player)
        {
            DrawSequenceSection();
            DrawSkillGroup(_playerSkills.Cast<Object>().ToList(), isPlayer: true);
        }
        else
            DrawSkillGroup(_npcSkills.Cast<Object>().ToList(), isPlayer: false);
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70))) RefreshList();
        GUILayout.FlexibleSpace();
        _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();

        _characterTab = (CharacterTab)GUILayout.Toolbar((int)_characterTab, new[] { "플레이어 (소라)", "리엘" });

        if (_characterTab == CharacterTab.Player)
            _slotTab = (SlotTab)GUILayout.Toolbar((int)_slotTab, new[] { "전체", "Q", "W", "E", "R" });
    }

    private void DrawSequenceSection()
    {
        EditorGUILayout.LabelField("콤보 시퀀스 (Q/W/E/R)", EditorStyles.boldLabel);
        foreach (var seq in _sequences)
        {
            if (seq == null) continue;
            if (!string.IsNullOrEmpty(_searchText) && !seq.name.ToLower().Contains(_searchText.ToLower())) continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(seq.name, EditorStyles.boldLabel);

            for (int i = 0; i < seq.comboSteps.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i + 1}타:", GUILayout.Width(40));
                EditorGUILayout.ObjectField(seq.comboSteps[i], typeof(SkillBase), false);
                if (GUILayout.Button("바로가기", GUILayout.Width(60))) Selection.activeObject = seq.comboSteps[i];
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space(10);
    }

    private void DrawSkillGroup(List<Object> skills, bool isPlayer)
    {
        foreach (var skill in skills)
        {
            if (skill == null) continue;
            if (!string.IsNullOrEmpty(_searchText) && !skill.name.ToLower().Contains(_searchText.ToLower())) continue;
            if (isPlayer && _slotTab != SlotTab.All && !MatchesSlot(skill.name, _slotTab)) continue;
            DrawSkillFoldout(skill);
        }
    }

    // 애셋 이름 규칙(Sora_Q1, Sora_W1...)으로 슬롯 필터링. 이름 규칙 바뀌면 여기만 고치면 됨.
    private bool MatchesSlot(string assetName, SlotTab tab)
    {
        string t = tab.ToString();
        return assetName.Contains("_" + t + "1") || assetName.Contains("_" + t + "2") || assetName.Contains("_" + t + "_");
    }

    private void DrawSkillFoldout(Object skill)
    {
        if (!_foldoutState.ContainsKey(skill)) _foldoutState[skill] = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _foldoutState[skill] = EditorGUILayout.InspectorTitlebar(_foldoutState[skill], skill);

        if (_foldoutState[skill])
        {
            var so = new SerializedObject(skill);
            so.Update();
            var prop = so.GetIterator();
            prop.NextVisible(true);
            while (prop.NextVisible(false))
                EditorGUILayout.PropertyField(prop, true);
            so.ApplyModifiedProperties();

            DrawVFXPreview(skill);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    private void DrawVFXPreview(Object skill)
    {
        List<SkillVFXCue> cues = skill switch { SkillBase sb => sb.vfxCues, NPCSkillBase nsb => nsb.vfxCues, _ => null };
        if (cues == null || cues.Count == 0 || _poolManager == null) return;

        EditorGUILayout.LabelField("VFX 미리보기", EditorStyles.boldLabel);
        foreach (var cue in cues)
        {
            EditorGUILayout.LabelField($"[{cue.cueId}]", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawVFXThumbnail("기본", cue.vfxKey);
            foreach (var co in cue.contextOverrides) DrawVFXThumbnail(co.context.ToString(), co.vfxKey);
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawVFXThumbnail(string label, string vfxKey)
    {
        var poolInfo = _poolManager.basePools.Find(p => p.poolName == vfxKey);
        EditorGUILayout.BeginVertical(GUILayout.Width(80));
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
        if (poolInfo != null && poolInfo.prefab != null)
        {
            var sr = poolInfo.prefab.GetComponentInChildren<SpriteRenderer>();
            Texture preview = sr != null && sr.sprite != null ? AssetPreview.GetAssetPreview(sr.sprite) : null;
            if (preview != null) GUILayout.Label(preview, GUILayout.Width(64), GUILayout.Height(64));
            else GUILayout.Box("로딩중", GUILayout.Width(64), GUILayout.Height(64));
        }
        else GUILayout.Box("(미등록)", GUILayout.Width(64), GUILayout.Height(64));
        EditorGUILayout.EndVertical();
    }
}