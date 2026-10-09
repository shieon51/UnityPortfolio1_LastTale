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
    [Tooltip("몸이 되돌아가는 회귀(경로 2, Day 1)의 정신력 충격 수식 (기획서 7-2). Resources/SO/DamageFormula의 Mental Shock Formula 애셋")]
    public MentalShockFormula mentalShockFormula;   // ★ 정신력 충격 Formula
    [Tooltip("정신력 충격 수식을 연결하지 않았을 때 경로 2·Day 1에서 깎이는 정신력")]
    public int forcedReturnMentalPenalty = 10;      // ★ 이제 수식이 없을 때의 대체값 (예전: 경로 2 고정값)
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

    [Header("사인 — 이야기의 행적 라벨 (기록에는 키만 남는다)")]
    [Tooltip("보스전이 아닌 사망(몬스터 피격 등)의 사인 문구 키")]
    public string deathCauseDefaultKey = "death_cause_monster";   // ★ 3-B
    [Tooltip("보스전 패배의 사인 문구 키. {0}에 보스의 (지금 아는) 이름이 들어간다")]
    public string deathCauseBattleKey = "death_cause_battle";     // ★ 3-B

    [Header("이야기의 행적 요약 — 자동 마일스톤 규칙")]
    public LoopSummaryRules summaryRules = new();                  // ★ 3-C

    [Header("돌아갈 지점 선택 (개발용)")]
    [Tooltip("켜면 사망 뒤 자동으로 고르지 않고, 오버뷰 창(전체 상태 관리)에 뜨는 선택지 버튼을 기다린다. " +
             "닻 선택 화면이 생기기 전 테스트용. 에디터에서만 동작")]
    public bool waitForReturnChoiceInEditor = false;               // ★ 3-B

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
    // ★ 3-B — 결말 제목·사인 키와 사인의 대상(보스 NPC 키)을 함께 남긴다
    public void EndCurrentLoop(LoopEndType endType, string endingTitleKey = null, string deathCauseKey = null, string deathCauseSource = null)
    {
        LoopHistory.EndLoop(endType, RecordSystem.TakeSnapshot(SnapshotReason.BeforeEnding), endingTitleKey, deathCauseKey, deathCauseSource);

        // ★ 3-C — 닫힌 회차의 요약을 계산해 둔다 (자기 구간이 확정된 뒤)
        var closed = LoopHistory.Current;
        if (closed != null && !closed.IsOpen && PlayerActionLog.Instance != null)
            closed.summary = LoopSummaryBuilder.Build(closed, PlayerActionLog.Instance.Records, summaryRules);
    }

    // ★ 3-C — 회차 요약. 닫힌 회차는 계산해 둔 것을, 진행 중인 회차는 지금 상태로 계산한다 (오버뷰 창·이야기의 행적용)
    public LoopSummary GetSummary(LoopRecord loop)
    {
        if (loop == null) return null;
        if (!loop.IsOpen && loop.summary != null) return loop.summary;
        var nowBody = loop.IsOpen ? RecordSystem.TakeSnapshot(SnapshotReason.Verify, RecordLayerMask.Body) : null;
        return LoopSummaryBuilder.Build(loop, PlayerActionLog.Instance != null ? PlayerActionLog.Instance.Records : null, summaryRules, nowBody);
    }

    // ---------------- ★ 3-C — 결말 태그 (#ending:<종류>:<제목 키>) ----------------
    // 태그의 종류 글자. ink에 쓰는 데이터 형식이므로 바꾸지 않는다
    public const string EndingTagDeath = "death";             // 스토리 사망
    public const string EndingTagIncomplete = "incomplete";   // 불완전 결말
    public const string EndingTagFinal = "final";             // 결말 (1부 완결)

    // DialogueManager가 #ending 태그가 붙은 대화를 마칠 때 부른다
    public void ReachEnding(string endingTag, string titleKey, string source = null)
    {
        LoopEndType type = endingTag switch
        {
            EndingTagDeath => LoopEndType.Death,
            EndingTagIncomplete => LoopEndType.Incomplete,
            EndingTagFinal => LoopEndType.Ending,
            _ => LoopEndType.None,
        };
        if (type == LoopEndType.None)
        {
            Debug.LogError($"[TimeLoopManager] 알 수 없는 결말 종류 '{endingTag}' — {EndingTagDeath} / {EndingTagIncomplete} / {EndingTagFinal} 중 하나");
            return;
        }
        if (IsAwaitingReturnChoice)   // 이미 회차가 닫혔다 — 결말 기록이 다음 회차로 새지 않게
        {
            Debug.LogWarning($"[TimeLoopManager] 돌아갈 지점을 고르는 중이라 결말 '{titleKey}'을 무시합니다");
            return;
        }

        // 회차를 닫기 전에 남겨야 이 회차의 자기 구간에 들어간다
        PlayerActionLog.Instance?.Record(RecordType.EndingReached, titleKey, 0, (int)type, source: source);

        if (type == LoopEndType.Ending)   // 1부 완결 — 돌아갈 지점이 없다
        {
            EndCurrentLoop(LoopEndType.Ending, titleKey);
            // 4단계: 자동 저장. 결말 이후 흐름(타이틀, 2부 열림, 시뮬레이션)은 아직 없다 (기획서 6-12, 8장)
            Debug.LogWarning("[TimeLoopManager] 1부 결말에 도달했습니다 — 결말 이후 흐름은 아직 구현되지 않았습니다");
            return;
        }
        FinishLoop(type, titleKey, null, null);   // 스토리 사망·불완전 결말 → 돌아갈 지점 선택 (3-B 흐름)
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
    // ★ 3-B — 소라를 직접 읽지 않고 값을 받는다. 닻 선택지의 예상 값(미리보기)과 실제 적용이 같은 계산을 쓰게 하려고
    private int HealthFromMana(int health, int maxHealth, int mana, out int manaSpent)
    {
        manaSpent = 0;
        if (healthPerMana <= 0f) return health;

        int target = Mathf.RoundToInt(maxHealth * deathReturnHealTargetRatio);
        int need = Mathf.Max(0, target - health);
        int affordable = Mathf.FloorToInt(mana * healthPerMana);
        int heal = Mathf.Min(need, affordable);

        manaSpent = Mathf.Min(mana, Mathf.CeilToInt(heal / healthPerMana));
        return health + heal;
    }

    // ★ 3-B — 경로 1로 돌아간 직후의 체력·마나. 복귀 비용을 낸 뒤 남은 마나로 회복한다 (기획서 7-2)
    private void NormalReturnVitals(SoraStats sora, out int health, out int mana, out int healedHealth, out int manaSpent)
    {
        int manaLeft = Mathf.Max(0, sora.currentMana - returnManaCost);
        healedHealth = HealthFromMana(sora.currentHealth, sora.maxHealth, manaLeft, out manaSpent);
        health = Mathf.Clamp(Mathf.Max(minHealthAfterReturn, healedHealth), 1, sora.maxHealth);
        mana = manaLeft - manaSpent;
    }

    // ★ 3-B — 경로별 정신력 하락. 선택지 표시와 실제 적용이 모두 이 함수를 쓴다.
    // ★ 정신력 충격 — 몸이 되돌아가는 회귀(경로 2, Day 1)만 깎는다 (기획서 7-2).
    //   예전: 경로 2는 고정값 10, Day 1은 0(미구현)이었다. 이제 둘 다 몸이 되돌아간 레벨 폭에 비례한다
    private int MentalLossFor(ReturnPath path, int levelBefore, int levelAfter)
    {
        if (path != ReturnPath.Forced && path != ReturnPath.Day1) return 0;   // 경로 1·자발적은 몸을 유지한다
        return mentalShockFormula != null
            ? mentalShockFormula.Calculate(levelBefore, levelAfter)
            : forcedReturnMentalPenalty;                                     // 수식 애셋을 연결하지 않았을 때
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

    // ---------------- ★ 3-B — 회차 마감 흐름 (기록시스템_설계 8장) ----------------
    //   사망·불완전 결말 → 회차 마감 → 선택지 계산 → 돌아갈 지점 선택 → 적용.
    //   선택은 닻 선택 화면이 한다. 화면이 없으면(지금) 예전처럼 최근 닻, 없으면 Day 1을 자동으로 고른다

    // 닻 선택 화면이 구독한다. 받은 선택지 중 하나로 ChooseReturn을 부르면 된다
    public event System.Action<IReadOnlyList<ReturnOption>> OnReturnChoiceRequested;

    private List<ReturnOption> _pendingReturnOptions;   // 고르기를 기다리는 선택지. 없으면 null
    public IReadOnlyList<ReturnOption> PendingReturnOptions => _pendingReturnOptions;
    public bool IsAwaitingReturnChoice => _pendingReturnOptions != null;

    // 기획서 7-2: 기억은 어떤 경로에서도 유지된다
    //   경로 1 (마나 충분)   — 마나 소모, 몸 유지
    //   경로 2 (마나 부족)   — 몸이 닻 시점으로, 정신력 하락
    //   경로 3 (Day 1)       — Day 1부터, 몸은 시작 상태로 (닻이 있어도 고를 수 있다)
    // ★ 3-B — 사인 키를 받는다. 비우면 일반 사망(deathCauseDefaultKey). 보스전 패배는 NPCManager가 보스 키와 함께 넘긴다
    public void HandleDeath(string deathCauseKey = null, string deathCauseSource = null)
        => FinishLoop(LoopEndType.Death, null, string.IsNullOrEmpty(deathCauseKey) ? deathCauseDefaultKey : deathCauseKey, deathCauseSource);

    // ★ 3-B — 회차를 마감하고 돌아갈 지점을 묻는다. 사망과 불완전 결말(3-C)이 함께 쓴다
    public void FinishLoop(LoopEndType endType, string endingTitleKey, string deathCauseKey, string deathCauseSource)
    {
        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;
        if (sora == null) return;
        if (IsAwaitingReturnChoice)   // 사망 처리가 두 번 들어와도(Die와 패배 흐름 등) 회차가 두 번 닫히지 않게
        {
            Debug.LogWarning("[TimeLoopManager] 이미 돌아갈 지점을 고르는 중입니다 — 회차 마감 요청을 무시합니다");
            return;
        }

        EndCurrentLoop(endType, endingTitleKey, deathCauseKey, deathCauseSource);   // 회차를 먼저 닫는다 (3-A)
        // 4단계: 여기서 자기 구간 파일을 쓰고 자동 저장한다 — 선택 화면에서 게임을 꺼도 회차의 결과는 남는다 (설계 8장)

        _pendingReturnOptions = BuildReturnOptions(sora);

        bool waitInEditor = Application.isEditor && waitForReturnChoiceInEditor;
        if (OnReturnChoiceRequested != null || waitInEditor)
        {
            if (waitInEditor) Debug.Log("[TimeLoopManager] 돌아갈 지점 선택 대기 — 오버뷰 창(전체 상태 관리) 맨 위에서 고른다");
            OnReturnChoiceRequested?.Invoke(_pendingReturnOptions);
            return;
        }
        ChooseReturn(DefaultReturnOption(_pendingReturnOptions));   // 화면이 없으면 예전 동작
    }

    // 선택지: 남은 닻 전부(마나에 따라 경로 1 또는 2) + Day 1. 닻은 설치 순(= 시간순)
    private List<ReturnOption> BuildReturnOptions(SoraStats sora)
    {
        var options = new List<ReturnOption>();
        foreach (var anchor in _anchors) options.Add(BuildAnchorOption(sora, anchor));
        options.Add(BuildDay1Option(sora));
        return options;
    }

    private ReturnOption BuildAnchorOption(SoraStats sora, TimeAnchorSnapshot anchor)
    {
        bool enoughMana = sora.currentMana >= returnManaCost;
        var o = new ReturnOption
        {
            anchor = anchor,
            path = enoughMana ? ReturnPath.Normal : ReturnPath.Forced,
            levelBefore = sora.level,
            anchorsLost = CountAnchorsAfter(anchor),
        };

        if (enoughMana)   // 경로 1 — 몸을 유지하고 남은 마나로 회복
        {
            o.manaCost = returnManaCost;
            o.keepsBody = true;
            o.levelAfter = sora.level;
            o.maxHealthAfter = sora.maxHealth;
            o.maxManaAfter = sora.maxMana;
            NormalReturnVitals(sora, out o.healthAfter, out o.manaAfter, out _, out _);
        }
        else              // 경로 2 — 몸이 닻 시점의 값으로 (닻의 몸 덩어리에서 읽는다)
        {
            ReadBody(anchor.recordSnapshot, sora, out o.levelAfter, out o.healthAfter, out o.maxHealthAfter, out o.manaAfter, out o.maxManaAfter);
        }
        o.mentalLoss = MentalLossFor(o.path, o.levelBefore, o.levelAfter);
        return o;
    }

    private ReturnOption BuildDay1Option(SoraStats sora)
    {
        var o = new ReturnOption
        {
            anchor = null,
            path = ReturnPath.Day1,
            levelBefore = sora.level,
            anchorsLost = _anchors.Count,   // Day 1은 모든 닻보다 과거 — 남은 닻이 모두 사라진다
        };
        ReadBody(_partStartSnapshot, sora, out o.levelAfter, out o.healthAfter, out o.maxHealthAfter, out o.manaAfter, out o.maxManaAfter);
        o.mentalLoss = MentalLossFor(o.path, o.levelBefore, o.levelAfter);
        return o;
    }

    // 스냅샷의 몸 덩어리에서 레벨·체력·마나를 읽는다. 없으면 지금 값 (스냅샷이 없을 때의 대비)
    private static void ReadBody(Snapshot snapshot, SoraStats sora, out int level, out int health, out int maxHealth, out int mana, out int maxMana)
    {
        StateBlock b = null;
        snapshot?.blocks.TryGetValue(RecordIds.SoraBody, out b);
        int Get(string key, int fallback) => b != null && b.ints.TryGetValue(key, out int v) ? v : fallback;

        level = Get(SoraStats.StateKeyLevel, sora.level);
        maxHealth = Get(SoraStats.StateKeyMaxHealth, sora.maxHealth);
        maxMana = Get(SoraStats.StateKeyMaxMana, sora.maxMana);
        health = Get(SoraStats.StateKeyHealth, maxHealth);
        mana = Get(SoraStats.StateKeyMana, maxMana);
    }

    // 이 닻보다 뒤에 내린 닻 수 — 그 닻으로 돌아가면 사라진다 (ConsumeAnchor와 같은 기준)
    private int CountAnchorsAfter(TimeAnchorSnapshot anchor)
    {
        if (!discardLaterAnchorsOnTravel) return 0;
        int at = ToAbsoluteHour(anchor.day, anchor.hour), count = 0;
        foreach (var a in _anchors)
            if (a != anchor && ToAbsoluteHour(a.day, a.hour) > at) count++;
        return count;
    }

    // 화면이 없을 때의 선택 — 예전 동작과 같다 (최근 닻, 없으면 Day 1)
    private static ReturnOption DefaultReturnOption(List<ReturnOption> options)
    {
        for (int i = options.Count - 1; i >= 0; i--)
            if (!options[i].IsDay1) return options[i];
        return options[options.Count - 1];
    }

    // 닻 선택 화면(또는 오버뷰 창)이 고른 선택지를 적용한다. 받은 목록에 없는 선택지는 거절한다
    public bool ChooseReturn(ReturnOption option)
    {
        if (_pendingReturnOptions == null || option == null || !_pendingReturnOptions.Contains(option))
        {
            Debug.LogWarning("[TimeLoopManager] 기다리는 선택지가 아니라서 적용하지 않습니다");
            return false;
        }
        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;
        if (sora == null) return false;
        if (option.anchor != null && !_anchors.Contains(option.anchor))
        {
            Debug.LogWarning($"[TimeLoopManager] 닻 #{option.anchor.anchorId}이 이미 없습니다");
            return false;
        }

        _pendingReturnOptions = null;
        ApplyReturn(sora, option);
        // 4단계: 새 회차 시작 스냅샷과 함께 자동 저장
        return true;
    }

    // ★ 3-B — 고른 선택지를 적용한다. 경로 1·2·3의 본문은 예전 HandleDeath 그대로이고, "최근 닻" 대신 고른 닻을 쓴다
    private void ApplyReturn(SoraStats sora, ReturnOption option)
    {
        sora.loopCount++;   // 회차 수는 고른 뒤에 오른다 (설계 8장)
        var anchor = option.anchor;
        int healthBefore = sora.currentHealth, manaBefore = sora.currentMana;   // ★ 회귀 로그용

        if (option.path == ReturnPath.Normal)                       // [경로 1] 정상 복귀
        {
            BeginLoopFromAnchor(anchor, sora.loopCount, ReturnPath.Normal);   // ★ 3-A
            // ★ 몸은 유지되므로 죽은 몸을 남은 마나로 회복한다 (마나를 생명력으로 바꿔 쓴다, 기획서 7-2).
            //   선택지에 보여준 예상 값과 같은 계산 (3-B)
            NormalReturnVitals(sora, out int health, out int mana, out int healedHealth, out int manaSpent);
            sora.UseMana(returnManaCost);                           // 중간 시점으로 되돌아가는 힘
            RestoreFromAnchor(anchor, RecordLayerMask.World);      // ★ 2-C — 경로 1은 세계만 되돌린다
            VerifyRecordSnapshot(anchor, RecordLayerMask.World);

            SetVitals(sora, health, mana);
            LogReturnVitals("경로 1", anchor, sora, healthBefore, manaBefore,   // ★ 마나를 어디에 얼마 썼는지
                $"복귀 비용 {returnManaCost} + 회복 {manaSpent} → 체력 {healedHealth}" +
                (healedHealth < minHealthAfterReturn ? $", 최소 보장 {minHealthAfterReturn} 적용" : ""));

            ConsumeAnchor(anchor, ReturnPath.Normal);
            PlayerActionLog.Instance?.Record(RecordType.Loop, "death_return", anchor.loopCountAtSave, anchor.anchorId,
                payload: PlayerActionLog.EncodeDayHour(anchor.day, anchor.hour));
            PlayerActionLog.Instance?.RecordVitals();   // ★ 회귀 직후의 체력·마나
            LoadScene(anchor.sceneID, anchor.position, anchor.day, anchor.hour);
        }
        else if (option.path == ReturnPath.Forced)                  // [경로 2] 마나 부족 강제 복귀
        {
            // ★ 기억은 되돌리지 않는다 (기존에는 RestoreAcquired로 기억까지 되돌렸다)
            // ★ 2-C — 경로 2는 세계와 몸을 닻 시점으로. 몸 덩어리에 레벨·공방민·피로도·체력·마나가 모두 들어 있다
            //   (기존: RestoreWorldState + RestoreBody + SetVitals를 따로 호출, 피로도는 빠져 있었다)
            BeginLoopFromAnchor(anchor, sora.loopCount, ReturnPath.Forced);   // ★ 3-A
            RestoreFromAnchor(anchor, RecordLayerMask.World | RecordLayerMask.Body);
            VerifyRecordSnapshot(anchor, RecordLayerMask.World | RecordLayerMask.Body);
            LogReturnVitals("경로 2", anchor, sora, healthBefore, manaBefore,
                $"닻 시점 값으로 (레벨 {option.levelBefore} → {sora.level}), 정신력 -{option.mentalLoss}");   // ★ 3-B — 선택지에 보여준 값, 레벨 폭도 함께
            sora.LoseMental(option.mentalLoss);

            ConsumeAnchor(anchor, ReturnPath.Forced);
            PlayerActionLog.Instance?.Record(RecordType.Loop, "death_return", anchor.loopCountAtSave, anchor.anchorId,
                payload: PlayerActionLog.EncodeDayHour(anchor.day, anchor.hour));
            PlayerActionLog.Instance?.RecordVitals();   // ★ 회귀 직후의 체력·마나
            LoadScene(anchor.sceneID, anchor.position, anchor.day, anchor.hour);
        }
        else                                                        // [경로 3] Day 1부터 — 닻이 있어도 고를 수 있다 (3-B)
        {
            StartNewLoopFromDay1(sora, null);   // ★ 2-B — 디버그 "다음 회차로"와 같은 절차를 쓰도록 함수로 뺐다
            // ★ 정신력 충격 — Day 1도 몸이 되돌아가므로 깎인다 (예전: 미구현, 0). 정신력은 의지 층위라 위 복원과 겹치지 않는다
            if (option.mentalLoss > 0) sora.LoseMental(option.mentalLoss);
            LogDay1Return(sora, option, healthBefore, manaBefore);
        }
    }

    // ★ Day 1 회귀도 경로 1·2처럼 콘솔에 한 줄 남긴다 (테스트·밸런스 확인용, 에디터·개발 빌드만)
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogDay1Return(SoraStats sora, ReturnOption option, int healthBefore, int manaBefore)
    {
        Debug.Log($"[회귀] Day 1 | 체력 {healthBefore} → {sora.currentHealth}/{sora.maxHealth}, 마나 {manaBefore} → {sora.currentMana}/{sora.maxMana} | " +
                  $"몸 레벨 {option.levelBefore} → {sora.level}, 정신력 -{option.mentalLoss}, 사라진 닻 {option.anchorsLost}개");
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