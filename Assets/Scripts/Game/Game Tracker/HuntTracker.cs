using System.Globalization;
using UnityEngine;

// 사냥에 시간을 붙인다. 한 마리마다 차감하면 번거롭고,
// 아예 없으면 "레벨을 올리려면 시간을 쓴다"는 교환이 성립하지 않는다.
public class HuntTracker : Singleton<HuntTracker>
{
    [Header("시간 소모 기준")]
    [Tooltip("이 마릿수마다 1시간 (0이면 사용 안 함). 경험치 기준을 쓸 때는 보조용")]
    public int killsPerHour = 0;
    [Tooltip("이 경험치마다 1시간 (권장 기준). 플레이어가 강해져도 성과당 시간이 일정하다")]
    public int expPerHour = 200;

    [Header("기록")]
    [Tooltip("이 시간 동안 사냥이 없으면 한 덩어리가 끝난 것으로 보고 기록한다")]
    public float idleSecondsToFlush = 20f;

    private int _pendingKills;          // 아직 시간으로 바뀌지 않은 마릿수
    private int _pendingExp;
    private int _sessionKills;          // 이번 덩어리의 누적 (기록용)
    private int _sessionHours;
    private string _sessionTargetId;    // ★ 표시 이름 대신 고유 ID
    private float _lastKillTime = -999f;

    private void Update()
    {
        if (_sessionKills > 0 && Time.unscaledTime - _lastKillTime >= idleSecondsToFlush) FlushSession();
    }

    // 몬스터가 죽을 때 호출한다
    public void ReportKill(string targetId, int expGained)
    {
        // ★ 대상이 바뀌면 이전 덩어리를 먼저 기록한다 (섞인 사냥이 마지막 대상 이름으로만 남던 문제)
        if (_sessionKills > 0 && _sessionTargetId != targetId) FlushSession();

        _pendingKills++;
        _pendingExp += expGained;
        _sessionKills++;
        _sessionTargetId = targetId;
        _lastKillTime = Time.unscaledTime;

        int hours = 0;
        if (killsPerHour > 0) { hours += _pendingKills / killsPerHour; _pendingKills %= killsPerHour; }
        if (expPerHour > 0) { hours += _pendingExp / expPerHour; _pendingExp %= expPerHour; }

        if (hours <= 0) return;

        _sessionHours += hours;
        GameManager.Instance?.CurrentGameMode?.ConsumeResourceForEvent(hours);
    }

    // 한 덩어리를 기록으로 남긴다 (씬 이동·대화 시작 전에도 호출)
    public void FlushSession()
    {
        if (_sessionKills <= 0) return;

        // ★ 문구("슬라임 15마리")를 조립하지 않는다. key: 대상 ID, after: 소모 시간, payload: 마릿수
        PlayerActionLog.Instance?.Record(RecordType.Hunt, _sessionTargetId ?? string.Empty, 0, _sessionHours,
            payload: _sessionKills.ToString(CultureInfo.InvariantCulture));

        _sessionKills = 0;
        _sessionHours = 0;
        _sessionTargetId = null;
    }
}