using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EventManager;

public class NPCManager : Singleton<NPCManager>
{
    [Header("NPC 정의")]
    [Tooltip("Resources 하위 폴더 — 이 안의 모든 NPCDefinition을 자동으로 읽어옴")]
    public string npcDefinitionFolder = "NPCDefinitions";

    // NPCManager.cs 에 추가 (private dict를 안전하게 읽기 전용으로 노출)
    public IReadOnlyDictionary<string, NPCData> AllNPCData => npcDataDict;

    // -------------------------------------------------------------------------------------------
    private NPC _activeBossBattle;

    private string _pendingWinNode, _pendingLoseNode;

    [Header("Battle End")]
    [Tooltip("전투가 끝난 뒤 결과 대화가 뜨기까지의 짧은 정지 시간(초)")]
    public float resultDialogueDelay = 1f;

    [Header("Ground Snap")]
    [Tooltip("NPC가 자동으로 안착할 바닥으로 인정할 레이어들 (Ground + OneWayPlatform 둘 다 체크)")]
    public LayerMask groundSnapLayer;

    // NPC 이름을 Key로 하여 데이터를 영구 보관하는 딕셔너리
    private Dictionary<string, NPCData> npcDataDict = new Dictionary<string, NPCData>();

    // 씬 내에서 껐다 켜기 위한 껍데기(프리팹) 보관소
    private Dictionary<string, GameObject> npcPool = new Dictionary<string, GameObject>();

    // 이벤트 실행 시 연출용 소환/퇴장 관련
    private HashSet<string> _summonedForEvent = new();

    private void Awake()
    {
        // 나중에는 여기서 Save 파일 데이터를 불러와서 npcDataDict에 덮어씌울 것.
        // 현재 세이브 기능이 없으니 임시로 초기 데이터 세팅
        InitializeDefaultNPCData();
    }

    private void InitializeDefaultNPCData()
    {
        var definitions = Resources.LoadAll<NPCDefinition>(npcDefinitionFolder);
        foreach (var def in definitions)
        {
            if (string.IsNullOrEmpty(def.npcName)) continue;
            if (npcDataDict.ContainsKey(def.npcName))
            {
                Debug.LogError($"[NPCManager] NPC 이름 중복: '{def.npcName}' — '{def.name}' 애셋 확인 필요");
                continue;
            }
            npcDataDict[def.npcName] = CreateFromDefinition(def);
        }
        Debug.Log($"[NPCManager] NPC 정의 {npcDataDict.Count}개 로드 완료");
    }

    private NPCData CreateFromDefinition(NPCDefinition def)
    {
        var data = new NPCData(def.npcName);
        data.hiddenAffection = def.initialAffection;
        data.rememberAcrossLoops = def.rememberAcrossLoops;
        data.maxObtainableUnderstanding = def.maxObtainableUnderstanding;
        data.observableCounterKeys = def.observableCounterKeys;
        data.relatedNPCs = def.relatedNPCs;
        data.trustInRelatedNPCs = def.trustInRelatedNPCs;
        data.suspicionSensitivity = def.suspicionSensitivity;
        data.trustThresholdForSora = def.trustThresholdForSora;
        return data;
    }

    // NPC가 스폰될 때 자신의 데이터를 요구하는 함수
    public NPCData GetNPCData(string npcName)
    {
        if (npcDataDict.TryGetValue(npcName, out NPCData data))
        {
            return data;
        }
        else
        {
            Debug.LogWarning($"[NPCManager] {npcName}의 데이터가 없습니다! 새로 생성합니다.");
            NPCData newData = new NPCData(npcName);
            npcDataDict.Add(npcName, newData);
            return newData;
        }
    }

    // NPC 호감도나 상태가 변했을 때 저장하는 함수 (나중에 호감도 이벤트 시 호출)
    public void SaveNPCData(NPCData updatedData)
    {
        if (npcDataDict.ContainsKey(updatedData.npcName))
        {
            npcDataDict[updatedData.npcName] = updatedData;
        }
    }

