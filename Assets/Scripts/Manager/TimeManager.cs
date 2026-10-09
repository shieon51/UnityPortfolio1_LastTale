using UnityEngine;
using System;

public class TimeManager : Singleton<TimeManager>, IRecordable   // ★ 2-C-2 — 날짜·시각을 범용 스냅샷에 포함 (World)
{
    [Header("하루 설정")]
    [Tooltip("하루에 주어지는 시간 수 (UI의 코인 개수와 같은 값)")]
    public int coinsPerDay = 24;          // ★ 하드코딩 제거

    public int timeCoins = 24;            // 남은 시간
    public int currentHour = 0;
    public int currentDay = 1;

    public event Action<int, int> OnTimeUpdated; // 남은 시간 코인, 현재 날짜 -> UIManager에서 시간 UI 업데이트

    // ★ Day 1 00:00부터 흐른 시간. 기록과 닻 간격 계산에 쓴다
    public int AbsoluteHour => (currentDay - 1) * coinsPerDay + currentHour;

    private void Awake()
    {
        timeCoins = coinsPerDay;
        RecordSystem.Register(this);   // ★ 2-C-2
    }
    private void OnDestroy() => RecordSystem.Unregister(this);

    public void UseTimeCoins(int amount)
    {
        int before = AbsoluteHour;           // ★

        timeCoins -= amount;
        currentHour += amount;

        if (currentHour >= coinsPerDay)
        {
            NextDay();
        }

        RecordTimeChange(before);            // ★

        // UI 갱신 이벤트 호출
        OnTimeUpdated?.Invoke(timeCoins, currentDay);

        EventManager.Instance.UpdateEventTriggers();
        AmbientEventManager.Instance?.TryTriggerAmbient(); // 시간이 흐를 때마다 판정
    }

    private void NextDay()
    {
        timeCoins += coinsPerDay;
        currentHour -= coinsPerDay;
        currentDay++;
    }

    public void ResetToDay1()
    {
        int before = AbsoluteHour;           // ★
        timeCoins = coinsPerDay;
        currentHour = 0;
        currentDay = 1;
        RecordTimeChange(before);            // ★
        OnTimeUpdated?.Invoke(timeCoins, currentDay);
        EventManager.Instance?.UpdateEventTriggers();
    }

    // 앵커 복귀 시 임의 시각으로 세팅하기 위함
    public void SetTime(int day, int hour)
    {
        int before = AbsoluteHour;           // ★
        currentDay = day;
        currentHour = hour;
        timeCoins = coinsPerDay - hour;
        RecordTimeChange(before);            // ★
        OnTimeUpdated?.Invoke(timeCoins, currentDay);
        EventManager.Instance?.UpdateEventTriggers();
    }

    // ---------------- IRecordable (★ 기록 시스템 2-C-2) ----------------
    // 남은 코인은 시각에서 계산되므로(SetTime) 담지 않는다.
    // 회귀에서는 RestoreLayers가 이 덩어리를 건너뛴다 — 시각은 회귀 기록을 남긴 뒤 TimeLoopManager.LoadScene에서 바꾼다.
    // ReadState는 세이브 불러오기(4단계)처럼 덩어리를 바로 적용할 때 쓰인다
    private const string StateKeyDay = "day";     // 덩어리 안의 키. 바꾸지 않는다
    private const string StateKeyHour = "hour";

    public string RecordId => RecordIds.TimeClock;
    public RecordLayer Layer => RecordLayer.World;
    public int StateVersion => 1;

    public void WriteState(StateWriter writer)
    {
        writer.WriteInt(StateKeyDay, currentDay);
        writer.WriteInt(StateKeyHour, currentHour);
    }

    public void ReadState(StateReader reader, int version)
        => SetTime(reader.ReadInt(StateKeyDay, currentDay), reader.ReadInt(StateKeyHour, currentHour));

    // ★ 시각 변화 기록. 남은 코인은 시각에서 계산되므로 따로 남기지 않는다
    private void RecordTimeChange(int beforeAbsolute)
    {
        if (AbsoluteHour == beforeAbsolute) return;
        PlayerActionLog.Instance?.Record(RecordType.TimeAdvance, RecordKeys.Time, beforeAbsolute, AbsoluteHour);
    }
}