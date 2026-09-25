// TimeLoopManager.cs (신규) — 실제 게임 내 회귀 로직 (디버그 도구인 DebugLoopTools와는 별개)
using System.Collections.Generic;
using UnityEngine;

public class TimeLoopManager : Singleton<TimeLoopManager>
{
    private List<TimeAnchorSnapshot> _anchors = new();
    private List<GameObject> _anchorMarkers = new();

    [Header("앵커 개수/자원")]
    public int setAnchorManaCost = 20;
    public int returnManaCost = 30;
    public int removeAnchorManaRefund = 10;
    public GameObject anchorMarkerPrefab;

    [Header("회귀 규칙")]
    [Tooltip("닻은 한 번 사용하면 사라진다 (기획서 7-5)")]
    public bool consumeAnchorOnUse = true;
    [Tooltip("과거의 닻으로 돌아가면 그보다 뒤에 내린 닻도 함께 사라진다")]
    public bool discardLaterAnchorsOnTravel = true;
    [Tooltip("마나 부족 강제 복귀(경로 2) 시 깎이는 정신력")]
    public int forcedReturnMentalPenalty = 10;
    [Tooltip("강제 복귀 후 회복되는 체력 비율")]
    [Range(0.1f, 1f)] public float forcedReturnHealthRatio = 0.5f;
    [Tooltip("정상 복귀 시 최소로 보장되는 체력")]
    public int minHealthAfterReturn = 10;
    [Tooltip("닻 없이 사망했을 때(경로 3) 되돌아가는 몸 레벨")]
    public int resetBodyLevel = 1;

    [Header("닻 최대 개수 (영혼 레벨 기준)")]
    [Tooltip("영혼 레벨이 이만큼 오를 때마다 최대 개수가 1개씩 늘어난다")]
    public int levelsPerExtraAnchor = 10;
    public int minAnchorCount = 1;
    public int maxAnchorCountCap = 10;

    [Header("닻 간격")]
    [Tooltip("현재 시점 이전의 마지막 닻과 최소 이 시간(인게임) 이상 떨어져야 새 닻을 내릴 수 있다")]
    public int minHoursBetweenAnchors = 24;

    [Header("알림 문구 키")]
    public string keyAnchorAir = "notify_anchor_air";
    public string keyAnchorMax = "notify_anchor_max";
    public string keyAnchorMana = "notify_anchor_mana";
    public string keyAnchorBattle = "notify_anchor_battle";
    public string keyAnchorTooSoon = "notify_anchor_too_soon";

    // TimeLoopManager.cs — 마커 관리 방식을 딕셔너리로 교체 (인덱스 매칭 방식은 씬이 바뀌면 깨지기 쉬워서)
    private Dictionary<TimeAnchorSnapshot, GameObject> _activeMarkers = new(); // 지금 로드된 씬에 실제로 떠있는 마커만

    public IReadOnlyList<TimeAnchorSnapshot> Anchors => _anchors;

    // ★ 영혼 레벨 기준 (몸 레벨은 회귀 경로에 따라 되돌아가므로)
    public int MaxAnchorCount(int soulLevel)
        => Mathf.Clamp((soulLevel - 1) / Mathf.Max(1, levelsPerExtraAnchor) + 1, minAnchorCount, maxAnchorCountCap);

    public int MaxAnchorCountFor(SoraStats sora)
       => sora == null ? minAnchorCount : MaxAnchorCount(sora.highestLevelReached);

