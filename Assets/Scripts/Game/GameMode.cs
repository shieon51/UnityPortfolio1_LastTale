using UnityEngine;

// [GameMode.cs]
public interface IGameMode
{
    // 이벤트가 활성화될 조건(시간, 날짜 등)이 맞는지 검사하는 함수
    bool IsEventValid(EventData eventData, int currentSceneID);

    // 이벤트를 겪었을 때 시간이나 행동력을 소비하는 룰
    void ConsumeResourceForEvent(int amount);
}

// 1부 전용 룰 (소라, 시간 코인 시스템)
public class Season1Mode : IGameMode
{
    public bool IsEventValid(EventData eventData, int currentSceneID)
    {
        // 1. 씬 일치 여부
        if (eventData.SceneID != currentSceneID) return false;

        // 2. 시간/날짜 일치 여부 (1부 전용 룰)
        int currentDay = TimeManager.Instance.currentDay;
        int currentTime = TimeManager.Instance.currentHour;

        return eventData.IsAnytime ||
               (eventData.Day == currentDay && currentTime >= eventData.StartTime && currentTime < eventData.EndTime);
    }

    public void ConsumeResourceForEvent(int amount)
    {
        TimeManager.Instance.UseTimeCoins(amount); // 시간 코인 소모
    }
}

// 2부 전용 룰 (리엘, 턴제/행동력 시스템 등으로 가정)
public class Season2Mode : IGameMode
{
    public bool IsEventValid(EventData eventData, int currentSceneID)
    {
        // 2부에서는 시간/날짜 상관없이 씬에 있으면 무조건 이벤트가 발생한다고 가정
        return eventData.SceneID == currentSceneID;
    }

    public void ConsumeResourceForEvent(int amount)
    {
        // 예: 2부에서는 시간 코인 대신 행동력(Action Point) 감소 로직 호출
        // ActionPointManager.Instance.UseAP(amount); 
    }
}
