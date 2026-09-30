using System.Collections.Generic;
using UnityEngine;

// 사냥에 시간을 붙인다. 한 마리마다 차감하면 번거롭고,
// 아예 없으면 "레벨을 올리려면 시간을 쓴다"는 교환이 성립하지 않는다.
public class HuntTracker : Singleton<HuntTracker>
{
    [Header("시간 소모 기준")]
    [Tooltip("이 마릿수마다 1시간이 흐른다 (0이면 마릿수 기준 사용 안 함)")]
    public int killsPerHour = 10;
    [Tooltip("이 경험치마다 1시간이 흐른다 (0이면 경험치 기준 사용 안 함)")]
    public int expPerHour = 0;

    [Header("기록")]
    [Tooltip("이 시간 동안 사냥이 없으면 한 덩어리가 끝난 것으로 보고 기록한다")]
    public float idleSecondsToFlush = 20f;

    private int _pendingKills;          // 아직 시간으로 바뀌지 않은 마릿수
    private int _pendingExp;
    private int _sessionKills;          // 이번 덩어리의 누적 (기록용)
    private int _sessionHours;
    private string _sessionTargetName;
    private float _lastKillTime = -999f;

    private void Update()
    {
        if (_sessionKills > 0 && Time.unscaledTime - _lastKillTime >= idleSecondsToFlush) FlushSession();
    }

    // 몬스터가 죽을 때 호출한다
    public void ReportKill(string targetName, int expGained)
    {
        _pendingKills++;
        _pendingExp += expGained;
        _sessionKills++;
        _sessionTargetName = targetName;
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

        string label = string.IsNullOrEmpty(_sessionTargetName) ? "사냥" : _sessionTargetName;
        PlayerActionLog.Instance?.Record(RecordType.Hunt, $"{label} {_sessionKills}마리", 0, _sessionHours);

        _sessionKills = 0;
        _sessionHours = 0;
        _sessionTargetName = null;
    }
}