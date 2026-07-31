// Assets/Scripts/Editor/GameStateOverviewWindow.cs (신규)
using UnityEngine;
using UnityEditor;
using System.Linq;

public class GameStateOverviewWindow : EditorWindow
{
    [MenuItem("LastMarchan/Game State Overview")]
    public static void Open() => GetWindow<GameStateOverviewWindow>("전체 상태 관리");

    private Vector2 _scroll;

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
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(kvp.Key, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"이해도: {kvp.Value.understanding}   호감도: {kvp.Value.hiddenAffection}   모드: {kvp.Value.currentMode}");

            var live = Object.FindObjectsOfType<NPC>().FirstOrDefault(n => n.npcName == kvp.Key);
            if (live != null)
                EditorGUILayout.LabelField($"HP: {live.currentHealth}/{live.maxHealth}   MP: {live.currentMana}/{live.maxMana}");

            EditorGUILayout.EndVertical();
        }
    }
}