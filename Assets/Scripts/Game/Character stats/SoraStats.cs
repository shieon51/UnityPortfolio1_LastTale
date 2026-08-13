using System;
using System.Collections;
using UnityEngine;

// PlayableCharacter를 상속받는 1부 전용 주인공 '소라'
public class SoraStats : PlayableCharacter, IFormStageProvider, IActionLockSource
{
    // ===============================================================
    // 요정화 변신 관련 변수 정리
    // ===============================================================
    // fairyStage      : "지금 확정된" 단계 (0=1단계, 1=2단계/비행형). 변신 연출이 끝나야 바뀜.
    // TargetFormStage : "지금 전환하려는 목표" 단계. 변신 시작 순간 바로 세팅되고,
    //                    연출 재생 중(=아직 fairyStage는 안 바뀐 상태)에 참고용으로 쓰임
    //                    (예: 나중에 요정화 2→3단계 변신 클립이 여러 개면, 이 값 보고 어느 클립을 고를지 결정)
    // _isTransforming : 지금 변신 연출 재생 중인지. true인 동안 IActionLockSource로 조작 잠김.
    // ===============================================================

    private IPlayerMotor _motor;

    [Header("Form Change (요정화)")] // 시즌 1에만 쓸지, 아님 다른 캐릭터도 해당하는가?
    [Tooltip("기획 반영: 변신 딜레이 0.5초")]
    public float formTransformDuration = 0.5f;

    public int FormStage => fairyStage;
    public int TargetFormStage { get; private set; } = 0; //?
    public bool IsFlightForm => fairyStage == 1; // 2단계 = 비행모드
    public bool IsTransforming => _isTransforming;
    public bool IsLocked => _isTransforming;      // IActionLockSource

    public event Action OnFormTransformStarted;
    public event Action<int> OnFormStageChanged;

    [Header("Time Loop")]
    public int loopCount = 0; // 회귀 횟수 (나중에 실제 회귀 시스템과 연동)

    [Header("Sora Exclusives - Meta Stats")] //피로도, 정신력 스탯
    public int maxFatigue = 100;
    public int currentFatigue = 0;
    public int maxMental = 100;
    public int currentMental = 100;

    [Header("Sora Exclusives - Time Loop")] // 시간결정체
    public int timeCrystals = 0;

    [Header("Form Change (요정화)")] // 변신 후 쿨타임
    public float formToggleCooldown = 0.3f;
    private float _lastTransformEndTime = -10f;

    private bool _isTransforming = false; // 변신 딜레이 중인지 체크

    // 소라의 특수 스탯은 '피로도'임을 UI에게 알려줌
    public override bool HasSpecialStat => true;
    public override float SpecialStatPercentage => (float)currentFatigue / maxFatigue;
    public override string SpecialStatText => $"{currentFatigue}/{maxFatigue}";

    [Header("Hostility (실제 트리거 조건은 추후 스토리/정신력 시스템과 연동 예정)")]
    public bool IsHostileState = false; // npc 공격 가능한지?

    protected override void Awake()
    {
        base.Awake();
        currentElement = ElementType.Spacetime; // 소라 전용 속성
        currentMental = maxMental;
        _motor = GetComponent<IPlayerMotor>();
    }

    // 플레이어가 소라를 조종하기 시작할 때 호출됨 (빙의)
    public override void OnPossessed()
    {
        Debug.Log("소라의 시점으로 플레이를 시작합니다.");
        // UI 매니저에게 소라 전용 UI(정신력 바, 피로도 바)를 켜라고 명령
    }

    // 빙의 해제
    public override void OnUnpossessed()
    {
        Debug.Log("소라의 시점에서 벗어납니다.");
    }

    // 소라만의 고유 업데이트 로직 (마나 리젠, 요정화 패널티)
    protected override void HandleSpecialMechanics()
    {
        // Tab 키를 누르면 1단계 <-> 2단계 변신 (3단계는 강제 발동이므로 2단계까지만 토글)
        if (Input.GetKeyDown(KeyCode.Tab) && !_isTransforming && !isKnockedBack
             && Time.time >= _lastTransformEndTime + formToggleCooldown
             && (_motor == null || !_motor.IsActionLocked))
        {
            StartCoroutine(FairyTransformRoutine());
        }

        // 1. 시간 결정체에 비례한 마나 리젠 로직 // ***
        if (currentMana < maxMana)
        {
            // float regen = 1f + (timeCrystals * 0.2f);
            // currentMana += ...
        }

        // 2. 요정화 지속 시 정신력 하락 패널티 로직
        if (fairyStage > 0)
        {
            fairyTimer += Time.deltaTime;
            if (fairyTimer >= 5f)
            {
                fairyTimer = 0f;
                LoseMental(fairyStage);
            }
        }
    }

