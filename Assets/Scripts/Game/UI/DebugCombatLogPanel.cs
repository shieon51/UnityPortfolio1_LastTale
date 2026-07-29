#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// DebugCombatLogPanel.cs — 개발/에디터 빌드에서만 컴파일됨 (출시 빌드엔 자동 제외)
public class DebugCombatLogPanel : MonoBehaviour
{
    public TextMeshProUGUI logText;
    public int maxLines = 8;
    private Queue<string> _lines = new();
    private int _totalDamage;

    public void LogDamage(string source, int amount)
    {
        _totalDamage += amount;
        _lines.Enqueue($"{source}: {amount} dmg (총 {_totalDamage})");
        if (_lines.Count > maxLines) _lines.Dequeue();
        logText.text = string.Join("\n", _lines);
    }

    public void ResetLog() { _lines.Clear(); _totalDamage = 0; logText.text = ""; }
}
#endif