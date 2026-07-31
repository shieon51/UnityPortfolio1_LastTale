#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using TMPro;
using UnityEngine;

public class DebugGameStateOverlay : Singleton<DebugGameStateOverlay>
{
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI overlayText;
    public KeyCode toggleKey = KeyCode.F1;
    private bool _visible = false;

    private void Awake() => SetVisible(false);

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) SetVisible(!_visible);
        if (_visible) RefreshText();
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;
        if (canvasGroup != null) { canvasGroup.alpha = visible ? 1f : 0f; canvasGroup.blocksRaycasts = visible; }
    }

    private void RefreshText()
    {
        if (overlayText == null) return;
        var sb = new System.Text.StringBuilder();

        var player = PlayerManager.Instance?.CurrentCharacter;
        if (player != null)
        {
            sb.AppendLine($"[플레이어] Lv{player.level} HP {player.currentHealth}/{player.maxHealth} MP {player.currentMana}/{player.maxMana}");
            if (player is SoraStats sora)
                sb.AppendLine($"  피로도 {sora.currentFatigue}/{sora.maxFatigue} 정신력 {sora.currentMental}/{sora.maxMental} 요정화 {sora.fairyStage}단계");
        }

        if (NPCManager.Instance != null)
        {
            foreach (var kvp in NPCManager.Instance.AllNPCData)
            {
                sb.AppendLine($"[{kvp.Key}] 이해도 {kvp.Value.understanding} 호감도 {kvp.Value.hiddenAffection} 모드 {kvp.Value.currentMode}");
                var live = Object.FindObjectsOfType<NPC>().FirstOrDefault(n => n.npcName == kvp.Key);
                if (live != null) sb.AppendLine($"  HP {live.currentHealth}/{live.maxHealth} MP {live.currentMana}/{live.maxMana}");
            }
        }

        overlayText.text = sb.ToString();
    }
}
#endif