    private void Awake()
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.OnSceneLoaded += HandleSceneLoaded;
    }
    private void OnDestroy()
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.OnSceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(int sceneID)
    {
        _activeMarkers.Clear(); // 이전 씬 마커는 씬 언로드로 이미 파괴됨, 참조만 정리
        foreach (var anchor in _anchors)
        {
            if (anchor.sceneID != sceneID || anchorMarkerPrefab == null) continue;
            var markerObj = Instantiate(anchorMarkerPrefab, anchor.position, Quaternion.identity);
            markerObj.GetComponent<TimeAnchorMarker>().snapshotData = anchor;
            _activeMarkers[anchor] = markerObj;
        }
    }

    // ★ 팝업을 띄우기 전에 먼저 검사한다 (기존에는 확인 버튼을 누른 뒤에야 실패를 알았다)
    public bool CanSetAnchor(out string reasonKey, out object[] reasonArgs)
    {
        reasonKey = null;
        reasonArgs = null;

        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;
        if (sora == null) return false;                           // 소라가 아니면 조용히 불가 (리엘 빙의 NRE 방지)

        if (UIModeManager.Instance != null && UIModeManager.Instance.CurrentMode == UIMode.Battle)
        { reasonKey = keyAnchorBattle; return false; }

        var motor = sora.GetComponent<IPlayerMotor>();
        if (sora.IsFlightForm || (motor != null && !motor.IsGrounded))
        { reasonKey = keyAnchorAir; return false; }

        int maxCount = MaxAnchorCountFor(sora);
        if (_anchors.Count >= maxCount)
        { reasonKey = keyAnchorMax; reasonArgs = new object[] { maxCount }; return false; }

        int remain = HoursUntilNextAnchor();
        if (remain > 0)
        { reasonKey = keyAnchorTooSoon; reasonArgs = new object[] { remain }; return false; }

        if (sora.currentMana < setAnchorManaCost)
        { reasonKey = keyAnchorMana; return false; }

        return true;
    }

    public void NotifyReason(string reasonKey, object[] reasonArgs)
    {
        if (string.IsNullOrEmpty(reasonKey) || NotificationManager.Instance == null) return;
        if (reasonArgs != null && reasonArgs.Length > 0)
            NotificationManager.Instance.ShowKeyFormat(reasonKey, NotificationType.Warning, reasonArgs);
        else
            NotificationManager.Instance.ShowKey(reasonKey, NotificationType.Warning);
    }

    // 현재 시점 이전의 마지막 닻 이후로 몇 시간 더 지나야 하는지 (0이면 지금 가능)
    public int HoursUntilNextAnchor()
    {
        var time = TimeManager.Instance;
        if (_anchors.Count == 0 || minHoursBetweenAnchors <= 0 || time == null) return 0;

        int now = ToAbsoluteHour(time.currentDay, time.currentHour);
        int latest = int.MinValue;
        foreach (var anchor in _anchors)
        {
            int at = ToAbsoluteHour(anchor.day, anchor.hour);
            if (at <= now) latest = Mathf.Max(latest, at);        // 미래의 닻은 기준에서 제외
        }
        if (latest == int.MinValue) return 0;

        return Mathf.Max(0, minHoursBetweenAnchors - (now - latest));
    }

    private int ToAbsoluteHour(int day, int hour)
    {
        int perDay = TimeManager.Instance != null ? TimeManager.Instance.coinsPerDay : 24;
        return (day - 1) * perDay + hour;
    }

    public bool TrySetAnchor()
    {
        // ★ 확인 버튼을 누르는 사이 상태가 바뀌었을 수 있으니 한 번 더 검사한다
        if (!CanSetAnchor(out string reasonKey, out object[] reasonArgs))
        {
            NotifyReason(reasonKey, reasonArgs);
            return false;
        }

        var sora = (SoraStats)PlayerManager.Instance.CurrentCharacter;

        sora.UseMana(setAnchorManaCost);
        var snapshot = new TimeAnchorSnapshot
        {
            sceneID = SceneLoader.Instance.CurrentSceneID,
            position = sora.transform.position,
            day = TimeManager.Instance.currentDay,
            hour = TimeManager.Instance.currentHour,
            level = sora.level,
            maxHealth = sora.maxHealth,
            maxMana = sora.maxMana,
            experience = sora.experience,
            // 변경 후 — 순서를 보존하고, 혼 층위인 개인친밀도는 담지 않는다
            acquiredMemoryFlags = new List<string>(MemoryManager.Instance.GetAllAcquired()),
            npcAffections = NPCManager.Instance.SnapshotAffections(),    
            npcSuspicions = SuspicionManager.Instance.Snapshot(),
            npcTrustEarned = SuspicionManager.Instance.SnapshotTrust(),
            npcLineCrossed = SuspicionManager.Instance.SnapshotLineCrossed(),
            counters = MemoryManager.Instance.SnapshotCounters(),          
            loopCountAtSave = sora.loopCount,                              
            actionLog = PlayerActionLog.Instance.Snapshot(),
        };
        _anchors.Add(snapshot);

        if (anchorMarkerPrefab != null)
        {
            var markerObj = Instantiate(anchorMarkerPrefab, sora.transform.position, Quaternion.identity);
            markerObj.GetComponent<TimeAnchorMarker>().snapshotData = snapshot;
            _activeMarkers[snapshot] = markerObj; // ★ 리스트 대신 딕셔너리
        }

        PlayerActionLog.Instance?.Record(RecordType.Counter, "anchor_set");

        return true;
    }

    public void RemoveAnchor(TimeAnchorSnapshot snapshot) // UI 창에서 삭제 버튼 누를 때 호출
    {
        if (!_anchors.Remove(snapshot)) return;
        if (_activeMarkers.TryGetValue(snapshot, out var marker))
        {
            if (marker != null) Destroy(marker);
            _activeMarkers.Remove(snapshot);
        }
        (PlayerManager.Instance.CurrentCharacter as SoraStats)?.RecoverMana(removeAnchorManaRefund);
    }

    // ★ 세 경로에 흩어져 있던 복원 코드를 하나로 모음.
    //   되돌리는 것은 "세계 쪽 상태"뿐이다. 기억·개인친밀도는 소라의 혼에 속해 유지된다
    private void RestoreWorldState(TimeAnchorSnapshot snapshot)
    {
        if (snapshot == null) return;

        NPCManager.Instance.RestoreAffections(snapshot.npcAffections);
        SuspicionManager.Instance.Restore(snapshot.npcSuspicions);
        SuspicionManager.Instance.RestoreTrust(snapshot.npcTrustEarned);
        SuspicionManager.Instance.RestoreLineCrossed(snapshot.npcLineCrossed);
        MemoryManager.Instance.RestoreCounters(snapshot.counters);
        PlayerActionLog.Instance.Restore(snapshot.actionLog);
    }

    // 몸 상태를 그 시점으로 되돌린다 (강제 복귀 전용)
    private void RestoreBody(SoraStats sora, TimeAnchorSnapshot snapshot)
    {
        sora.level = snapshot.level;
        sora.maxHealth = snapshot.maxHealth;
        sora.maxMana = snapshot.maxMana;
        sora.experience = snapshot.experience;
    }

    // ★ 사용한 닻을 소모하고, 과거로 갔다면 그보다 뒤의 닻도 정리한다 (기획서 7-5)
    private void ConsumeAnchor(TimeAnchorSnapshot used)
    {
        if (used == null) return;

        if (discardLaterAnchorsOnTravel)
        {
            int usedAt = ToAbsoluteHour(used.day, used.hour);
            for (int i = _anchors.Count - 1; i >= 0; i--)
            {
                if (_anchors[i] == used) continue;
                if (ToAbsoluteHour(_anchors[i].day, _anchors[i].hour) > usedAt) DiscardAnchor(_anchors[i]);
            }
        }

        if (consumeAnchorOnUse) DiscardAnchor(used);
    }

    // 마나 환급 없이 조용히 제거 (사용·소멸용). RemoveAnchor는 플레이어가 직접 거둘 때 쓴다
    private void DiscardAnchor(TimeAnchorSnapshot snapshot)
    {
        _anchors.Remove(snapshot);
        if (_activeMarkers.TryGetValue(snapshot, out var marker))
        {
            if (marker != null) Destroy(marker);
            _activeMarkers.Remove(snapshot);
        }
    }

    public bool TravelToAnchor(TimeAnchorSnapshot anchor)
    {
        if (anchor == null || !_anchors.Contains(anchor)) return false;

        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;
        if (sora == null) return false;                       // ★ null 체크 누락 수정

        if (sora.currentMana < returnManaCost)                // ★ 마나 비용이 적용되지 않던 문제 수정
        {
            NotifyReason(keyAnchorMana, null);
            return false;
        }

        sora.UseMana(returnManaCost);
        sora.loopCount++;                                     // 시간 역행이므로 회차 증가

        RestoreWorldState(anchor);
        ConsumeAnchor(anchor);                                // ★ 1회용 + 이후 닻 소멸

        LoadScene(anchor.sceneID, anchor.position, anchor.day, anchor.hour);
        return true;
    }

    // 기획서 7-2: 기억은 어떤 경로에서도 유지된다
    //   경로 1 (마나 충분)   — 마나 소모, 몸 유지
    //   경로 2 (마나 부족)   — 몸이 닻 시점으로, 정신력 하락
    //   경로 3 (닻 없음)     — Day 1부터, 몸 레벨 1
    public void HandleDeath()
    {
        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;
        if (sora == null) return;
        sora.loopCount++;

        var latest = _anchors.Count > 0 ? _anchors[_anchors.Count - 1] : null;

        if (latest != null && sora.currentMana >= returnManaCost)   // [경로 1] 정상 복귀
        {
            sora.UseMana(returnManaCost);
            RestoreWorldState(latest);
            sora.currentHealth = Mathf.Max(minHealthAfterReturn, sora.currentHealth);

            ConsumeAnchor(latest);
            LoadScene(latest.sceneID, latest.position, latest.day, latest.hour);
        }
        else if (latest != null)                                    // [경로 2] 마나 부족 강제 복귀
        {
            // ★ 기억은 되돌리지 않는다 (기존에는 RestoreAcquired로 기억까지 되돌렸다)
            RestoreWorldState(latest);
            RestoreBody(sora, latest);
            sora.LoseMental(forcedReturnMentalPenalty);
            sora.currentHealth = Mathf.Max(minHealthAfterReturn, Mathf.RoundToInt(sora.maxHealth * forcedReturnHealthRatio));

            ConsumeAnchor(latest);
            LoadScene(latest.sceneID, latest.position, latest.day, latest.hour);
        }
        else                                                        // [경로 3] 닻 없음 — Day 1부터
        {
            // ★ 기억은 유지한다. 되돌아가는 것은 세계와 몸이다
            MemoryManager.Instance.ClearAllCounters();
            NPCManager.Instance.ResetAffectionForNewLoop();          // SuspicionManager 리셋 포함
            PlayerActionLog.Instance.ClearAll();

            sora.ResetProgression();                                 // 몸 레벨을 기본값으로
            sora.level = resetBodyLevel;
            sora.currentHealth = sora.maxHealth;                     // ★ 체력을 회복하지 않아 0으로 시작하던 문제 수정
            sora.currentMana = sora.maxMana;

            var cfg = SceneLoader.Instance.startConfig;
            TimeManager.Instance.ResetToDay1();
            LoadScene(cfg.startSceneID, cfg.startPosition, 1, 0);
        }
    }

    private void LoadScene(int sceneID, Vector2 pos, int day, int hour)
    {
        TimeManager.Instance.SetTime(day, hour);
        SceneLoader.Instance.LoadScene(sceneID, pos);
    }
}