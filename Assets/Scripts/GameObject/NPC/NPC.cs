using UnityEngine;

// 💡 abstract 키워드를 붙여서, 'NPC'라는 객체 자체는 생성하지 못하고 무조건 상속받게 만듭니다.
public abstract class NPC : CharacterStats
{
    //  관계 등급(내면) & 표현 성향(표현 방식)
    public enum RelationshipTier { Hostile, Wary, Acquaintance, Friend, Trusted, Romance }
    public enum PersonalityTrait { Honest, Tsundere, Shy, Cold }
    public enum NPCMode { Normal, Attack }

    [Header("Identity")]
    public string npcName; // 프리팹 인스펙터에서 설정! (예: "Liel")
    public PersonalityTrait myPersonality = PersonalityTrait.Honest;

    protected NPCData myData;

    // 💡 [신규] 매니저가 처음 나를 스폰시켰을 때의 CSV 좌표
    [HideInInspector] public Vector2 originalCsvPos;

    // 💡 [수정 1] 인스펙터에서 호감도를 보거나 수정하기 위해 property 대신 직접 접근 가능하게 만듭니다.
    // 주의: 인스펙터 수정은 실행 중에만 myData에 반영되며, 에디터 수정값을 초기값으로 쓰려면 NPCData 초기화 로직을 건드려야 합니다.
    [Header("Relations (Read/Write)")]
    [SerializeField] private int debugUnderstanding = 0;
    [SerializeField] private int debugHiddenAffection = 0;

    public NPCMode CurrentMode => myData != null ? myData.currentMode : NPCMode.Normal;
    public RelationshipTier CurrentRelationship => myData != null ? myData.GetRelationshipTier() : RelationshipTier.Wary;

    // 대화 상호작용을 위한 트리거 (NPC 프리팹 자식에 부착되어 있음)
    private EventTrigger myEventTrigger;
    protected Animator animator;
    protected Transform player;

    // 중력 제어
    protected Rigidbody2D rb;
    private float originalGravity; // 보스전 돌입 시 돌려줄 원래 중력

    // 💡 [신규] 모든 NPC가 공유할 스프라이트 렌더러
    protected SpriteRenderer spriteRenderer;

    // 💡 [신규] 대화 관련 변수들
    protected bool isTalking = false;
    private bool previousFlipX = false;

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) originalGravity = rb.gravityScale;

        // 자식 오브젝트에 달려 있는 EventTrigger를 찾음
        myEventTrigger = GetComponentInChildren<EventTrigger>(true);

        // 💡 부모에서 한 번만 캐싱해두면 모든 자식이 쓸 수 있음!
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // 💡 [수정됨] Start에 있던 데이터 동기화를 Awake로 끌어올림!
        // Instantiate 되자마자 OnEnable이 불리기 전에 미리 데이터를 채워둡니다.
        if (NPCManager.Instance != null)
        {
            myData = NPCManager.Instance.GetNPCData(npcName);
        }

    }

    private void Start()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentCharacter != null)
            player = PlayerManager.Instance.CurrentCharacter.transform;
    }

    // 💡 [핵심] EventManager가 NPC를 켤 때 직접 "지금 시간의 이벤트 데이터"를 주입해 줍니다. (대사 꼬임 완벽 해결)
    public void SetupCurrentEvent(EventData currentData)
    {
        if (myEventTrigger != null)
        {
            myEventTrigger.UpdateTrigger(currentData);
        }
    }


    private void OnEnable()
    {
        // 💡 [방어 코드 추가] 혹시라도 데이터가 꼬여서 null이면 중단
        if (myData == null) return;

        // 💡 일반 모드일 때만 물리 간섭 차단
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

    // 💡 [신규] 대화 시작 시 호출됨
    public void OnDialogueStart()
    {
        isTalking = true;
        if (spriteRenderer != null)
        {
            previousFlipX = spriteRenderer.flipX; // 원래 방향 기억
            LookAtPlayer(); // 플레이어를 쳐다봄
        }

        // 걷고 있었다면 강제 정지
        if (animator != null) animator.SetBool("IsWalk", false);
    }

    // 💡 [신규] 대화 종료 시 호출됨
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

        // 💡 [핵심] 대화 중일 때는 AI 판단(다가가기 등)을 멈춤!
        if (isTalking) return;

        // 💡 [수정 1-2] 에디터에서 값을 바꾸면 실제 데이터(myData)에도 실시간 반영 (디버깅 편의)
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
            HandleAttackModeAI(); // ** 여기에 1/2/3 페이즈 유틸리티 AI (가중치 계산) 적용 예정!
        }
    }

    // 자식 클래스(Liel, Gaon 등)가 무조건 각자의 방식으로 오버라이드(구현)해야 하는 함수들
    protected abstract void HandleNormalModeAI();
    protected abstract void HandleAttackModeAI();

    //// 💡 전투 시(공격 모드) 보스 패턴
    //private void HandleAttackModeAI()
    //{
    //    // [유틸리티 AI 뼈대]
    //    // 1. 현재 내 체력 
    //    // 2. 플레이어와의 거리
    //    // 3. 내 호감도 (핸디캡 적용)
    //    // 위 요소를 종합해 점수를 매겨 (평타/돌진/마법/의도적 Miss) 중 하나를 선택해 실행
    //}

    // 공격 모드로 진입하는 함수 (스토리나 특정 조건 만족 시 호출됨)
    public void SwitchToAttackMode()
    {
        if (myData == null) return;

        myData.currentMode = NPCMode.Attack;

        if (myEventTrigger != null)
        {
            EventManager.Instance.UnregisterDynamicTrigger(myEventTrigger);
            myEventTrigger.gameObject.SetActive(false);
        }

        // 💡 [중력 복구] 전투가 시작되면 다시 중력을 줘서 정상적인 물리 전투가 되게 합니다.
        if (rb != null) rb.gravityScale = originalGravity;

        Debug.Log($"{gameObject.name}이(가) 공격 모드로 돌입했습니다!");
    }

    // 💡 호감도 상승 등 이벤트가 발생하면 호출할 함수
    public void IncreaseAffection(int amount) // ***
    {
        if (myData == null) return;

        myData.hiddenAffection += amount;
        NPCManager.Instance.SaveNPCData(myData); // 변경된 내 기억을 매니저에게 저장하라고 보냄
        Debug.Log($"[{npcName}] 호감도 상승! (현재: {myData.hiddenAffection})");
    }

    // ==========================================
    // 💡 [공통 헬퍼 함수] 모든 자식 NPC가 가져다 쓸 유틸리티 로직
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
        if (player != null) LookAtTarget(player.position);
    }

    // 💡 [수정 2] 공통 기즈모 그리기 함수 (자식에서 호출 가능)
    protected virtual void OnDrawGizmosSelected()
    {
        // 자식 클래스에서 오버라이드하여 각자의 범위를 그릴 예정
    }
}
