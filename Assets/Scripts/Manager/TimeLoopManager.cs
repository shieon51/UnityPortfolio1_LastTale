// TimeLoopManager.cs (신규) — 실제 게임 내 회귀 로직 (디버그 도구인 DebugLoopTools와는 별개)
using System.Collections.Generic;
using UnityEngine;

public class TimeLoopManager : Singleton<TimeLoopManager>
{
    private List<TimeAnchorSnapshot> _anchors = new();
    private List<GameObject> _anchorMarkers = new();

    // ★ 누적 닻 번호. 세계 전체에서 하나씩 증가하며 회귀해도 되돌리지 않는다 (세이브가 생기면 저장 대상)
    private int _anchorSerial = 0;
    public int AnchorSerial => _anchorSerial;

    // ★ 닻으로 돌아간 경로. 기록에 숫자로 남으므로 값을 바꾸지 않는다
    public enum ReturnPath { Voluntary = 0, Normal = 1, Forced = 2, Day1 = 3 }   // ★ 3-A — Day1(경로 3) 추가. LoopRecord.returnPath에 쓴다

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
    // ★ 경로 2는 닻 시점의 체력·마나로 돌아가므로 비율 설정(forcedReturnHealthRatio)을 없앴다
    [Header("경로 1 회복 — 남은 마나를 생명력으로 바꿔 몸을 회복한다 (기획서 7-2)")]
    [Tooltip("경로 1로 돌아올 때 마나로 회복시키는 목표 체력 (최대 체력 대비 비율)")]
    [Range(0f, 1f)] public float deathReturnHealTargetRatio = 0.5f;   // ★
    [Tooltip("마나 1로 회복하는 체력. 0이면 마나로 회복하지 않는다")]
    public float healthPerMana = 2f;                                  // ★
    [Tooltip("정상 복귀 시 마나가 모자라도 최소로 보장되는 체력")]
    public int minHealthAfterReturn = 10;
    //[Tooltip("닻 없이 사망했을 때(경로 3) 되돌아가는 몸 레벨")]
    //public int resetBodyLevel = 1;

    [Header("기록 시스템 검증 (개발용)")]
    [Tooltip("닻 복귀 직후, 옛 방식으로 복원한 상태가 닻의 범용 스냅샷과 같은지 비교해 콘솔에 남긴다. 에디터·개발 빌드에서만 동작")]
    public bool verifyRecordSnapshot = true;   // ★ 기록 시스템 2단계 — 복원 교체 전 검증용

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
    public string keyTravelMana = "notify_travel_mana";      // ★ 추가

    // TimeLoopManager.cs — 마커 관리 방식을 딕셔너리로 교체 (인덱스 매칭 방식은 씬이 바뀌면 깨지기 쉬워서)
    private Dictionary<TimeAnchorSnapshot, GameObject> _activeMarkers = new(); // 지금 로드된 씬에 실제로 떠있는 마커만

    public IReadOnlyList<TimeAnchorSnapshot> Anchors => _anchors;

    // ★ 영혼 레벨 기준 (몸 레벨은 회귀 경로에 따라 되돌아가므로)
    public int MaxAnchorCount(int soulLevel)
        => Mathf.Clamp((soulLevel - 1) / Mathf.Max(1, levelsPerExtraAnchor) + 1, minAnchorCount, maxAnchorCountCap);

    public int MaxAnchorCountFor(SoraStats sora)
       => sora == null ? minAnchorCount : MaxAnchorCount(sora.highestLevelReached);

    // ★ 2-C — 파트 시작 스냅샷 (1회차 Day 1을 시작하는 순간). 경로 3은 세계와 몸을 이것으로 되돌린다 (설계 4장).
    //   세이브가 생기면(4단계) 파일에 함께 저장해야 한다. 지금은 플레이를 시작할 때마다 새로 찍는다
    private Snapshot _partStartSnapshot;

