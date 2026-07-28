using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 시간 코인 전담 
public class TimeCoinPanel : MonoBehaviour
{
    public Transform coinParent;
    public GameObject coinPrefab;
    public TextMeshProUGUI timeText, dayText;
    public Color usedCoinColor = new Color(0.5f, 0, 0, 1);
    public Color unusedCoinColor = new Color(0, 1, 0.8f, 1);

    private List<Image> _coinImages = new List<Image>();
    private const int TotalCoins = 24;

    private void Start()
    {
        CreateTimeCoins();
        TimeManager.Instance.OnTimeUpdated += UpdateTimeUI; // ★ 이제 한 번만 구독됨
        UpdateTimeUI(TimeManager.Instance.timeCoins, TimeManager.Instance.currentDay);
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.OnTimeUpdated -= UpdateTimeUI;
    }

    private void CreateTimeCoins()
    {
        float radius = 80f;
        float angleStep = 360f / TotalCoins;
        for (int i = 0; i < TotalCoins; i++)
        {
            GameObject coin = Instantiate(coinPrefab, coinParent);
            _coinImages.Add(coin.GetComponent<Image>());
            float angle = (i * angleStep + 90) * Mathf.Deg2Rad;
            coin.GetComponent<RectTransform>().anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
    }

    public void UpdateTimeUI(int remainingCoins, int currentDay)
    {
        for (int i = 0; i < TotalCoins; i++)
            _coinImages[i].color = (i < remainingCoins) ? unusedCoinColor : usedCoinColor;

        int hour = 24 - remainingCoins;
        string period = hour < 12 ? "AM" : "PM";
        int displayHour = (hour % 12 == 0) ? 12 : hour % 12;
        timeText.text = $"{period} {displayHour}:00";
        dayText.text = $"Day {currentDay}";
    }
}