using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 시간 코인 전담
public class TimeCoinPanel : MonoBehaviour
{
    [Header("참조")]
    public Transform coinParent;
    public GameObject coinPrefab;
    public TextMeshProUGUI timeText, dayText;

    [Header("배치")]                                         // ★ 하드코딩 제거
    [Tooltip("코인이 배치될 원의 반지름")]
    public float radius = 80f;
    [Tooltip("첫 코인의 각도. 90이면 12시 방향에서 시작")]
    public float startAngleDegrees = 90f;
    [Tooltip("체크하면 시계 방향으로 배치")]
    public bool clockwise = true;

    [Header("색상")]
    public Color usedCoinColor = new Color(0.5f, 0, 0, 1);
    public Color unusedCoinColor = new Color(0, 1, 0.8f, 1);

    [Header("시각 표기")]
    [Tooltip("체크하면 AM/PM 12시간제, 해제하면 24시간제로 표시한다. 하루가 24시간이 아니면 자동으로 24시간제를 쓴다")]
    public bool use12HourClock = true;

    [Header("문구 (로컬라이제이션 키)")]                      // ★ 문자열 하드코딩 제거
    public string dayFormatKey = "hud_day_format";           // 예: "Day {0}"
    public string timeFormatKey = "hud_time_format";         // 예: "{0} {1}:00"
    public string amKey = "hud_time_am";                     // 예: "AM"
    public string pmKey = "hud_time_pm";                     // 예: "PM"
    public string time24FormatKey = "hud_time_format_24";   // 예: "{0}:00"

    [Header("문구 기본값 (테이블에 키가 없을 때)")]
    public string dayFormatFallback = "Day {0}";
    public string timeFormatFallback = "{0} {1}:00";
    public string amFallback = "AM";
    public string pmFallback = "PM";
    public string time24FormatFallback = "{0}:00";

    private readonly List<Image> _coinImages = new();

    // 하루 시간 수는 TimeManager가 기준이다
    private int TotalCoins => TimeManager.Instance != null ? TimeManager.Instance.coinsPerDay : 24;

    private void Start()
    {
        CreateTimeCoins();

        var time = TimeManager.Instance;
        if (time == null) return;

        time.OnTimeUpdated += UpdateTimeUI;
        UpdateTimeUI(time.timeCoins, time.currentDay);
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.OnTimeUpdated -= UpdateTimeUI;
    }

    private void CreateTimeCoins()
    {
        if (coinPrefab == null || coinParent == null)
        {
            Debug.LogError("[TimeCoinPanel] coinPrefab 또는 coinParent가 비어있습니다.", this);
            return;
        }

        foreach (var image in _coinImages) if (image != null) Destroy(image.gameObject);
        _coinImages.Clear();

        int total = TotalCoins;
        float angleStep = 360f / total;
        float direction = clockwise ? -1f : 1f;               // ★ 방향도 옵션으로

        for (int i = 0; i < total; i++)
        {
            GameObject coin = Instantiate(coinPrefab, coinParent);
            var image = coin.GetComponent<Image>();
            if (image != null) _coinImages.Add(image);

            float angle = (startAngleDegrees + direction * i * angleStep) * Mathf.Deg2Rad;
            var rect = coin.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
    }

    public void UpdateTimeUI(int remainingCoins, int currentDay)
    {
        int total = TotalCoins;

        if (_coinImages.Count != total) CreateTimeCoins();

        for (int i = 0; i < _coinImages.Count; i++)
            _coinImages[i].color = (i < remainingCoins) ? unusedCoinColor : usedCoinColor;

        int hour = Mathf.Clamp(total - remainingCoins, 0, total);

        // ★ AM/PM은 하루가 24시간일 때만 의미가 있다. 그 외에는 24시간제로 표시한다
        bool use12 = use12HourClock && total == 24;

        if (use12)
        {
            string period = hour < 12 ? Text(amKey, amFallback) : Text(pmKey, pmFallback);
            int displayHour = (hour % 12 == 0) ? 12 : hour % 12;
            if (timeText != null) timeText.text = Format(timeFormatKey, timeFormatFallback, period, displayHour);
        }
        else
        {
            if (timeText != null) timeText.text = Format(time24FormatKey, time24FormatFallback, hour);
        }

        if (dayText != null) dayText.text = Format(dayFormatKey, dayFormatFallback, currentDay);
    }

    private string Text(string key, string fallback)
    {
        var loc = LocalizationManager.Instance;
        return (loc != null && loc.Has(key)) ? loc.Get(key) : fallback;
    }

    private string Format(string key, string fallback, params object[] args)
    {
        var loc = LocalizationManager.Instance;
        if (loc != null && loc.Has(key)) return loc.GetFormat(key, args);
        return string.Format(fallback, args);
    }
}