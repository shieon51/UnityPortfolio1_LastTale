using UnityEngine;

public class Liel_AI : NPC
{
    public enum LielCombatStyle { InjuredCommander, FallenAngel } // 스토리 진행에 따른 축 (캐릭터성)
    // ** 실제 전투 파라미터는 (difficultyTier, combatStyle, bossPhase) 세 값의 조합으로 결정

    [Header("Liel Specifics")]
    public LielCombatStyle currentCombatStyle = LielCombatStyle.InjuredCommander;
    public BossDifficultyTier currentDifficultyTier = BossDifficultyTier.Normal; 
    public int bossPhase = 1; // 타락 모드일 때 1~3페이즈 관리

    [Header("Phase Thresholds")]
    [Tooltip("체력 비율이 이 값 이하로 떨어지면 다음 페이즈로 전환 (인덱스0=2페이즈 진입점, 인덱스1=3페이즈 진입점)")]
    public float[] phaseHealthThresholds = new float[] { 0.6f, 0.3f };

    [Header("Injured Mechanics (치명상 기믹)")]
    public int teleportManaCost = 20;
    public int ultimateManaCost = 40;
    public bool isGroggy = false;
    private float groggyTimer = 0f;

    private float actionCooldown = 1.5f;
    private float lastActionTime = 0f;

    // 일반 모드 행동 제어용 변수들
    [Header("Normal Mode AI")]
    public float approachDistance = 6f; // 다가오기 시작하는 감지 거리
    public float stopDistance = 2f;     // 플레이어 코앞 (멈추는 거리)
    public float walkSpeed = 1.5f;        // 걷는 속도
    public bool hasApproached = false; // 상태 클래스에서 수정할 수 있게 public으로 변경 // 1회만 다가오게 하는 플래그

    // 페이즈 다음 단계 관련 이벤트
    public event System.Action<int> OnPhaseChanged;

    // 보스 HP바 UI가 조회할 페이즈 총 개수 (3번 BossHUDPanel.SetPhaseCount와 연결)
    public int TotalPhaseCount => currentDifficultyTier switch
    {
        BossDifficultyTier.Training => 1,
        BossDifficultyTier.Normal => 2,
        BossDifficultyTier.Hard => 3,
        _ => 1,
    };
    
    protected override void Awake()
    {
        base.Awake();
        currentElement = ElementType.Light; // 빛 속성 고정
        myPersonality = PersonalityTrait.Cold; // 리엘의 성향
        npcName = "Liel"; // NPCData와 매칭될 이름

        // ** 세팅 임시
        level = 99;
        attack.AddBaseValue(10);
        agility.AddBaseValue(999); // 회피 Max
    }

    protected override void Start()
    {
        base.Start(); // 부모의 Start(플레이어 캐싱) 실행

        var formController = GetComponent<NPCFormStageController>();
        if (formController != null) formController.OnFormStageChanged += stage => bossPhase = stage;

        // 시작할 때 현재 모드에 맞춰 FSM 첫 상태를 꽂아줌
        if (CurrentMode == NPCMode.Normal)
            StateMachine.Initialize(new Liel_NormalApproachState(this, visual, player));
        else
            StateMachine.Initialize(new Liel_UtilityDecisionState(this, visual, player));
    }

    // NPC.cs에서 호출해주는 전투 모드 전환 함수 오버라이드
    public override void SwitchToAttackMode()
    {
        base.SwitchToAttackMode();

        // 공격 모드 진입 시 전투 대기 상태로 강제 전환
        StateMachine.ChangeState(new Liel_UtilityDecisionState(this, visual, player));
    }

    // 일반 모드로 돌아오기
    public override void SwitchToNormalMode()
    {
        base.SwitchToNormalMode();
        StateMachine.ChangeState(new Liel_NormalApproachState(this, visual, player)); 
    }

    // 페이즈 전환
    public void CheckPhaseTransition()
    {
        var formController = GetComponent<NPCFormStageController>();
        if (formController == null || formController.IsTransforming) return;

        float hpPercent = maxHealth > 0 ? (float)currentHealth / maxHealth : 1f;
        int targetPhase = 1;
        for (int i = 0; i < phaseHealthThresholds.Length; i++)
            if (hpPercent <= phaseHealthThresholds[i]) targetPhase = i + 2;

        if (targetPhase != bossPhase && targetPhase <= TotalPhaseCount)
        {
            formController.TransitionToStage(targetPhase);
            // TODO: 페이즈별로 실제 뭐가 달라질지(새 스킬 목록, 외형 변화 등)는
            //       이 이벤트를 구독해서 나중에 채우시면 됩니다.
            //       예: NPCUtilityAI.ApplyProfile(currentProfile, bossPhase);
        }
    }

    // ==========================================
    // 1. 일반 모드 (성격 반영)
    // ==========================================
    protected override void HandleNormalModeAI()
    {
        StateMachine.Update();
    }

    // ==========================================
    // 2. 공격 모드 (전투 스타일 분기)
    // ==========================================
    protected override void HandleAttackModeAI()
    {
        // 이제 여기서 if-else를 안 하고, stateMachine만 돌려주면 알아서 행동
        StateMachine.Update();
    }

    // 외부 상태 클래스에서 부모(NPC.cs)의 protected 함수를 쓰기 위한 Public 래퍼 함수
    public void LookAtPlayer_Public()
    {
        base.LookAtPlayer();
    }


    //// ==========================================
    //// 3. 기믹 및 액션 스킬들
    //// ==========================================
    //private void EnterGroggyState()
    //{
    //    isGroggy = true;
    //    groggyTimer = 5.0f; // 5초간 그로기
    //    animator.Play("Groggy");
    //    Debug.Log("[리엘] 그로기 상태!");

    //    // 이때 방어막(Guard)을 쳐서 대미지를 경감시킴
    //    isGuarding = true;
    //}

    //private void HandleGroggyState()
    //{
    //    groggyTimer -= Time.deltaTime;
    //    if (groggyTimer <= 0)
    //    {
    //        isGroggy = false;
    //        isGuarding = false;
    //        RecoverMana(50); // 마나 회복 후 다시 전투
    //        animator.Play("Idle");
    //        Debug.Log("[리엘] 그로기 해제");
    //    }
    //}

    //private void ExecuteTeleport()
    //{
    //    UseMana(teleportManaCost); // 텔레포트로 마나 소모 (공략의 핵심)
    //    animator.SetTrigger("Teleport");
    //    Debug.Log("[리엘] 텔레포트로 플레이어의 공격을 회피합니다!");
    //    // 플레이어 뒤로 이동하는 로직...
    //}

    //private void ExecuteLightUltimate() { UseMana(ultimateManaCost); /* 궁극기 */ }
    //private void ExecuteBasicAttack() { /* 기본 공격 */ }


    // Liel_AI 기즈모 오버라이드
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // 일반 모드일 때만 범위 기즈모 그리기
        if (myData != null && myData.currentMode == NPCMode.Normal)
        {
            // 감지 범위 (노란색 선)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, approachDistance);

            // 멈춤 범위 (빨간색 선)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, stopDistance);
        }
        // 공격 모드 기즈모를 추가하고 싶다면 아래에 else if 추가 가능
    }

}

/*
  Liel_AI의 보스 FSM에서:

  페이즈 전환 조건 만족 시 → GetComponent<NPCFormStageController>().TransitionToStage(2);
  스토리 분기(타락 여부)가 결정되는 시점 → GetComponent<CharacterAppearance>().SetProfile(isFallen ? _fallenProfile : _normalProfile);
*/