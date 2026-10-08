using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 시간 코인 전담. 시각 문구는 GameTimeFormatter가 만든다 (기획서 5-4)
public class TimeCoinPanel : MonoBehaviour
{
    [Header("참조")]
    public Transform coinParent;
    public GameObject coinPrefab;
    public TextMeshProUGUI timeText, dayText;

    [Header("배치")]
    [Tooltip("코인이 배치될 원의 반지름")]
    public float radius = 80f;
    [Tooltip("첫 코인의 각도. 90이면 12시 방향에서 시작")]
    public float startAngleDegrees = 90f;
    [Tooltip("체크하면 시계 방향으로 배치")]
    public bool clockwise = true;

    [Header("색상")]
    public Color usedCoinColor = new Color(0.5f, 0, 0, 1);
    public Color unusedCoinColor = new Color(0, 1, 0.8f, 1);

    [Header("문구 (로컬라이제이션 키)")]
    public string dayFormatKey = "hud_day_format";           // 예: "Day {0}"
    [Tooltip("테이블에 키가 없을 때")]
    public string dayFormatFallback = "Day {0}";

    private readonly List<Image> _coinImages = new();

    // 하루 시간 수는 TimeManager가 기준이다
    private int TotalCoins => TimeManager.Instance != null ? TimeManager.Instance.coinsPerDay : 24;

    private void Start()
    {
        CreateTimeCoins();

        // ★ 시간제 설정이나 언어가 바뀌면 다시 그린다
        GameTimeFormatter.OnFormatChanged += Refresh;
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged += Refresh;

        if (TimeManager.Instance != null) TimeManager.Instance.OnTimeUpdated += UpdateTimeUI;
        Refresh();
    }

    private void OnDestroy()
    {
        GameTimeFormatter.OnFormatChanged -= Refresh;
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= Refresh;
        if (TimeManager.Instance != null) TimeManager.Instance.OnTimeUpdated -= UpdateTimeUI;
    }

    private void Refresh()
    {
        var time = TimeManager.Instance;
        if (time != null) UpdateTimeUI(time.timeCoins, time.currentDay);
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
        float direction = clockwise ? -1f : 1f;

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
        if (_coinImages.Count != TotalCoins) CreateTimeCoins();

        for (int i = 0; i < _coinImages.Count; i++)
            _coinImages[i].color = (i < remainingCoins) ? unusedCoinColor : usedCoinColor;

        // ★ 남은 코인에서 거꾸로 계산하지 않고 현재 시각을 그대로 쓴다
        int hour = TimeManager.Instance != null ? TimeManager.Instance.currentHour : 0;
        if (timeText != null) timeText.text = GameTimeFormatter.FormatTime(hour);
        if (dayText != null) dayText.text = Format(dayFormatKey, dayFormatFallback, currentDay);
    }

    private string Format(string key, string fallback, params object[] args)
    {
        var loc = LocalizationManager.Instance;
        if (loc != null && loc.Has(key)) return loc.GetFormat(key, args);
        return string.Format(fallback, args);
    }
}