    // [기획 반영] 1초의 변신 딜레이
    private IEnumerator FairyTransformRoutine()
    {
        _isTransforming = true;  // 조작 잠금 시작
        isSuperArmor = true;     // 변신 중 무적이나 슈퍼아머 처리 (원하는 대로 변경 가능)

        TargetFormStage = (fairyStage == 0) ? 1 : 0; // 목표 단계 미리 확정 (fairyStage 자체는 아직 그대로)
        OnFormTransformStarted?.Invoke();             // 이 시점에 구독자가 TargetFormStage를 참고해 연출 준비 가능

        // 시각 효과 호출 (이펙트 등)
        Debug.Log("요정화 변신 시작...");
        yield return new WaitForSeconds(formTransformDuration); // 변신 딜레이

        fairyStage = (fairyStage == 0) ? 1 : 0; // 0(1단계) <-> 1(2단계) 토글 ★ 여기서 비로소 "확정" 단계가 바뀜
        Debug.Log($"요정화 단계가 {fairyStage + 1}단계로 변경되었습니다.");
        OnFormStageChanged?.Invoke(fairyStage); // 확정된 단계를 알림

        isSuperArmor = false;
        _isTransforming = false;  // 조작 잠금 해제
        _lastTransformEndTime = Time.time;
    }

    // [기획 반영] 폼체인지 시 마나 사용 효율 증가
    public override int CalculateManaCost(int originalCost)
    {
        if (fairyStage == 1) return Mathf.FloorToInt(originalCost * 0.8f); // 2단계: 마나 20% 감소
        if (fairyStage == 2) return Mathf.FloorToInt(originalCost * 0.5f); // 3단계: 마나 50% 감소
        return originalCost;
    }

    // [기획 반영] 시간 속성의 소라는 역상성(예: Normal)에 맞으면 추가 피해 및 정신력 감소
    public override bool TakeDamage(int incomingDamage, ElementType attackElement = ElementType.Normal, CharacterStats attacker = null, Vector2? knockbackDirection = null, float knockbackPower = 0f)
    {
        int finalDamage = incomingDamage;
        if (fairyStage > 0 && attackElement == ElementType.Normal) // 기획에 따라 상성 정의 필요    //************* 추후 수정
        {
            finalDamage = Mathf.FloorToInt(incomingDamage * 1.5f); // 1.5배 피해
            LoseMental(5); // 상성에 맞으면 정신력도 깎임
            Debug.Log("[상성 피해] 요정화 상태에서 역상성 공격을 받아 피해가 증가합니다!");
        }

        return base.TakeDamage(finalDamage, attackElement, attacker, knockbackDirection, knockbackPower);
    }

    public override float GetSpeedMultiplier()
    {
        return (currentFatigue >= 30) ? 0.5f : 1.0f; // 피로도 30 이상이면 이속 0.5배
    }

    protected override void PrepareRigidbodyForKnockback(Rigidbody2D rb)
    {
        if (fairyStage == 1 || LastAttacker is NPC) rb.linearVelocity = Vector2.zero; // 비행 중엔 기존 비행 속도까지 완전히 리셋
        else base.PrepareRigidbodyForKnockback(rb);
    }

    protected override Vector2 ComputeKnockbackForce(Vector2 direction, float power)
    {
        Debug.Log($"[넉백 진단] LastAttacker = {(LastAttacker != null ? LastAttacker.GetType().Name : "null")}");
        if (fairyStage == 1 || LastAttacker is NPC) return direction.normalized * power; // 순수하게 맞은 반대 방향으로만, 상승 편향 없음
        return base.ComputeKnockbackForce(direction, power);
    }

    #region 소라 고유 시스템 (시간결정체, 정신력, 피로도)
    public void CollectTimeCrystal()
    {
        timeCrystals++;
        Debug.Log($"[시간 결정체] 획득! 소라의 기억이 돌아옵니다. (현재: {timeCrystals}개)");
        GainExperience(500);
        CallSpecialStatChanged();
    }

    public void LoseMental(int amount)
    {
        currentMental = Mathf.Max(0, currentMental - amount);
        CallSpecialStatChanged();
        if (currentMental < 30) Debug.Log("[소라] 정신력 붕괴! 환영이 보입니다.");
    }

    // 2번: 피로도 관련 함수
    public void IncreaseFatigue(int amount)
    {
        currentFatigue = Mathf.Min(maxFatigue, currentFatigue + amount);
        CallSpecialStatChanged();
    }
    public void RecoverFatigue(int amount)
    {
        currentFatigue = Mathf.Max(0, currentFatigue - amount);
        CallSpecialStatChanged(); // UI 갱신
    }

    #endregion

    protected override void Die()
    {
        Debug.Log("소라 사망. 타임루프(회귀) 발동!");
    }

}
