using UnityEngine;

public class Liel_AI : NPC
{
    public enum LielCombatStyle { InjuredCommander, FallenAngel }

    [Header("Liel Specifics")]
    public LielCombatStyle currentCombatStyle = LielCombatStyle.InjuredCommander;
    public int bossPhase = 1; // 타락 모드일 때 1~3페이즈 관리

    //[Header("Combat Settings")]
    //public float attack1Range = 2f; // 공격 사거리

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

    // LielAttackCompo 컴포넌트를 캐싱해둘 변수
    [HideInInspector] public LielAttackCompo attackCompo;


    protected override void Awake()
    {
        base.Awake();
        currentElement = ElementType.Light; // 빛 속성 고정
        myPersonality = PersonalityTrait.Cold; // 리엘의 성향
        npcName = "Liel"; // NPCData와 매칭될 이름

        // 세팅
        level = 99;
        attack.AddBaseValue(500);
        agility.AddBaseValue(999); // 회피 Max

        // 시작할 때 컴포넌트 찾아두기
        attackCompo = GetComponent<LielAttackCompo>();
        if (attackCompo == null) attackCompo = GetComponentInChildren<LielAttackCompo>();
    }

    protected override void Start()
    {
        base.Start(); // 부모의 Start(플레이어 캐싱) 실행

        // 시작할 때 현재 모드에 맞춰 FSM 첫 상태를 꽂아줌
        if (CurrentMode == NPCMode.Normal)
            StateMachine.Initialize(new Liel_NormalApproachState(this, animator, player));
        else
            StateMachine.Initialize(new Liel_BattleIdleState(this, animator, player));
    }

    // NPC.cs에서 호출해주는 전투 모드 전환 함수 오버라이드
    public override void SwitchToAttackMode()
    {
        base.SwitchToAttackMode();
        // 공격 모드 진입 시 전투 대기 상태로 강제 전환
        StateMachine.ChangeState(new Liel_BattleIdleState(this, animator, player));
    }

    // ==========================================
    // 1. 일반 모드 (성격 반영)
    // ==========================================
    protected override void HandleNormalModeAI()
    {
        StateMachine.Update();

        //if (player == null || myData == null) return;
        //float dist = Vector2.Distance(transform.position, player.position);

        //// 1. 친밀도 확인 (예: Friend 이상일 때만 반응하도록 설정)
        //if (CurrentRelationship >= RelationshipTier.Friend)
        //{
        //    // 2. 감지 거리 안으로 들어왔고, 아직 다가간 적이 없다면?
        //    if (dist <= approachDistance && !hasApproached)
        //    {
        //        LookAtPlayer(); // 방향 전환

        //        // 코앞(stopDistance)까지 오지 않았다면 걷기
        //        if (dist > stopDistance)
        //        {
        //            animator.SetBool("IsWalk", true); // 걷기 애니메이션 ON

        //            float dir = (player.position.x > transform.position.x) ? 1f : -1f;
        //            transform.position += new Vector3(dir * walkSpeed * Time.deltaTime, 0, 0);
        //        }
        //        else
        //        {
        //            // 코앞에 도착했으면 멈추고 1회 플래그 달성
        //            animator.SetBool("IsWalk", false); // 걷기 애니메이션 OFF
        //            hasApproached = true;
        //        }
        //    }
        //    else
        //    {
        //        // 다가가는 중이 아닐 때 (이미 다가왔거나, 아예 범위 밖일 때)
        //        animator.SetBool("IsWalk", false);

        //        // (플레이어가 멀리 떠나면 다시 다가올 수 있게 리셋해주면 자연스러움)
        //        if (dist > approachDistance * 1.5f)
        //        {
        //            hasApproached = false;
        //        }
        //    }
        //}
        //else
        //{
        //    // 안 친할 때: 쳐다보지도 않고 가만히 있음
        //    animator.SetBool("IsWalk", false);
        //}
    }

    // ==========================================
    // 2. 공격 모드 (전투 스타일 분기)
    // ==========================================
    protected override void HandleAttackModeAI()
    {
        // 이제 여기서 if-else를 안 하고, stateMachine만 돌려주면 알아서 행동
        StateMachine.Update();


        //// 유틸리티 AI 로직에서 핸디캡을 줄 때도 myData 활용
        //// if (myData.hiddenAffection > 50) score -= 30f; (봐주기)

        //// ... 보스 페이즈 관리도 myData 활용
        //// if (hpPercent < 0.7f && myData.bossPhase == 1) { myData.bossPhase = 2; ... }

        //if (player == null || Time.time < lastActionTime + actionCooldown) return;
        //if (isGroggy)
        //{
        //    HandleGroggyState();
        //    return;
        //}

        //// 전투 버전에 따라 아예 다른 AI 로직을 돌림
        //if (currentCombatStyle == LielCombatStyle.InjuredCommander)
        //{
        //    ExecuteInjuredCommanderAI();
        //}
        //else if (currentCombatStyle == LielCombatStyle.FallenAngel)
        //{
        //    ExecuteFallenAngelAI();
        //}

        //lastActionTime = Time.time;
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
