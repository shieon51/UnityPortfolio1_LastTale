using System.Collections;
using TMPro;
using UnityEngine;

// NPC 클래스 (추상)
public abstract class NPC : CharacterStats, ICombatTargetable
{
    private CutsceneAnimationPlayer _cutscenePlayer;

    // ** 디버깅 전용 **
    [Header("Debug Settings")]
    public TextMeshProUGUI statusText; // NPC 머리 위 TextMesh (World Space)

    // 인스펙터에서 호감도를 보거나 수정하기 위해 property 대신 직접 접근 가능하게 만듦.
    // 주의: 인스펙터 수정은 실행 중에만 myData에 반영되며, 에디터 수정값을 초기값으로 쓰려면 NPCData 초기화 로직을 건드려야 함.
    [Header("Relations (Read/Write)")]
    [SerializeField] private int debugUnderstanding = 0;
    [SerializeField] private int debugHiddenAffection = 0;

    //------------------------------------------------------------------
    // 외부 상태 클래스들이 접근할 수 있도록 Property로 변경
    public StateMachine StateMachine { get; protected set; }

    //  관계 등급(내면) & 표현 성향(표현 방식)
    public enum RelationshipTier { Hostile, Wary, Acquaintance, Friend, Trusted, Romance }
    public enum PersonalityTrait { Honest, Tsundere, Shy, Cold }
    public enum NPCMode { Normal, Attack }


    [Header("Identity")]
    public string npcName; // 프리팹 인스펙터에서 설정 (예: "Liel")
    public PersonalityTrait myPersonality = PersonalityTrait.Honest;

    protected NPCData myData;

    // 매니저가 처음 나를 스폰시켰을 때의 CSV 좌표
    [HideInInspector] public Vector2 originalCsvPos;

    // 모든 NPC가 공유할 스프라이트 렌더러
    protected SpriteRenderer spriteRenderer;

    public NPCMode CurrentMode => myData != null ? myData.currentMode : NPCMode.Normal;
    public RelationshipTier CurrentRelationship => myData != null ? myData.GetRelationshipTier() : RelationshipTier.Wary;

    // 대화 상호작용을 위한 트리거 (NPC 프리팹 자식에 부착되어 있음)
    private EventTrigger myEventTrigger;
    protected NPCVisual visual;
    protected Transform player;

    // 중력 제어
    protected Rigidbody2D rb;
    private float originalGravity; // 보스전 돌입 시 돌려줄 원래 중력

    // 방향 전환 가능 여부 플래그 (보스전)
    public bool canRotate = true; 

    // 대화 관련 변수들
    protected bool isTalking = false;
    private bool previousFlipX = false;

    // 스킬 클래스들이 접근할 수 있도록 프로퍼티 공개
    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public Rigidbody2D Rb => rb;

    // 모션 컨택스트:
    // 지금은 항상 Grounded, 나중에 NPC용 비행/점프 컨트롤러가 생기면 그쪽에서 SetMovementContext()를 호출해 갱신하도록 확장 가능
    public MovementContext CurrentMovementContext { get; protected set; } = MovementContext.Grounded;
    public void SetMovementContext(MovementContext context) => CurrentMovementContext = context;

    // 마나 사용 관련
    private float _manaRegenAccumulator = 0f;

    // 지형 벗어남 예외처리 (리스폰)
    private Vector2 _lastGroundedPosition;
    private float _groundCheckTimer = 0f;
    private const float GroundCheckInterval = 0.5f;

    // 플레이 중 실시간 기즈모 (PlayerCombat과 동일한 패턴)
    private bool _showHitbox = false;
    private Vector2 _lastHitboxCenter, _lastHitboxSize;
    public void SetDebugHitbox(Vector2 center, Vector2 size) { _showHitbox = true; _lastHitboxCenter = center; _lastHitboxSize = size; }
    public void ClearDebugHitbox() { _showHitbox = false; }

    public NPCSkillBase CurrentPlayingSkill { get; set; } // Liel_ExecutingActionState가 실행 시작할 때 설정