    // ★ 2-C-2 — 회귀 복원(RestoreLayers)과 검증에서 건너뛰는 덩어리. 시각·씬은 비동기 씬 로드와 기록 순서 때문에
    //   회귀 기록을 남긴 뒤 LoadScene에서 따로 옮긴다 (목적지는 닻의 day·hour·sceneID·position)
    private static readonly HashSet<string> MovedByLoadScene = new() { RecordIds.TimeClock, RecordIds.SceneLocation };

    // ★ 3-A — 새 회차를 연 뒤 씬 로드가 끝나면 회차 시작 스냅샷을 찍는다.
    //   회귀 직후에는 씬이 비동기로 바뀌는 중이라 시각·위치 덩어리가 아직 옛 값이기 때문이다
    private bool _pendingLoopStartSnapshot;

    private void Awake()
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.OnSceneLoaded += HandleSceneLoaded;
    }

    // ★ 모든 IRecordable은 Awake에서 등록되고 NPC 데이터도 Awake에서 채워지므로, 첫 Start 시점이면 빠지는 덩어리가 없다
    private void Start()
    {
        _partStartSnapshot = RecordSystem.TakeSnapshot(SnapshotReason.PartStart);
        BeginFirstLoop();   // ★ 3-A — 첫 회차를 연다 (세이브가 생기면 4단계에서 불러온 이력으로 대신한다)
    }
    private void OnDestroy()
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.OnSceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(int sceneID)
    {
        // ★ 3-A — 회차 시작 스냅샷 (기록시스템_설계 6-1). 씬·위치·시각이 모두 새 회차 값이 된 순간
        if (_pendingLoopStartSnapshot && LoopHistory.Current != null)
        {
            LoopHistory.Current.startSnapshot = RecordSystem.TakeSnapshot(SnapshotReason.LoopStart);
            _pendingLoopStartSnapshot = false;
        }

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

        // ★ 설치 비용은 스냅샷을 찍은 뒤에 낸다 — 닻은 "내리기 직전"의 순간을 붙잡는다 (2026-10-09 결정).
        //   경로 2로 돌아오면 설치에 쓴 마나도 그 시점 값으로 돌아온다 (닻에 넣은 마력이 풀려나는 것)
        var snapshot = new TimeAnchorSnapshot
        {
            anchorId = ++_anchorSerial,                       // ★ 누적 번호
            sceneID = SceneLoader.Instance.CurrentSceneID,
            position = sora.transform.position,
            day = TimeManager.Instance.currentDay,
            hour = TimeManager.Instance.currentHour,
            // ★ 2-C-2 — 몸·세계 값(레벨, 공방민, 체력·마나, 호감도, 의심, 카운터, 기억)을 손으로 나열하던 필드를 걷어냈다.
            //   모두 아래 범용 스냅샷의 덩어리에 들어 있고, 복원도 그것을 쓴다
            loopCountAtSave = sora.loopCount,
            // ★ 3-A — 행적 로그 사본(actionLog)은 없앴다. 돌아오면 recordSnapshot.seq 앞까지의 흐름을 물려받는다
            recordSnapshot = RecordSystem.TakeSnapshot(SnapshotReason.Anchor),
        };
        sora.UseMana(setAnchorManaCost);   // ★ 스냅샷 뒤로 옮김 (기존: 스냅샷 전에 내서 31 → 11로 저장됐다)
        _anchors.Add(snapshot);

        if (anchorMarkerPrefab != null)
        {
            var markerObj = Instantiate(anchorMarkerPrefab, sora.transform.position, Quaternion.identity);
            markerObj.GetComponent<TimeAnchorMarker>().snapshotData = snapshot;
            _activeMarkers[snapshot] = markerObj; // ★ 리스트 대신 딕셔너리
        }

        PlayerActionLog.Instance?.Record(RecordType.AnchorSet, snapshot.anchorId.ToString(), 0, snapshot.anchorId,
            payload: PlayerActionLog.EncodeDayHour(snapshot.day, snapshot.hour));
        LoopHistory.RegisterAnchor(snapshot.anchorId, snapshot.day, snapshot.hour, snapshot.sceneID);   // ★ 3-A — 닻 이력 (사용 전)

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
        PlayerActionLog.Instance?.Record(RecordType.AnchorRetracted, snapshot.anchorId.ToString(), snapshot.anchorId, 0);
        LoopHistory.SetAnchorStatus(snapshot.anchorId, AnchorStatus.Retracted);   // ★ 3-A
    }

    // ★ 2-C — 닻 복원을 범용 스냅샷으로 교체했다 (기존: 매니저마다 옛 복원 함수를 손으로 호출).
    //   mask가 정하는 층위만 되돌린다 — 경로 1·자발적은 World, 경로 2는 World + Body.
    //   의지·플레이어 층위(기억, 개인친밀도, 영혼 레벨, 회차 수 등)는 mask에 없으므로 유지된다
    private void RestoreFromAnchor(TimeAnchorSnapshot anchor, RecordLayerMask mask)
    {
        if (anchor == null) return;

        if (anchor.recordSnapshot != null) RecordSystem.RestoreLayers(anchor.recordSnapshot, mask, MovedByLoadScene);   // ★ 2-C-2
        else Debug.LogError($"[TimeLoopManager] 닻 #{anchor.anchorId}에 범용 스냅샷이 없어 세계·몸을 되돌리지 못했습니다");

        // ★ 3-A — 행적 로그는 더 이상 되돌리지 않는다. 새 회차가 이 닻의 순번 앞까지의 흐름을 물려받는다 (BeginLoopFromAnchor)
    }

    // ---------------- ★ 3-A — 회차 열고 닫기 (기록시스템_설계 13-3-2) ----------------

    // 지금 회차를 닫는다. 회귀 절차의 맨 처음에 부른다 — 이 뒤에 생기는 기록(복귀 비용, 닻 사용, 회귀 기록)은
    // 모두 새 회차의 자기 구간에 들어간다. 결말 직전 스냅샷을 함께 남긴다
    public void EndCurrentLoop(LoopEndType endType)
    {
        LoopHistory.EndLoop(endType, RecordSystem.TakeSnapshot(SnapshotReason.BeforeEnding));
    }

    private void BeginFirstLoop()
    {
        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;
        var time = TimeManager.Instance;
        LoopHistory.BeginLoop(sora != null ? sora.loopCount : 0, parentLoop: -1, branchAnchorId: 0, branchSeq: 0,
            time != null ? time.currentDay : 1, time != null ? time.currentHour : 0, returnPath: -1);
        _pendingLoopStartSnapshot = true;
    }

    // 닻으로 돌아가는 회차. 부모 = 그 닻을 내린 회차, 물려받는 흐름 = 닻 스냅샷 순번 앞까지
    private void BeginLoopFromAnchor(TimeAnchorSnapshot anchor, int loopNumber, ReturnPath path)
    {
        var record = LoopHistory.FindAnchor(anchor.anchorId);
        int parent = record != null ? record.loopSet : anchor.loopCountAtSave;
        long branchSeq = anchor.recordSnapshot != null ? anchor.recordSnapshot.seq : 0;
        if (anchor.recordSnapshot == null)
            Debug.LogWarning($"[TimeLoopManager] 닻 #{anchor.anchorId}에 스냅샷이 없어 이번 흐름이 비어서 시작합니다");

        LoopHistory.BeginLoop(loopNumber, parent, anchor.anchorId, branchSeq, anchor.day, anchor.hour, (int)path);
        _pendingLoopStartSnapshot = true;
    }

    // ★ 3-A — 공식 하드 리셋 전용. 닻과 회차 이력을 모두 지우고 첫 회차를 새로 연다.
    //   예전 하드 리셋은 닻을 남겨 두었다 — 새 세계인데 지난 세계의 닻으로 돌아갈 수 있었다
    public void ResetForHardReset()
    {
        foreach (var marker in _activeMarkers.Values) if (marker != null) Destroy(marker);
        _activeMarkers.Clear();
        _anchors.Clear();
        _anchorSerial = 0;      // 새 세계이므로 닻 번호도 처음부터
        LoopHistory.Clear();
        BeginFirstLoop();
    }

    // ★ 2-C — 복원 직후 상태가 기준 스냅샷과 같은지 비교한다. 이제는 복원 자체가 범용 스냅샷을 쓰므로,
    //   불일치가 나오면 어떤 IRecordable의 ReadState가 값을 빠뜨렸다는 뜻이다 (자기 검증)
    //   label: 로그에 찍을 기준 이름 ("닻 #3", "파트 시작")
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void VerifyRecordSnapshot(string label, Snapshot expected, RecordLayerMask mask)
    {
        if (!verifyRecordSnapshot || expected == null) return;

        var now = RecordSystem.TakeSnapshot(SnapshotReason.Verify, mask);
        var diffs = RecordSystem.Compare(expected, now, mask, MovedByLoadScene);   // ★ 2-C-2 — 시각·씬은 아직 옮기기 전이라 비교에서 뺀다
        if (diffs.Count == 0)
            Debug.Log($"[RecordSystem] {label} 검증 일치 ({mask}, 덩어리 {now.blocks.Count}개)");
        else
            Debug.LogWarning($"[RecordSystem] {label} 검증 불일치 ({mask}, 덩어리 {now.blocks.Count}개) {diffs.Count}건\n- " + string.Join("\n- ", diffs)); // ★ 불일치 때도 덩어리 수 표시
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void VerifyRecordSnapshot(TimeAnchorSnapshot anchor, RecordLayerMask mask)
        => VerifyRecordSnapshot($"닻 #{anchor.anchorId}", anchor.recordSnapshot, mask);

    // ★ currentHealth에 직접 대입하면 변경 이벤트가 발생하지 않아 HUD가 갱신되지 않는다.
    //   Heal/RecoverMana를 거쳐야 슬라이더가 즉시 따라온다
    private void SetVitals(SoraStats sora, int health, int mana)
    {
        sora.currentHealth = 0;
        sora.currentMana = 0;
        sora.Heal(Mathf.Clamp(health, 1, sora.maxHealth));
        sora.RecoverMana(Mathf.Clamp(mana, 0, sora.maxMana));
    }

    // ★ 회귀 직후 체력·마나가 어떻게 바뀌었는지 콘솔에 한 줄로 남긴다 (테스트·밸런스 확인용, 에디터·개발 빌드만)
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogReturnVitals(string path, TimeAnchorSnapshot anchor, SoraStats sora, int healthBefore, int manaBefore, string detail)
    {
        Debug.Log($"[회귀] {path} → 닻 #{anchor.anchorId} | 체력 {healthBefore} → {sora.currentHealth}/{sora.maxHealth}, " +
                  $"마나 {manaBefore} → {sora.currentMana}/{sora.maxMana} | {detail}");
    }

    // ★ 경로 1 회복량. 목표 체력(최대 체력 × 비율)까지, 남은 마나가 허락하는 만큼만 회복한다.
    //   돌려주는 값은 회복 후 체력, manaSpent는 그 회복에 쓴 마나
    private int HealthFromMana(SoraStats sora, out int manaSpent)
    {
        manaSpent = 0;
        if (healthPerMana <= 0f) return sora.currentHealth;

        int target = Mathf.RoundToInt(sora.maxHealth * deathReturnHealTargetRatio);
        int need = Mathf.Max(0, target - sora.currentHealth);
        int affordable = Mathf.FloorToInt(sora.currentMana * healthPerMana);
        int heal = Mathf.Min(need, affordable);

        manaSpent = Mathf.Min(sora.currentMana, Mathf.CeilToInt(heal / healthPerMana));
        return sora.currentHealth + heal;
    }

    // ★ 사용한 닻을 소모하고, 과거로 갔다면 그보다 뒤의 닻도 정리한다 (기획서 7-5)
    private void ConsumeAnchor(TimeAnchorSnapshot used, ReturnPath path)
    {
        if (used == null) return;

        if (discardLaterAnchorsOnTravel)
        {
            int usedAt = ToAbsoluteHour(used.day, used.hour);
            for (int i = _anchors.Count - 1; i >= 0; i--)
            {
                if (_anchors[i] == used) continue;
                if (ToAbsoluteHour(_anchors[i].day, _anchors[i].hour) > usedAt)
                    DiscardAnchor(_anchors[i], RecordType.AnchorVanished, 0);
            }
        }

        if (consumeAnchorOnUse) DiscardAnchor(used, RecordType.AnchorUsed, (int)path);
    }

    // 마나 환급 없이 제거 (사용·소멸용). RemoveAnchor는 플레이어가 직접 거둘 때 쓴다
    // ★ 왜 사라졌는지 기록한다 — 이야기의 행적이 닻 아이콘 상태를 그릴 때 쓴다
    private void DiscardAnchor(TimeAnchorSnapshot snapshot, RecordType reason, int detail)
    {
        if (!_anchors.Remove(snapshot)) return;
        if (_activeMarkers.TryGetValue(snapshot, out var marker))
        {
            if (marker != null) Destroy(marker);
            _activeMarkers.Remove(snapshot);
        }
        PlayerActionLog.Instance?.Record(reason, snapshot.anchorId.ToString(), snapshot.anchorId, detail);

        // ★ 3-A — 닻 이력의 최종 상태. 사용한 닻은 "이 닻에서 갈라진 회차"(이미 연 새 회차)를 함께 적는다
        if (reason == RecordType.AnchorUsed)
            LoopHistory.SetAnchorStatus(snapshot.anchorId,
                detail == (int)ReturnPath.Forced ? AnchorStatus.ForcedUsed : AnchorStatus.Used,
                LoopHistory.Current != null ? LoopHistory.Current.loopNumber : -1);
        else
            LoopHistory.SetAnchorStatus(snapshot.anchorId, AnchorStatus.Vanished);
    }

    public bool TravelToAnchor(TimeAnchorSnapshot anchor)
    {
        if (anchor == null || !_anchors.Contains(anchor)) return false;

        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;
        if (sora == null) return false;                       // ★ null 체크 누락 수정

        if (sora.currentMana < returnManaCost)                // ★ 마나 비용이 적용되지 않던 문제 수정
        {
            NotifyReason(keyTravelMana, null);               // ★ 설치용 문구 → 이동용 문구
            return false;
        }

        int healthBefore = sora.currentHealth, manaBefore = sora.currentMana;   // ★ 회귀 로그용
        EndCurrentLoop(LoopEndType.Voluntary);                // ★ 3-A — 회차를 먼저 닫는다 (결말 직전 스냅샷은 비용을 내기 전)
        sora.UseMana(returnManaCost);
        sora.loopCount++;                                     // 시간 역행이므로 회차 증가
        BeginLoopFromAnchor(anchor, sora.loopCount, ReturnPath.Voluntary);   // ★ 3-A

        RestoreFromAnchor(anchor, RecordLayerMask.World);      // ★ 2-C — 자발적 회귀는 세계만 되돌린다
        VerifyRecordSnapshot(anchor, RecordLayerMask.World);
        LogReturnVitals("자발적", anchor, sora, healthBefore, manaBefore, $"복귀 비용 {returnManaCost}");   // ★
        ConsumeAnchor(anchor, ReturnPath.Voluntary);          // ★ 1회용 + 이후 닻 소멸

        // ★ 복원 뒤에 기록해야 한다. RestoreFromAnchor가 행적 로그를 그 시점으로 되돌리므로,
        //   먼저 기록하면 복원 과정에서 지워진다
        // ★ 문구를 조립하지 않고 숫자만 남긴다 (회차 → before, 닻 번호 → after, 시각 → payload)
        PlayerActionLog.Instance?.Record(RecordType.Loop, "return_to_anchor", anchor.loopCountAtSave, anchor.anchorId,
            payload: PlayerActionLog.EncodeDayHour(anchor.day, anchor.hour));

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
        EndCurrentLoop(LoopEndType.Death);   // ★ 3-A — 회차를 먼저 닫는다. 사인 키는 3-B에서 넘긴다
        sora.loopCount++;

        var latest = _anchors.Count > 0 ? _anchors[_anchors.Count - 1] : null;
        int healthBefore = sora.currentHealth, manaBefore = sora.currentMana;   // ★ 회귀 로그용

        if (latest != null && sora.currentMana >= returnManaCost)   // [경로 1] 정상 복귀
        {
            BeginLoopFromAnchor(latest, sora.loopCount, ReturnPath.Normal);   // ★ 3-A
            sora.UseMana(returnManaCost);                           // 중간 시점으로 되돌아가는 힘
            RestoreFromAnchor(latest, RecordLayerMask.World);      // ★ 2-C — 경로 1은 세계만 되돌린다
            VerifyRecordSnapshot(latest, RecordLayerMask.World);

            // ★ 몸은 유지되므로 죽은 몸을 남은 마나로 회복한다 (마나를 생명력으로 바꿔 쓴다, 기획서 7-2)
            int healedHealth = HealthFromMana(sora, out int manaSpent);
            SetVitals(sora, Mathf.Max(minHealthAfterReturn, healedHealth), sora.currentMana - manaSpent);
            LogReturnVitals("경로 1", latest, sora, healthBefore, manaBefore,   // ★ 마나를 어디에 얼마 썼는지
                $"복귀 비용 {returnManaCost} + 회복 {manaSpent} → 체력 {healedHealth}" +
                (healedHealth < minHealthAfterReturn ? $", 최소 보장 {minHealthAfterReturn} 적용" : ""));

            ConsumeAnchor(latest, ReturnPath.Normal);
            PlayerActionLog.Instance?.Record(RecordType.Loop, "death_return", latest.loopCountAtSave, latest.anchorId,
                payload: PlayerActionLog.EncodeDayHour(latest.day, latest.hour));
            PlayerActionLog.Instance?.RecordVitals();   // ★ 회귀 직후의 체력·마나
            LoadScene(latest.sceneID, latest.position, latest.day, latest.hour);
        }
        else if (latest != null)                                    // [경로 2] 마나 부족 강제 복귀
        {
            // ★ 기억은 되돌리지 않는다 (기존에는 RestoreAcquired로 기억까지 되돌렸다)
            // ★ 2-C — 경로 2는 세계와 몸을 닻 시점으로. 몸 덩어리에 레벨·공방민·피로도·체력·마나가 모두 들어 있다
            //   (기존: RestoreWorldState + RestoreBody + SetVitals를 따로 호출, 피로도는 빠져 있었다)
            BeginLoopFromAnchor(latest, sora.loopCount, ReturnPath.Forced);   // ★ 3-A
            RestoreFromAnchor(latest, RecordLayerMask.World | RecordLayerMask.Body);
            VerifyRecordSnapshot(latest, RecordLayerMask.World | RecordLayerMask.Body);
            LogReturnVitals("경로 2", latest, sora, healthBefore, manaBefore,
                $"닻 시점 값으로 (레벨 {sora.level}), 정신력 -{forcedReturnMentalPenalty}");   // ★ 2-C-2 — 복원된 몸에서 읽는다 (닻의 level 필드 제거)
            sora.LoseMental(forcedReturnMentalPenalty);

            ConsumeAnchor(latest, ReturnPath.Forced);
            PlayerActionLog.Instance?.Record(RecordType.Loop, "death_return", latest.loopCountAtSave, latest.anchorId,
                payload: PlayerActionLog.EncodeDayHour(latest.day, latest.hour));
            PlayerActionLog.Instance?.RecordVitals();   // ★ 회귀 직후의 체력·마나
            LoadScene(latest.sceneID, latest.position, latest.day, latest.hour);
        }
        else                                                        // [경로 3] 닻 없음 — Day 1부터
        {
            StartNewLoopFromDay1(sora, null);   // ★ 2-B — 디버그 "다음 회차로"와 같은 절차를 쓰도록 함수로 뺐다
        }
    }

    // ★ 2-B — 경로 3의 "Day 1부터 새 회차" 절차. 내용은 기존 경로 3 그대로다.
    //   디버그 도구도 이 함수를 불러 실제 규칙과 어긋나지 않게 한다. 회차 증가는 부르는 쪽에서 한다
    //   ★ 3-A — 회차를 닫는 것(EndCurrentLoop)도 부르는 쪽에서 한다. 여기서는 새 회차를 연다
    //   source: 기록의 출처 (실제 게임은 null, 디버그 도구는 RecordKeys.DebugSource)
    public void StartNewLoopFromDay1(SoraStats sora, string source)
    {
        if (sora == null) return;

        // ★ 3-A — Day 1 회차는 부모 흐름을 물려받지 않는다. 부모는 직전 회차 (그래프에서 어디서 넘어왔는지 잇는 용도)
        var previous = LoopHistory.Current;
        LoopHistory.BeginLoop(sora.loopCount, previous != null ? previous.loopNumber : -1, branchAnchorId: 0, branchSeq: 0,
            branchDay: 1, branchHour: 0, returnPath: (int)ReturnPath.Day1);
        _pendingLoopStartSnapshot = true;

        // ★ Day 1은 모든 닻보다 과거다 — "과거로 가면 뒤의 닻은 사라진다"(기획서 7-5).
        //   실제 경로 3은 닻이 없을 때만 오므로 여기서 지워지는 닻은 디버그로 왔을 때뿐이다
        for (int i = _anchors.Count - 1; i >= 0; i--)
            DiscardAnchor(_anchors[i], RecordType.AnchorVanished, 0);

        // ★ 2-C — 세계와 몸을 파트 시작 스냅샷으로 되돌린다 (설계 4장).
        //   기존에는 시스템마다 초기화 함수를 따로 불러, 하나를 빠뜨리면 그 값만 남았다(피로도가 그랬다).
        //   의지·플레이어 층위(기억, 영혼 레벨, 회차 수 등)는 mask에 없으므로 유지된다
        const RecordLayerMask partStartMask = RecordLayerMask.World | RecordLayerMask.Body;
        if (_partStartSnapshot != null)
        {
            RecordSystem.RestoreLayers(_partStartSnapshot, partStartMask, MovedByLoadScene);   // ★ 2-C-2 — 시각·씬은 아래 ResetToDay1·LoadScene
            VerifyRecordSnapshot("파트 시작", _partStartSnapshot, partStartMask);
        }
        else
        {
            // 스냅샷이 없을 때만(시작 전에 회귀가 불린 경우 등) 옛 초기화로 대신한다
            Debug.LogWarning("[TimeLoopManager] 파트 시작 스냅샷이 없어 옛 초기화로 대신합니다");
            MemoryManager.Instance.ClearAllCounters();
            NPCManager.Instance.ResetAffectionForNewLoop();
            sora.ResetBodyForNewLoop();                         // 영혼 레벨은 유지
        }

        // ★ 3-A — 행적을 지우지 않는다(ClearAll 제거). 새 회차가 물려받는 것이 없으므로 이번 흐름은 여기서부터 시작한다
        PlayerActionLog.Instance.Record(RecordType.Loop, "full_reset", source: source);
        PlayerActionLog.Instance?.RecordVitals();   // ★ 회귀 직후의 체력·마나

        var cfg = SceneLoader.Instance.startConfig;
        TimeManager.Instance.ResetToDay1();
        LoadScene(cfg.startSceneID, cfg.startPosition, 1, 0);
    }

    private void LoadScene(int sceneID, Vector2 pos, int day, int hour)
    {
        TravelTimeTracker.Instance?.CancelJourney();   // ★ 회귀는 걸어서 온 게 아니다 — 이동 시간이 차감되던 문제
        TimeManager.Instance.SetTime(day, hour);
        SceneLoader.Instance.LoadScene(sceneID, pos);
    }
}