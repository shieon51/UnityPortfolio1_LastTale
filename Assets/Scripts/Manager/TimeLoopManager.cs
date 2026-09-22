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
            acquiredMemoryFlags = new HashSet<string>(MemoryManager.Instance.GetAllAcquired()),
            npcAffections = NPCManager.Instance.SnapshotAffections(),    
            npcSuspicions = SuspicionManager.Instance.Snapshot(),
            npcTrustEarned = SuspicionManager.Instance.SnapshotTrust(),
            npcLineCrossed = SuspicionManager.Instance.SnapshotLineCrossed(),
            soraPersonalBond = sora.SnapshotPersonalBond(),
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

    public void TravelToAnchor(TimeAnchorSnapshot anchor)
    {
        if (anchor == null) return;
        var sora = PlayerManager.Instance.CurrentCharacter as SoraStats;
        if (sora != null) sora.loopCount++;              // ★ 추가 — 시간 역행이므로 회차 증가

        // ★ 리셋이 아니라 "그 시점 상태로 복원" — 이게 핵심
        NPCManager.Instance.RestoreAffections(anchor.npcAffections);
        SuspicionManager.Instance.Restore(anchor.npcSuspicions);
        SuspicionManager.Instance.RestoreTrust(anchor.npcTrustEarned);
        SuspicionManager.Instance.RestoreLineCrossed(anchor.npcLineCrossed);
        sora.RestorePersonalBond(anchor.soraPersonalBond);
        MemoryManager.Instance.RestoreCounters(anchor.counters);
        PlayerActionLog.Instance.Restore(anchor.actionLog); // ★ 추가

        TimeManager.Instance.SetTime(anchor.day, anchor.hour);
        SceneLoader.Instance.LoadScene(anchor.sceneID, anchor.position);
    }

    public void HandleDeath(bool keepBodyLevel = true)
    {
        var sora = PlayerManager.Instance.CurrentCharacter as SoraStats;
        if (sora == null) return;
        sora.loopCount++;

        bool hasAnchor = _anchors.Count > 0;
        var latest = hasAnchor ? _anchors[_anchors.Count - 1] : null;
        bool canReturn = hasAnchor && sora.currentMana >= returnManaCost;

        if (canReturn) // [경로 1] 앵커로 정상 복귀 — 그 시점 NPC 상태를 그대로 복원
        {
            sora.UseMana(returnManaCost);

            NPCManager.Instance.RestoreAffections(latest.npcAffections);  
            SuspicionManager.Instance.Restore(latest.npcSuspicions);
            SuspicionManager.Instance.RestoreTrust(latest.npcTrustEarned);
            SuspicionManager.Instance.RestoreLineCrossed(latest.npcLineCrossed);
            sora.RestorePersonalBond(latest.soraPersonalBond);
            MemoryManager.Instance.RestoreCounters(latest.counters);      
            PlayerActionLog.Instance.Restore(latest.actionLog); // ★ 추가

            if (!keepBodyLevel)
            {
                sora.level = latest.level; sora.maxHealth = latest.maxHealth;
                sora.maxMana = latest.maxMana; sora.experience = latest.experience;
            }
            sora.currentHealth = Mathf.Max(10, sora.currentHealth);
            LoadScene(latest.sceneID, latest.position, latest.day, latest.hour);
        }
        else if (hasAnchor) // [경로 2] 마나 부족 강제 복귀 — 기억까지 앵커 시점으로 되돌아감
        {
            MemoryManager.Instance.RestoreAcquired(latest.acquiredMemoryFlags);

            NPCManager.Instance.RestoreAffections(latest.npcAffections);    
            SuspicionManager.Instance.Restore(latest.npcSuspicions);
            SuspicionManager.Instance.RestoreTrust(latest.npcTrustEarned);           // ★ 추가
            SuspicionManager.Instance.RestoreLineCrossed(latest.npcLineCrossed);     // ★ 추가
            sora.RestorePersonalBond(latest.soraPersonalBond);                       // ★ 추가
            MemoryManager.Instance.RestoreCounters(latest.counters);
            PlayerActionLog.Instance.Restore(latest.actionLog); // ★ 추가

            sora.level = latest.level; sora.maxHealth = latest.maxHealth;
            sora.maxMana = latest.maxMana; sora.experience = latest.experience;
            sora.currentHealth = Mathf.Max(10, sora.maxHealth / 2);
            LoadScene(latest.sceneID, latest.position, latest.day, latest.hour);
        }
        else // [경로 3] 앵커 없음 — Day1부터 완전히 새로 (복원이 아니라 리셋)
        {
            MemoryManager.Instance.ClearAllAcquired();
            MemoryManager.Instance.ClearAllCounters();          // ★ 추가 — 만남/선택 기록도 초기화
            NPCManager.Instance.ResetAffectionForNewLoop();     // ★ 추가 (SuspicionManager 리셋도 이 안에 포함됨)
            PlayerActionLog.Instance.ClearAll(); // ★ 추가

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