    protected override void Awake()
    {
        base.Awake();
        visual = GetComponentInChildren<NPCVisual>();
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) originalGravity = rb.gravityScale;

        _cutscenePlayer = GetComponent<CutsceneAnimationPlayer>(); // ?

        // 자식 오브젝트에 달려 있는 EventTrigger를 찾음
        myEventTrigger = GetComponentInChildren<EventTrigger>(true);

        // 부모에서 한 번만 캐싱해두면 모든 자식이 쓸 수 있음
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Instantiate 되자마자 OnEnable이 불리기 전에 미리 데이터를 채워두기
        if (NPCManager.Instance != null)
        {
            myData = NPCManager.Instance.GetNPCData(npcName);
        }

        StateMachine = new StateMachine();
    }

    protected virtual void Start()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentCharacter != null)
            player = PlayerManager.Instance.CurrentCharacter.transform;
    }

    // EventManager가 NPC를 켤 때 직접 지금 시간의 이벤트 데이터를 주입 
    public void SetupCurrentEvent(EventData currentData)
    {
        if (myEventTrigger != null)
        {
            myEventTrigger.UpdateTrigger(currentData);
        }
    }

    private void OnEnable()
    {
        // 혹시라도 데이터가 꼬여서 null이면 중단
        if (myData == null) return;

        // 일반 모드일 때만 물리 간섭 차단
        if (myData.currentMode == NPCMode.Normal)
        {
            if (myEventTrigger != null) EventManager.Instance.RegisterDynamicTrigger(myEventTrigger);
            if (rb != null)
            {
                rb.gravityScale = 0f; // 촥 붙어있게 함
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    private void OnDisable()
    {
        // 게임 종료 시 EventManager가 먼저 파괴되었을 수 있으므로 null 체크
        if (myEventTrigger != null && EventManager.Instance != null)
        {
            EventManager.Instance.UnregisterDynamicTrigger(myEventTrigger);
        }
    }

    // 패링 확률 오버라이드
    protected override bool TryResolveParry(CharacterStats attacker)
    {
        if (attacker == null) return false;
        float chance = CombatFormulaService.Instance.CalculateParryChance(this, attacker);
        return Random.value < chance;
    }

    // 지금 재생중인 스킬 알리기
    public void NotifyDashStart() => CurrentPlayingSkill?.OnDashStart();
    public void NotifyHitboxStart() => CurrentPlayingSkill?.OnHitboxStart();
    public void NotifySlideStart() => CurrentPlayingSkill?.OnSlideStart();
    public void NotifyActionEnd() => CurrentPlayingSkill?.OnActionEndEvent();

    public void PlayCurrentSkillVFX(string cueId)
    {
        if (CurrentPlayingSkill == null) return;
        var cue = CurrentPlayingSkill.FindVFXCue(cueId);
        if (cue == null) return;
        string vfxKey = cue.ResolveVFXKey(1);
        if (string.IsNullOrEmpty(vfxKey)) return;

        float dir = SpriteRenderer.flipX ? -1f : 1f;
        Vector2 offset = new Vector2(cue.spawnOffset.x * dir, cue.spawnOffset.y);
        Transform followParent = cue.followCaster ? transform : null;
        VFXManager.Instance.Play(vfxKey, (Vector2)transform.position + offset, dir, PoolType.Global, followParent);
    }

    public void PlayCurrentSkillCamera(string cueId) => CameraDirector.Instance?.PlayCue(CurrentPlayingSkill?.FindCameraCue(cueId)); //?

    // 대화 시작 시 호출됨
    public void OnDialogueStart()
    {
        isTalking = true;
        if (spriteRenderer != null)
        {
            previousFlipX = spriteRenderer.flipX; // 원래 방향 기억
            LookAtPlayer(); // 플레이어를 쳐다봄
        }

        visual?.PlayIfChanged(NPCAnimStateNames.Idle); // animator.SetBool("IsWalk", false) 대체
    }

    // 대화 종료 시 호출됨
    public void OnDialogueEnd()
    {
        isTalking = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = previousFlipX; // 원래 보던 방향으로 원상복구
        }
    }

    private void Update()
    {
        if (isKnockedBack || myData == null) return; // 넉백 중엔 행동 불가
        if (isTalking) return;                       // 대화 중일 때는 AI 판단(다가가기 등)을 멈춤
        if (_cutscenePlayer != null && _cutscenePlayer.IsLocked) return; // 연출 중엔 AI 정지

        // ★ 프레임마다 조금씩 쌓아뒀다가, '1 이상' 모였을 때만 실제로 회복시킴
        if (currentMana < maxMana)
        {
            _manaRegenAccumulator += ManaRegenPerSecond * Time.deltaTime;
            if (_manaRegenAccumulator >= 1f)
            {
                int whole = Mathf.FloorToInt(_manaRegenAccumulator);
                RecoverMana(whole);
                _manaRegenAccumulator -= whole;
            }
        }

        // 디버깅용 상태 출력
        if (statusText != null && StateMachine.CurrentState != null)
            statusText.text = StateMachine.CurrentState.GetType().Name.Replace("Liel_", "");

        // 에디터에서 값을 바꾸면 실제 데이터(myData)에도 실시간 반영 (디버깅 편의)
#if UNITY_EDITOR
        if (myData.understanding != debugUnderstanding || myData.hiddenAffection != debugHiddenAffection)
        {
            myData.understanding = debugUnderstanding;
            myData.hiddenAffection = debugHiddenAffection;
            NPCManager.Instance.SaveNPCData(myData);
        }
        else
        {
            debugUnderstanding = myData.understanding;
            debugHiddenAffection = myData.hiddenAffection;
        }
#endif

        // 내 현재 모드(myData 안에 저장됨)에 따라 행동 분기
        if (myData.currentMode == NPCMode.Normal)
        {
            HandleNormalModeAI();
        }
        else if (myData.currentMode == NPCMode.Attack)
        {
            HandleAttackModeAI(); // ** 여기에 1/2/3 페이즈 유틸리티 AI (가중치 계산) 적용 예정
        }

        // 예기치 못한 경우 리스폰
        _groundCheckTimer += Time.deltaTime;
        if (_groundCheckTimer >= GroundCheckInterval)
        {
            _groundCheckTimer = 0f;
            CheckFailsafeRespawn();
        }
    }

    // 자식 클래스(Liel, Gaon 등)가 무조건 각자의 방식으로 오버라이드(구현)해야 하는 함수들
    protected abstract void HandleNormalModeAI();
    protected abstract void HandleAttackModeAI();

    public bool IsValidCombatTarget(CharacterStats attacker)
    {
        if (myData != null && myData.currentMode == NPCMode.Attack) return true; // 보스전 중이면 무조건 유효

        // 평상시엔 공격자가 '적대 상태'일 때만 유효 (기획 미확정 — 우선 훅만 열어둠) // **
        return attacker is SoraStats sora && sora.IsHostileState;
    }

    // 공격 모드로 진입하는 함수 (스토리나 특정 조건 만족 시 호출됨)
    public virtual void SwitchToAttackMode()
    {
        if (myData == null) return;

        myData.currentMode = NPCMode.Attack;

        if (myEventTrigger != null)
        {
            EventManager.Instance.UnregisterDynamicTrigger(myEventTrigger);
            myEventTrigger.gameObject.SetActive(false);
        }

        // [중력 복구] 전투가 시작되면 다시 중력을 줘서 정상적인 물리 전투가 되도록
        if (rb != null) rb.gravityScale = originalGravity;

        Debug.Log($"{gameObject.name}이(가) 공격 모드로 돌입했습니다!");
    }

    public virtual void SwitchToNormalMode()
    {
        if (myData == null) return;

        StopAllCoroutines(); // ★ 핵심: 진행 중이던 공격/이동 코루틴을 완전히 중단.
                             //   (Unity는 StopCoroutine으로 중단된 코루틴의 finally 블록을 실행하지 않으므로,
                             //    이후 아무도 뒤늦게 상태를 되돌릴 수 없게 됩니다)
        ClearDebugHitbox(); // ★ 진행 중이던 히트박스 기즈모도 강제로 끔 (코루틴이 중간에 끊겨 자연 종료 못하는 경우 대비)

        myData.currentMode = NPCMode.Normal;

        if (myEventTrigger != null)
        {
            myEventTrigger.gameObject.SetActive(true);
            EventManager.Instance.RegisterDynamicTrigger(myEventTrigger);
        }

        if (rb != null)
        {
            rb.gravityScale = 0f; // 평시 모드 복귀 시 다시 물리 간섭 차단 (OnEnable과 동일)
            rb.angularVelocity = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.Sleep(); // ★ 추가 — 물리 상태를 완전히 재워서 보간으로 인한 잔여 미끄러짐까지 확실히 제거
            StartCoroutine(ForceZeroVelocityNextFixedUpdate(rb)); // ★ 추가 — 이미 큐잉된 힘이 한 스텝 뒤에 뒤늦게 반영되는 경우까지 대비
        }

        canRotate = true;
        isSuperArmor = false;
        CurrentPlayingSkill = null;
    }

    private IEnumerator ForceZeroVelocityNextFixedUpdate(Rigidbody2D rb)
    {
        yield return new WaitForFixedUpdate();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.Sleep(); }
    }

    // 호감도 상승 등 이벤트가 발생하면 호출할 함수
    public void IncreaseAffection(int amount) // ***
    {
        if (myData == null) return;

        myData.hiddenAffection += amount;
        NPCManager.Instance.SaveNPCData(myData); // 변경된 내 기억을 매니저에게 저장하라고 보냄
        Debug.Log($"[{npcName}] 호감도 상승! (현재: {myData.hiddenAffection})");
    }

    // 예외처리 (리스폰)
    private void CheckFailsafeRespawn()
    {
        if (SceneBoundsManager.Instance == null || !SceneBoundsManager.Instance.HasBounds) return;

        if (SceneBoundsManager.Instance.IsWellWithinBounds(transform.position))
        {
            _lastGroundedPosition = transform.position; // 정상 범위 안이면 계속 갱신
        }
        else
        {
            Debug.LogWarning($"[{npcName}] 지형 바깥으로 벗어나 마지막 정상 위치로 리스폰합니다.");
            transform.position = _lastGroundedPosition;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
    }

    // ==========================================
    // [공통 헬퍼 함수] 모든 자식 NPC가 가져다 쓸 유틸리티 로직
    // ==========================================

    // 특정 타겟을 쳐다보는 함수 (좌우 반전)
    protected void LookAtTarget(Vector3 targetPos)
    {
        if (spriteRenderer == null) return;

        // 타겟이 내 왼쪽에 있으면 flipX를 true로 (기본 이미지가 오른쪽을 본다고 가정)
        spriteRenderer.flipX = targetPos.x > transform.position.x;
    }

    // 플레이어를 쳐다보는 함수
    protected void LookAtPlayer()
    {
        if (!canRotate || player == null || spriteRenderer == null) return; // 플래그 체크 
        
        LookAtTarget(player.position);
    }

    protected override Vector2 ComputeKnockbackForce(Vector2 direction, float power) => direction.normalized * power; // 순수 반대 방향
    protected override void PrepareRigidbodyForKnockback(Rigidbody2D rb) => rb.linearVelocity = Vector2.zero; // 기존 관성 완전 제거

    protected virtual float ManaRegenPerSecond => 2f; // 자식 NPC가 필요시 오버라이드


    // 공통 기즈모 그리기 함수 (자식에서 호출 가능)
    protected virtual void OnDrawGizmosSelected()
    {
        // 자식 클래스에서 오버라이드하여 각자의 범위를 그릴 예정
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying && _showHitbox)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawCube(_lastHitboxCenter, _lastHitboxSize);
        }
    }
#endif
}
