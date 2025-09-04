using UnityEngine;
using System;

public class TimeManager : Singleton<TimeManager>
{
    public int timeCoins = 24; // 하루에 주어지는 시간 코인
    public int currentHour = 0;
    public int currentDay = 1;

    public event Action OnTimeChanged;
    public event Action OnDayChanged;
    public event Action<int, int> OnTimeUpdated; // 남은 시간 코인, 현재 날짜 -> UIManager에서 시간 UI 업데이트

    public void UseTimeCoins(int amount)
    {
        timeCoins -= amount;
        currentHour += amount;

        if (currentHour >= 24)
        {
            NextDay();
        }

        OnTimeChanged?.Invoke(); //**아직 안 쓰임

        // UI 갱신 이벤트 호출
        OnTimeUpdated?.Invoke(timeCoins, currentDay);

        EventManager.Instance.UpdateEventTriggers();
    }

    private void NextDay()
    {
        timeCoins += 24;
        currentHour -= 24;
        currentDay++;

        OnDayChanged?.Invoke(); //** 아직 안 쓰임
    }
}
