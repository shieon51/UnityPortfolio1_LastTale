using TMPro;
using UnityEngine;

// BattleTimerDisplay.cs - 보스전 타이머
public class BattleTimerDisplay : Singleton<BattleTimerDisplay>
{
    public CanvasGroup canvasGroup; // 이 오브젝트에 CanvasGroup 컴포넌트 추가
    public TextMeshProUGUI timerText;
    private float _elapsed;
    private bool _running;

    private void Awake() => Hide();

    public void Show() { if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = true; } }
    public void Hide() { if (canvasGroup != null) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; } }

    public void StartTimer() { _elapsed = 0f; _running = true; Show(); }
    public void StopTimer() { _running = false; Hide(); }

    private void Update()
    {
        if (!_running) return;
        _elapsed += Time.deltaTime;
        int m = Mathf.FloorToInt(_elapsed / 60f);
        int s = Mathf.FloorToInt(_elapsed % 60f);
        if (timerText != null) timerText.text = $"{m:00}:{s:00}";
    }
}