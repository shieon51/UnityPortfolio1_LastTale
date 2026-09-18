using TMPro;
using UnityEngine;

// BattleTimerDisplay.cs - 보스전 타이머
// ★ 표시 여부는 UIModeManager(HUD_Battle)가 관리한다. 여기서는 알파를 건드리지 않는다.
public class BattleTimerDisplay : Singleton<BattleTimerDisplay>
{
    public TextMeshProUGUI timerText;

    [Tooltip("타이머가 멈췄을 때 표시할 문구")]
    public string idleText = "00:00";

    private float _elapsed;
    private bool _running;

    public bool IsRunning => _running;
    public float Elapsed => _elapsed;

    private void Awake()
    {
        var cg = GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = false; }   // ★ 부모 알파와 곱해지므로 항상 1

        _elapsed = 0f;
        _running = false;
        if (timerText != null) timerText.text = idleText;
    }

    public void StartTimer()
    {
        _elapsed = 0f;
        _running = true;
    }

    public void StopTimer() => _running = false;

    private void Update()
    {
        if (!_running) return;
        _elapsed += Time.deltaTime;
        int m = Mathf.FloorToInt(_elapsed / 60f);
        int s = Mathf.FloorToInt(_elapsed % 60f);
        if (timerText != null) timerText.text = $"{m:00}:{s:00}";
    }
}