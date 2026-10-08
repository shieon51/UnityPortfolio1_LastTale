using UnityEngine;

// 마을 간 이동 시간을 계산한다.
// 씬 전환마다 차감하지 않고 "야외에 머문 시간"을 재므로,
// 왔던 길로 되돌아가도 공평하고 도중에 사냥한 시간도 자연스럽게 포함된다.
public class TravelTimeTracker : Singleton<TravelTimeTracker>
{
    // ★ 기록에 남는 이동 방식 키. 숫자처럼 고정값이므로 바꾸지 않는다
    public const string RecordKeyWalk = "walk";
    public const string RecordKeyInstant = "instant";

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

    [Header("문구 키 (기록장이 표시할 때 사용)")]
    public string keyTravelWalk = "flow_travel_walk";       // "이동"
    public string keyTravelInstant = "flow_travel_instant";  // "순간이동"

    private int _departureAbsoluteHour = -1;   // 야외로 나선 시각
    private int _departureSceneId = -1;        // 어디서 나섰는지
    private int _outdoorHops;                  // 야외 씬을 몇 번 거쳤는지 (거리)
    private bool _skipNextCost;                // 포탈·순간이동으로 들어온 경우
    private int _lastSceneId = -1;             // ★ 직전에 있던 씬

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

    // ★ 회귀처럼 "걸어서 이동한 것이 아닌" 씬 이동 직전에 호출한다.
    //   진행 중이던 이동을 버리고, 도착 씬을 새 출발점으로 삼는다
    public void CancelJourney()
    {
        _departureAbsoluteHour = -1;
        _departureSceneId = -1;
        _outdoorHops = 0;
        _skipNextCost = false;
        _lastSceneId = -1;
        _suppressNextArrival = true;
    }

    private bool _suppressNextArrival;   // ★ CancelJourney 직후의 도착은 이동으로 치지 않는다

    private void HandleSceneLoaded(int sceneId)
    {
        if (_suppressNextArrival || TimeManager.Instance == null)
        {
            _suppressNextArrival = false;
            _lastSceneId = sceneId;
            return;
        }

        int now = TimeManager.Instance.AbsoluteHour;

        if (!IsSettlement(sceneId))
        {
            if (_departureAbsoluteHour < 0)
            {
                _departureAbsoluteHour = now;
                _departureSceneId = _lastSceneId;   // ★ 방금 떠나온 씬 (기존에는 막 들어온 야외 씬이 기록됐다)
                _outdoorHops = 0;
            }
            _outdoorHops++;                         // 야외를 거칠수록 멀리 간 것
            _lastSceneId = sceneId;
            return;
        }

        if (_departureAbsoluteHour >= 0 && !_skipNextCost)
        {
            // 야외에서 이미 흐른 시간(대화·사냥)은 차감 대상에서 뺀다. 안 그러면 이중 차감이다
            int alreadyElapsed = Mathf.Max(0, now - _departureAbsoluteHour);
            int baseCost = minTravelHours + extraHoursPerScene * Mathf.Max(0, _outdoorHops - 1);
            if (maxTravelHours > 0) baseCost = Mathf.Min(baseCost, maxTravelHours);

            ApplyTravel(Mathf.Max(0, baseCost - alreadyElapsed), instant: false);
        }
        else if (_skipNextCost)
        {
            ApplyTravel(0, instant: true);
        }

        _departureAbsoluteHour = -1;
        _departureSceneId = -1;
        _outdoorHops = 0;
        _skipNextCost = false;
        _lastSceneId = sceneId;
    }

    private void ApplyTravel(int hours, bool instant)
    {
        // 야외에 머문 시간 중 "이미 흐른 시간"은 이벤트에서 차감됐으므로,
        // 여기서는 이동 자체의 비용만 추가로 차감한다
        if (hours > 0) GameManager.Instance?.CurrentGameMode?.ConsumeResourceForEvent(hours);

        // ★ 문구를 조립하지 않는다. key: 이동 방식, before: 출발 씬 ID, after: 소모 시간.
        //   도착 씬은 기록 자체의 sceneId(지금 씬)에 남는다
        int from = _departureSceneId >= 0 ? _departureSceneId : _lastSceneId;
        PlayerActionLog.Instance?.Record(RecordType.Travel, instant ? RecordKeyInstant : RecordKeyWalk, from, hours);
    }
}