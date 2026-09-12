// AmbientEventManager.cs (신규)
using UnityEngine;

// 꿈, 회상 등 위치 무관 이벤트 관리
public class AmbientEventManager : Singleton<AmbientEventManager>
{
    public string resourcesFolder = "AmbientEvents";
    private AmbientEventData[] _events;

    private void Awake() => _events = Resources.LoadAll<AmbientEventData>(resourcesFolder);

    private string FiredKey(AmbientEventData e) => $"ambient_fired_{e.eventId}";

    /// <summary>시간이 흐른 뒤나 잠자기 직후 등에서 호출</summary>
    public void TryTriggerAmbient(bool afterSleep = false)
    {
        if (DialogueManager.Instance.IsTalking || GlobalActionLock.IsLocked) return;

        var sora = PlayerManager.Instance.CurrentCharacter as SoraStats;
        if (sora == null) return;

        foreach (var e in _events)
        {
            if (e.onlyAfterSleep && !afterSleep) continue;
            if (e.onceOnly && MemoryManager.Instance.GetCounter(FiredKey(e)) > 0) continue;

            int hour = TimeManager.Instance.currentHour;
            if (hour < e.minHour || hour >= e.maxHour) continue;
            if (sora.loopCount < e.minLoopCount) continue;
            if (!string.IsNullOrEmpty(e.requiredFlagId) && !MemoryManager.Instance.HasMemory(e.requiredFlagId)) continue;
            if (sora.MentalRatio * 100f > e.maxMentalPercent) continue;
            if (Random.value > e.chance) continue;

            MemoryManager.Instance.IncrementCounter(FiredKey(e));
            Debug.Log($"[AmbientEvent] 발동: {e.eventId}");

            DialogueManager.Instance.StartStory(new EventData
            {
                EventID = 0,
                EventName = e.eventId,
                InkNodeName = e.inkNodeName,
                IsAnytime = true,
                TimeTaken = 0,
            });
            return; // 한 번에 하나만
        }
    }
}