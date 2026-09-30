using UnityEngine;

// 마을 간 이동 시간을 계산한다.
// 씬 전환마다 차감하지 않고 "야외에 머문 시간"을 재므로,
// 왔던 길로 되돌아가도 공평하고 도중에 사냥한 시간도 자연스럽게 포함된다.
public class TravelTimeTracker : Singleton<TravelTimeTracker>
{
    [Header("야외 판정")]
    [Tooltip("이 씬 ID들은 마을(정착지)로 취급한다. 나머지는 모두 야외")]
    public int[] settlementSceneIds = { 1 };

    [Header("이동 비용")]
    [Tooltip("야외를 한 번 거칠 때의 기본 이동 시간")]
    public int minTravelHours = 1;
    [Tooltip("야외 씬을 더 거칠 때마다 추가되는 시간")]
    public int extraHoursPerScene = 1;
    [Tooltip("한 번의 이동으로 차감할 수 있는 최대 시간 (0이면 제한 없음)")]
    public int maxTravelHours = 0;

    [Header("문구 키")]
    public string keyTravelWalk = "flow_travel_walk";       // "이동"
    public string keyTravelInstant = "flow_travel_instant";  // "순간이동"

    private int _departureAbsoluteHour = -1;   // 야외로 나선 시각
    private int _departureSceneId = -1;        // 어디서 나섰는지
    private int _outdoorHops;                 // ★ 야외 씬을 몇 번 거쳤는지 (거리)
    private bool _skipNextCost;                // 포탈·순간이동으로 들어온 경우

    public bool IsSettlement(int sceneId) => System.Array.IndexOf(settlementSceneIds, sceneId) >= 0;

    private void Awake()
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.OnSceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.OnSceneLoaded -= HandleSceneLoaded;
    }

    // 포탈·순간이동처럼 시간을 쓰지 않는 이동 직전에 호출한다
    public void SkipNextTravelCost() => _skipNextCost = true;

    private void HandleSceneLoaded(int sceneId)
    {
        if (TimeManager.Instance == null) return;
        int now = ToAbsoluteHour(TimeManager.Instance.currentDay, TimeManager.Instance.currentHour);

        if (!IsSettlement(sceneId))
        {
            if (_departureAbsoluteHour < 0)
            {
                _departureAbsoluteHour = now;
                _departureSceneId = SceneLoader.Instance != null ? SceneLoader.Instance.CurrentSceneID : -1;
                _outdoorHops = 0;
            }
            _outdoorHops++;                   // 야외를 거칠수록 멀리 간 것
            return;
        }

        if (_departureAbsoluteHour >= 0 && !_skipNextCost)
        {
            // ★ 야외에서 이미 흐른 시간(대화·사냥)은 차감 대상에서 뺀다. 안 그러면 이중 차감이다
            int alreadyElapsed = Mathf.Max(0, now - _departureAbsoluteHour);
            int baseCost = minTravelHours + extraHoursPerScene * Mathf.Max(0, _outdoorHops - 1);
            if (maxTravelHours > 0) baseCost = Mathf.Min(baseCost, maxTravelHours);

            ApplyTravel(Mathf.Max(0, baseCost - alreadyElapsed), sceneId, instant: false);
        }
        else if (_skipNextCost)
        {
            ApplyTravel(0, sceneId, instant: true);
        }

        _departureAbsoluteHour = -1;
        _departureSceneId = -1;
        _outdoorHops = 0;
        _skipNextCost = false;
    }

    private void ApplyTravel(int hours, int arrivedSceneId, bool instant)
    {
        // ★ 야외에 머문 시간 중 "이미 흐른 시간"은 이벤트에서 차감됐으므로,
        //   여기서는 이동 자체의 비용만 추가로 차감한다
        if (hours > 0) GameManager.Instance?.CurrentGameMode?.ConsumeResourceForEvent(hours);

        string from = SceneNameUtil.GetDisplayName(_departureSceneId);
        string to = SceneNameUtil.GetDisplayName(arrivedSceneId);
        string label = Text(instant ? keyTravelInstant : keyTravelWalk, instant ? "순간이동" : "이동");

        PlayerActionLog.Instance?.Record(RecordType.Travel, $"{from} → {to}", 0, hours, label);
    }

    private static int ToAbsoluteHour(int day, int hour)
    {
        int perDay = TimeManager.Instance != null ? TimeManager.Instance.coinsPerDay : 24;
        return (day - 1) * perDay + hour;
    }

    private static string Text(string key, string fallback)
    {
        var loc = LocalizationManager.Instance;
        return (loc != null && loc.Has(key)) ? loc.Get(key) : fallback;
    }
}