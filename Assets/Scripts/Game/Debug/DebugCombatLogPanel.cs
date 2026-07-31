#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// DebugCombatLogPanel.cs — 개발/에디터 빌드에서만 컴파일됨 (출시 빌드엔 자동 제외)
public class DebugCombatLogPanel : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI logText;
    public int maxLines = 8;
    private Queue<string> _lines = new();
    private int _totalDamage;

    private void Awake() => Show(); // 디버그 로그는 개발 중엔 항상 보이는 게 유용함

    public void Show() { if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = true; } }
    public void Hide() { if (canvasGroup != null) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; } }

    public void LogDamage(string source, int amount)
    {
        _totalDamage += amount;
        _lines.Enqueue($"{source}: {amount} dmg (총 {_totalDamage})");
        if (_lines.Count > maxLines) _lines.Dequeue();
        if (logText != null) logText.text = string.Join("\n", _lines);
    }

    public void ResetLog() { _lines.Clear(); _totalDamage = 0; if (logText != null) logText.text = ""; }
}
#endif