using UnityEngine;
using System;

public class TimeManager : Singleton<TimeManager>
{
    [Header("하루 설정")]
    [Tooltip("하루에 주어지는 시간 수 (UI의 코인 개수와 같은 값)")]
    public int coinsPerDay = 24;          // ★ 하드코딩 제거

    public int timeCoins = 24;            // 남은 시간
    public int currentHour = 0;
    public int currentDay = 1;

    public event Action<int, int> OnTimeUpdated; // 남은 시간 코인, 현재 날짜 -> UIManager에서 시간 UI 업데이트

    private void Awake() => timeCoins = coinsPerDay;  

    public void UseTimeCoins(int amount)
    {
        timeCoins -= amount;
        currentHour += amount;

        if (currentHour >= coinsPerDay)
        {
            NextDay();
        }

        //OnTimeChanged?.Invoke(); //**아직 안 쓰임

        // UI 갱신 이벤트 호출
        OnTimeUpdated?.Invoke(timeCoins, currentDay);

        EventManager.Instance.UpdateEventTriggers();
        AmbientEventManager.Instance?.TryTriggerAmbient(); // 시간이 흐를 때마다 판정
    }

    private void NextDay()
    {
        timeCoins += coinsPerDay;          // 변경 전: timeCoins += 24
        currentHour -= coinsPerDay;        // 변경 전: currentHour -= 24
        currentDay++;

        //OnDayChanged?.Invoke(); //** 아직 안 쓰임
    }

    public void ResetToDay1()
    {
        timeCoins = coinsPerDay;
        currentHour = 0;
        currentDay = 1;
        OnTimeUpdated?.Invoke(timeCoins, currentDay);
        EventManager.Instance?.UpdateEventTriggers();
    }

    // 앵커 복귀 시 임의 시각으로 세팅하기 위함
    public void SetTime(int day, int hour)
    {
        currentDay = day;
        currentHour = hour;
        timeCoins = coinsPerDay - hour;
        OnTimeUpdated?.Invoke(timeCoins, currentDay);
        EventManager.Instance?.UpdateEventTriggers();
    }
}
