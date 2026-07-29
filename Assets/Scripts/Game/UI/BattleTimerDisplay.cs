using TMPro;
using UnityEngine;

// BattleTimerDisplay.cs - 보스전 타이머
public class BattleTimerDisplay : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    private float _elapsed;
    private bool _running;

    public void StartTimer() { _elapsed = 0f; _running = true; }
    public void StopTimer() => _running = false;

    private void Update()
    {
        if (!_running) return;
        _elapsed += Time.deltaTime;
        int m = Mathf.FloorToInt(_elapsed / 60f);
        int s = Mathf.FloorToInt(_elapsed % 60f);
        timerText.text = $"{m:00}:{s:00}";
    }
}