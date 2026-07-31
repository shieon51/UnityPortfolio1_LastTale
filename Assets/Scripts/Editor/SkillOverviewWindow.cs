using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// Assets/Scripts/Editor/SkillOverviewWindow.cs (신규, 반드시 Editor 폴더 안에)
public class SkillOverviewWindow : EditorWindow
{
    private Vector2 _scroll;
    private List<SkillBase> _playerSkills;
    private List<NPCSkillBase> _npcSkills;

    [MenuItem("LastMarchan/Skill Overview")]
    public static void Open() => GetWindow<SkillOverviewWindow>("스킬 전체 관리");

    private void OnEnable() => RefreshList();

    private void RefreshList()
    {
        _playerSkills = AssetDatabase.FindAssets("t:SkillBase")
            .Select(guid => AssetDatabase.LoadAssetAtPath<SkillBase>(AssetDatabase.GUIDToAssetPath(guid)))
            .OrderBy(s => s.name).ToList();

        _npcSkills = AssetDatabase.FindAssets("t:NPCSkillBase")
            .Select(guid => AssetDatabase.LoadAssetAtPath<NPCSkillBase>(AssetDatabase.GUIDToAssetPath(guid)))
            .OrderBy(s => s.name).ToList();
    }

    private void OnGUI()
    {
        if (GUILayout.Button("새로고침")) RefreshList();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("플레이어 스킬 (Sora)", EditorStyles.boldLabel);
        foreach (var skill in _playerSkills) DrawSkillRow(skill);

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("NPC 스킬", EditorStyles.boldLabel);
        foreach (var skill in _npcSkills) DrawSkillRow(skill);

        EditorGUILayout.EndScrollView();
    }

    private void DrawSkillRow(Object skill)
    {
        if (skill == null) return;
        var so = new SerializedObject(skill);
        so.Update();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(skill.name, EditorStyles.boldLabel);

        var prop = so.GetIterator();
        prop.NextVisible(true); // m_Script 필드는 건너뜀
        while (prop.NextVisible(false))
            EditorGUILayout.PropertyField(prop, true);

        so.ApplyModifiedProperties();
        EditorGUILayout.EndVertical();
    }
}