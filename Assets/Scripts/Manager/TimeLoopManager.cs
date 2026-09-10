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

    // TimeLoopManager.cs — 마커 관리 방식을 딕셔너리로 교체 (인덱스 매칭 방식은 씬이 바뀌면 깨지기 쉬워서)
    private Dictionary<TimeAnchorSnapshot, GameObject> _activeMarkers = new(); // 지금 로드된 씬에 실제로 떠있는 마커만

    public IReadOnlyList<TimeAnchorSnapshot> Anchors => _anchors;
    public int MaxAnchorCount(int level) => Mathf.Clamp((level - 1) / 10 + 1, 1, 10); // 1~9→1, ..., 90~99→10

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

    public bool TrySetAnchor()
    {
        var sora = PlayerManager.Instance.CurrentCharacter as SoraStats;
        if (sora == null) return false;

        var motor = sora.GetComponent<IPlayerMotor>();
        if (sora.IsFlightForm || (motor != null && !motor.IsGrounded))
        {
            NotificationManager.Instance?.Show("공중에서는 시간을 고정할 수 없습니다", NotificationType.Warning);
            return false;
        }

        int maxCount = MaxAnchorCount(sora.level);
        if (_anchors.Count >= maxCount)
        {
            NotificationManager.Instance?.Show($"시간 고정 최대 개수({maxCount}개)에 도달했습니다", NotificationType.Warning);
            return false;
        }
        if (sora.currentMana < setAnchorManaCost)
        {
            NotificationManager.Instance?.Show("마나가 부족하여 시간을 고정할 수 없습니다", NotificationType.Warning);
            return false;
        }

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