    // =========================================================
    // EventManager에게서 위임받은 NPC 스폰 & 배치 기능 (필요한 애들만 업데이트)
    // =========================================================
    public void UpdateNPCsOnMap(List<EventData> activeNPCEvents)
    {
        // 1. 이번 타임에 활성화되어야 할 NPC 이름 목록 수집
        HashSet<string> activeNames = new HashSet<string>();
        foreach (var data in activeNPCEvents) activeNames.Add(data.EventName);

        // 2. 이번 타임에 없는 NPC만 풀에서 꺼버림 (살아남을 애들은 안 건드림)
        foreach (var kvp in npcPool)
        {
            if (!activeNames.Contains(kvp.Key) && kvp.Value != null)
            {
                kvp.Value.SetActive(false);
            }
        }

        // 3. 켜야 할 애들 스폰 및 업데이트 진행
        foreach (EventData data in activeNPCEvents)
        {
            SpawnOrUpdateNPC(data);
        }
    }

    private void SpawnOrUpdateNPC(EventData data)
    {
        GameObject npcObj = null;
        bool isAlreadyActive = false; // 현재 켜져 있는지 확인용

        if (npcPool.TryGetValue(data.EventName, out npcObj) && npcObj != null)
        {
            isAlreadyActive = npcObj.activeSelf;
        }

        if (npcObj == null)
        {
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/NPC/{data.EventName}");
            if (prefab == null) return;
            npcObj = Instantiate(prefab);
            npcPool[data.EventName] = npcObj;
        }

        NPC npcScript = npcObj.GetComponent<NPC>();

        // -----------------------------------------------------
        // 스마트 연속 이벤트 처리:
        // 이미 씬에 켜져 있고 위치가 동일하면, 
        // Transform을 건드리지 않고 대사만 주입하기
        // -----------------------------------------------------
        if (isAlreadyActive && npcScript != null && Vector2.Distance(npcScript.originalCsvPos, data.Position) < 0.1f)
        {
            npcScript.SetupCurrentEvent(data);
            return;
        }

        // 이하 위치 갱신 및 바닥 스냅 로직 (장소가 바뀌었거나 첫 스폰일 때만)
        npcObj.transform.position = data.Position;
        npcObj.SetActive(true);

        if (npcScript != null)
        {
            npcScript.originalCsvPos = data.Position;
            npcScript.SetupCurrentEvent(data);
        }

        npcObj.transform.position = ComputeSnappedPosition(npcObj, data.Position); // 바닥 자동 안착 기능
    }

    // 기존 SpawnOrUpdateNPC()의 '바닥 자동 안착 기능' 블록과
    // TriggerBossBattle()의 위치 보정 블록을 아래 헬퍼 하나로 교체
    private Vector3 ComputeSnappedPosition(GameObject npcObj, Vector2 desiredPos, float startOffset = 1f, float rayDistance = 3f)
    {
        Vector2 rayStart = desiredPos + Vector2.up * startOffset;
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, rayDistance, groundSnapLayer);
        Debug.DrawRay(rayStart, Vector2.down * rayDistance, Color.magenta, 5f);

