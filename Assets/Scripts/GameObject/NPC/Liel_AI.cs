using UnityEngine;

public class Liel_AI : NPC
{
    public enum LielCombatStyle { InjuredCommander, FallenAngel }

    [Header("Liel Specifics")]
    public LielCombatStyle currentCombatStyle = LielCombatStyle.InjuredCommander;
    public int bossPhase = 1; // 타락 모드일 때 1~3페이즈 관리

    [Header("Injured Mechanics (치명상 기믹)")]
    public int teleportManaCost = 20;
    public int ultimateManaCost = 40;
    public bool isGroggy = false;
    private float groggyTimer = 0f;

    private float actionCooldown = 1.5f;
    private float lastActionTime = 0f;

    protected override void Awake()
    {
        base.Awake();
        currentElement = ElementType.Light; // 빛 속성 고정
        myPersonality = PersonalityTrait.Cold; // 리엘의 성향

        // 💡 세계관 최강자 세팅
        level = 99;
        attack.AddBaseValue(500);
        agility.AddBaseValue(999); // 회피 Max
    }

    protected virtual void Update()
    {
        if (isKnockedBack) return;

        // 나중엔 여기에 State Machine(FSM)이 들어갈 자리입니다!
        if (currentMode == NPCMode.Normal)
        {
            // Idle 애니메이션이 무한 반복되고 있을 테니, 일단 비워둡니다.
        }
    }

    // ==========================================
    // 1. 일반 모드 (성격 반영)
    // ==========================================
    protected override void HandleNormalModeAI()
    {
        if (player == null) return;
        float dist = Vector2.Distance(transform.position, player.position);

        // Cold 성향 + 관계 등급 연동 행동
        if (CurrentRelationship == RelationshipTier.Romance)
        {
            // 겉으론 차갑지만(Cold), 플레이어가 위험(피가 적음)하면 조용히 힐을 던져줌
            CharacterStats pStats = player.GetComponent<CharacterStats>();
            if (pStats != null && pStats.currentHealth < pStats.maxHealth * 0.3f && dist < 8f)
            {
                Debug.Log("[리엘] \"...멍청하게 다치고 다니지 마라.\" (조용히 힐)");
                // 힐 로직...
            }
        }
        else if (CurrentRelationship <= RelationshipTier.Wary)
        {
            // 경계/적대 상태일 땐 플레이어가 다가오면 무기를 뽑는 모션 등
        }
    }

    // ==========================================
    // 2. 공격 모드 (전투 스타일 분기)
    // ==========================================
    protected override void HandleAttackModeAI()
    {
        if (player == null || Time.time < lastActionTime + actionCooldown) return;
        if (isGroggy)
        {
            HandleGroggyState();
            return;
        }

        // 💡 전투 버전에 따라 아예 다른 AI 로직을 돌립니다.
        if (currentCombatStyle == LielCombatStyle.InjuredCommander)
        {
            ExecuteInjuredCommanderAI();
        }
        else if (currentCombatStyle == LielCombatStyle.FallenAngel)
        {
            ExecuteFallenAngelAI();
        }

        lastActionTime = Time.time;
    }

    // ----------------------------------------------------
    // ⚔️ 버전 1: 치명상을 입은 기사단장 (초반 맹공 -> 마나 고갈 -> 그로기)
    // ----------------------------------------------------
    private void ExecuteInjuredCommanderAI()
    {
        float dist = Vector2.Distance(transform.position, player.position);

        // 💡 [핵심 기믹] 마나가 부족하면(스킬을 너무 많이 썼거나 텔레포트를 뺐다면) 그로기 돌입!
        if (currentMana < 10)
        {
            EnterGroggyState();
            return;
        }

        // 플레이어가 치명적인 공격(궁극기 등)을 날리는 것을 감지했다고 가정 (임시 조건)
        bool playerUsingUltimate = false; // 실제론 PlayerAttack의 상태를 참조해야 함

        if (playerUsingUltimate && currentMana >= teleportManaCost)
        {
            // 회피를 위해 마나를 쥐어짜며 텔레포트! -> 마나 고갈을 유도하는 플레이어의 공략법
            ExecuteTeleport();
            return;
        }

        // 초반 마나가 넉백할 땐 무조건 강력한 궁극기/연속기 맹공!
        if (currentMana >= ultimateManaCost)
        {
            ExecuteLightUltimate();
        }
        else
        {
            ExecuteBasicAttack();
        }
    }

    // ----------------------------------------------------
    // ⚔️ 버전 2: 타락한 날개 (페이즈별 광폭화, 디버프 없음)
    // ----------------------------------------------------
    private void ExecuteFallenAngelAI()
    {
        float hpPercent = (float)currentHealth / maxHealth;

        // 페이즈 전환 로직
        if (hpPercent < 0.3f && bossPhase == 2)
        {
            bossPhase = 3;
            Debug.Log("[리엘] \"모든 것을 파괴하겠다...\" (3페이즈 즉사기 해금)");
        }
        else if (hpPercent < 0.7f && bossPhase == 1)
        {
            bossPhase = 2;
            Debug.Log("[리엘] 타락의 날개를 펼칩니다! (2페이즈 진입)");
        }

        // 페이즈별 유틸리티 AI 점수 계산 및 행동
        if (bossPhase == 3)
        {
            // 미친듯한 광역기 난사
        }
        else if (bossPhase == 2)
        {
            // 속도 증가 및 연속 공격
        }
        // ...
    }

    // ==========================================
    // 3. 기믹 및 액션 스킬들
    // ==========================================
    private void EnterGroggyState()
    {
        isGroggy = true;
        groggyTimer = 5.0f; // 5초간 그로기
        animator.Play("Groggy");
        Debug.Log("[리엘] \"...큭, 치명상이...!\" (리엘이 배를 부여잡고 비틀거립니다.)");

        // 💡 이때 방어막(Guard)을 쳐서 대미지를 경감시킴
        isGuarding = true;
    }

    private void HandleGroggyState()
    {
        groggyTimer -= Time.deltaTime;
        if (groggyTimer <= 0)
        {
            isGroggy = false;
            isGuarding = false;
            RecoverMana(50); // 억지로 마나 회복 후 다시 전투
            animator.Play("Idle");
            Debug.Log("[리엘] 억지로 숨을 고르며 다시 자세를 잡습니다.");
        }
    }

    private void ExecuteTeleport()
    {
        UseMana(teleportManaCost); // 텔레포트로 마나 소모! (공략의 핵심)
        animator.SetTrigger("Teleport");
        Debug.Log("[리엘] 텔레포트로 플레이어의 공격을 회피합니다!");
        // 플레이어 뒤로 이동하는 로직...
    }

    private void ExecuteLightUltimate() { UseMana(ultimateManaCost); /* 궁극기 쾅! */ }
    private void ExecuteBasicAttack() { /* 기본 공격 */ }

}