        if (hit.collider != null)
        {
            Collider2D col = npcObj.GetComponentInChildren<Collider2D>();
            if (col != null)
            {
                Physics2D.SyncTransforms();
                float pivotToBottom = npcObj.transform.position.y - col.bounds.min.y;
                return new Vector3(desiredPos.x, hit.point.y + pivotToBottom, 0);
            }
        }
        return new Vector3(desiredPos.x, desiredPos.y, 0);
    }

    // EventManager에서 호출할 대화 연결 함수
    public void StartNPCDialogue(string npcName)
    {
        if (npcPool.TryGetValue(npcName, out GameObject npcObj) && npcObj != null)
        {
            NPC npcScript = npcObj.GetComponent<NPC>();
            if (npcScript != null) npcScript.OnDialogueStart();
        }
    }

    public void EndNPCDialogue(string npcName)
    {
        if (npcPool.TryGetValue(npcName, out GameObject npcObj) && npcObj != null)
        {
            NPC npcScript = npcObj.GetComponent<NPC>();
            if (npcScript != null) npcScript.OnDialogueEnd();
        }
    }

    // 나중에 보스전 진입 시 처리 (다이얼로그 매니저에서 호출)
    public void TriggerBossBattle(string targetNpcName, BossDifficultyTier difficulty = BossDifficultyTier.Training, string winNode = null, string loseNode = null)
    {
        if (_activeBossBattle != null) // ★ 중복 트리거 방지 (원래 없던 안전장치)
        {
            Debug.LogWarning($"[NPCManager] 이미 {_activeBossBattle.npcName}과 전투 중이라 {targetNpcName} 전투 시작을 무시합니다.");
            return;
        }

        _pendingWinNode = !string.IsNullOrEmpty(winNode) ? winNode : $"{targetNpcName}_Battle_Win";
        _pendingLoseNode = !string.IsNullOrEmpty(loseNode) ? loseNode : $"{targetNpcName}_Battle_Lose";


        NPCData targetData = GetNPCData(targetNpcName);
        targetData.currentMode = NPC.NPCMode.Attack;

        if (!npcPool.TryGetValue(targetNpcName, out GameObject npcObj) || npcObj == null) return;

        NPC npcScript = npcObj.GetComponent<NPC>();
        if (npcScript == null) return; // ★ null이면 여기서 안전하게 반환 (기존엔 아래 구독부에서 크래시 위험)

        if (npcScript is Liel_AI liel) liel.currentDifficultyTier = difficulty; // ★ SwitchToAttackMode보다 반드시 먼저 — TotalPhaseCount가 여기 의존함

        npcScript.SwitchToAttackMode(); // NPC를 공격 모드로 전환
        UIModeManager.Instance.SetMode(UIMode.Battle); // ** 전투 전용 ui 적용

        // ** [임시 구현] 전투 시작 시 강제로 거리를 벌려줌 (카메라 연출용)
        // (실제로는 맵마다 지정된 '보스전 시작 위치(Transform)'를 가져다 쓸 예정)
        Transform player = PlayerManager.Instance.CurrentCharacter.transform;
        BossHUDPanel.Instance?.BindBoss(npcScript);
        BossHUDPanel.Instance?.SetPhaseCount(npcScript is Liel_AI liel_ai ? liel_ai.TotalPhaseCount : 1);
        BattleTimerDisplay.Instance?.StartTimer();

        // 플레이어는 원래 위치, 보스는 플레이어 기준 오른쪽으로 5칸 뒤로 순간이동
        Vector2 bossStartPos = new Vector2(player.position.x + 5f, player.position.y);

        // NPC 위치 보정 (바닥 레이캐스트 재활용)
        npcObj.transform.position = ComputeSnappedPosition(npcObj, bossStartPos);
        CameraDirector.Instance?.SetSecondaryTarget(npcObj.transform); // 카메라 조정
        PortalManager.Instance?.SetPortalsActive(false); // 포탈 비활성화

        Debug.Log($"[전투 시작] {targetNpcName} 보스전 돌입! 거리를 벌립니다.");

        // ★ null 체크를 이미 위에서 통과했으니, 이 블록은 항상 안전합니다 (기존엔 if 밖에 있어서 위험했음)
        _activeBossBattle = npcScript;
        npcScript.OnHealthChanged += HandleBossHealthChanged;
        PlayerManager.Instance.CurrentCharacter.OnHealthChanged += HandlePlayerHealthChangedDuringBattle;

        var bossFormProvider = npcObj.GetComponent<IFormStageProvider>();
        if (bossFormProvider != null)
            PlayerManager.Instance.CurrentCharacter.GetComponent<BossPhaseTransitionLock>()?.SetLockedNPC(bossFormProvider); //?
    }

    private void HandleBossHealthChanged()
    {
        if (_activeBossBattle == null || _activeBossBattle.currentHealth > 0) return;
        EndBossBattle(win: true);
    }

    private void HandlePlayerHealthChangedDuringBattle()
    {
        var player = PlayerManager.Instance.CurrentCharacter;
        if (_activeBossBattle == null || player.currentHealth > 0) return;
        EndBossBattle(win: false);
    }

    // 전투 종료 처리
    private void EndBossBattle(bool win)
    {
        if (_activeBossBattle == null) return;

        _activeBossBattle.OnHealthChanged -= HandleBossHealthChanged;
        PlayerManager.Instance.CurrentCharacter.OnHealthChanged -= HandlePlayerHealthChangedDuringBattle;

        string bossName = _activeBossBattle.npcName;
        var bossStats = _activeBossBattle;

        bossStats.SwitchToNormalMode();
        bossStats.Heal(bossStats.maxHealth);       // ★ 보스 체력 원상복구
        bossStats.RecoverMana(bossStats.maxMana);  // ★ 보스 마나 원상복구

        if (!win && bossStats is Liel_AI liel && liel.currentDifficultyTier == BossDifficultyTier.Training)
        {
            PlayerManager.Instance.CurrentCharacter.Heal(1); // ★ 훈련모드는 봐주는 대련 — 패배해도 완전히 죽지 않고 1HP로
        }

        _activeBossBattle = null;

        UIModeManager.Instance.SetMode(UIMode.Normal);
        BattleTimerDisplay.Instance?.StopTimer();
        BossHUDPanel.Instance?.UnbindBoss();
        PortalManager.Instance?.SetPortalsActive(true); // 포탈 재활성화

        StartCoroutine(PlayBattleResultAfterDelay(bossName, win));

        PlayerManager.Instance.CurrentCharacter.GetComponent<BossPhaseTransitionLock>()?.UnbindCurrent();
        CameraDirector.Instance?.ClearSecondaryTarget();
    }

    private IEnumerator PlayBattleResultAfterDelay(string bossName, bool win)
    {
        yield return new WaitForSeconds(resultDialogueDelay); // "그 자리에서 멈춘 후에"

        if (npcPool.TryGetValue(bossName, out GameObject npcObj) && npcObj != null)
            npcObj.GetComponent<NPC>()?.RestorePreBattleFacing(); // ★ StartNPCDialogue 바로 직전에 복원

        StartNPCDialogue(bossName); // NPC를 '대화 중' 상태로 고정 → AI 판단 정지

        var resultEvent = new EventData
        {
            EventID = (int)EventID.NPC, // NPC 대역 ID로 지정해야 EventResult()가 정상 분기함
            EventName = bossName,
            InkNodeName = win ? _pendingWinNode : _pendingLoseNode, // ★ 1번에서 저장해둔 노드 사용
            IsAnytime = true,
            TimeTaken = 0, // 이 대화 자체로 시간 코인을 추가 소모하진 않음
        };
        DialogueManager.Instance.StartStory(resultEvent);
    }

    // 이해도 계산
    public void ResetAffectionForNewLoop()
    {
        foreach (var data in npcDataDict.Values)
            if (!data.rememberAcrossLoops) data.hiddenAffection = 0;
        // 이해도는 이제 계산값이라 손 댈 필요 없음

        SuspicionManager.Instance.ResetForNewLoop();
    }

    // 호감도 스냅샷/복원
    public Dictionary<string, int> SnapshotAffections()
    {
        var result = new Dictionary<string, int>();
        foreach (var kvp in npcDataDict) result[kvp.Key] = kvp.Value.hiddenAffection;
        return result;
    }

    public void RestoreAffections(Dictionary<string, int> snapshot)
    {
        foreach (var kvp in snapshot)
            if (npcDataDict.TryGetValue(kvp.Key, out var data)) data.hiddenAffection = kvp.Value;
    }

    public void ResetAllNPCData() // 디버그 완전 리셋 전용
    {
        foreach (var data in npcDataDict.Values) data.hiddenAffection = 0; // rememberAcrossLoops 무시
    }

    // 연출용 이벤트 npc 등장 관리
    public void SummonNPCAt(string npcName, Vector2 position)
    {
        if (!npcPool.TryGetValue(npcName, out GameObject npcObj) || npcObj == null)
        {
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/NPC/{npcName}");
            if (prefab == null) { Debug.LogWarning($"[NPCManager] 소환 실패 — 프리팹 없음: {npcName}"); return; }
            npcObj = Instantiate(prefab);
            npcPool[npcName] = npcObj;
        }
        npcObj.SetActive(true);
        npcObj.transform.position = ComputeSnappedPosition(npcObj, position);
        _summonedForEvent.Add(npcName);
        Debug.Log($"[NPCManager] 연출용 소환: {npcName}");
    }

    public void DespawnEventNPCs()
    {
        foreach (var name in _summonedForEvent)
            if (npcPool.TryGetValue(name, out var obj) && obj != null) obj.SetActive(false);
        _summonedForEvent.Clear();
    }
}
