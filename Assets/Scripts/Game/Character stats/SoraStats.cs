using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PlayableCharacter를 상속받는 1부 전용 주인공 '소라'
public class SoraStats : PlayableCharacter, IFormStageProvider, IActionLockSource
{
    // ===============================================================
    // 요정화 단계 (fairyStage) — 화면 표시와 내부 값이 같다
    //   0 = 변신 안 함 (HUD 점 모두 꺼짐)
    //   1 = 비행형     (HUD "1단계")
    //   2 = 강제 발동  (HUD "2단계")
    // TargetFormStage : 변신 연출 중 참고용 목표 단계. 연출이 끝나야 fairyStage가 바뀐다
    // _isTransforming : 연출 재생 중. true인 동안 IActionLockSource로 조작 잠김
    // ===============================================================

    public const int FairyStageNone = 0;
    public const int FairyStageFlight = 1;
    public const int FairyStageAwakened = 2;

    private IPlayerMotor _motor;

    [Header("Form Change (요정화)")] // 시즌 1에만 쓸지, 아님 다른 캐릭터도 해당하는가?
    [Tooltip("기획 반영: 변신 딜레이 0.5초")]
    public float formTransformDuration = 0.5f;

    public int FormStage => fairyStage;
    public int TargetFormStage { get; private set; } = FairyStageNone;
    public bool IsFlightForm => fairyStage == FairyStageFlight;
    public bool IsTransforming => _isTransforming;
    public bool IsLocked => _isTransforming;      // IActionLockSource

    public event Action OnFormTransformStarted;
    public event Action<int> OnFormStageChanged;

    // 정신력 변화 (이전 값, 현재 값) — UI가 감소 애니메이션에 사용
    public event Action<int, int> OnMentalChanged;
    public bool IsMentalDanger => currentMental < mentalDangerThreshold;

    // 완전 리셋용 기본값 캐싱 
    private int _baseLevel, _baseMaxHealth, _baseMaxMana;

    [Header("Time Loop")]
    public int loopCount = 0; // 회귀 횟수 (나중에 실제 회귀 시스템과 연동)

    [Header("Sora Exclusives - Meta Stats")] //피로도, 정신력 스탯
    public int maxFatigue = 100;
    public int currentFatigue = 0;
    public int maxMental = 100;
    public int currentMental = 100;

    [Header("피로도 페널티")]
    [Tooltip("이 값 이상이면 이동속도가 느려진다. HUD의 경고 구간도 같은 값을 쓴다")]
    public int fatigueSlowThreshold = 30;
    [Range(0.1f, 1f)]
    [Tooltip("느려졌을 때의 이동속도 배율")]
    public float fatigueSlowMultiplier = 0.5f;

    [Header("정신력")]
    [Tooltip("이 값 미만이면 위험 구간 (환영·결정체 코어 흔들림 연출)")]
    public int mentalDangerThreshold = 30;

    [Header("요정화 유지 비용")]
    [Tooltip("정신력이 깎이는 주기(초)")]
    public float fairyMentalDrainInterval = 5f;
    [Tooltip("주기마다 깎이는 정신력 = 이 값 × 현재 단계")]
    public int fairyMentalDrainPerStage = 1;
    [Tooltip("단계별 마나 소모 배율. 0번=변신 안 함, 1번=비행형, 2번=강제 발동")]
    public float[] fairyManaCostMultipliers = { 1f, 0.8f, 0.5f };

    [Header("시간결정체")]
    [Tooltip("결정체 하나를 얻을 때 주는 경험치")]
    public int timeCrystalExpReward = 500;

    [Header("Sora Exclusives - Time Loop")] // 시간결정체
    public int timeCrystals = 0;

    [Header("Form Change (요정화)")] // 변신 후 쿨타임
    public float formToggleCooldown = 0.3f;
    private float _lastTransformEndTime = -10f;

    private bool _isTransforming = false; // 변신 딜레이 중인지 체크

    [Header("역상성 피격 (요정화 상태)")]
    [Tooltip("요정화 상태에서 이 속성의 공격을 받으면 역상성 피해로 처리 (상성 기획 확정 전 임시 기준)")]
    public ElementType reverseElement = ElementType.Normal; // ★ 하드코딩 제거
    [Tooltip("역상성 피격 시 피해 배율")]
    public float reverseElementDamageMultiplier = 1.5f;     // ★ 하드코딩 제거
    [Tooltip("역상성 피해가 실제로 적용됐을 때 깎이는 정신력")]
    public int reverseElementMentalLoss = 5;                 // ★ 하드코딩 제거

    // 소라 회피 '틈입'
    private PlayerDodge _dodge;
    protected override bool IsDodgeInvincible => _dodge != null && _dodge.IsDodging;

    // 플로팅 텍스트 오프셋
    [Header("알림 표시")]
    [Tooltip("플로팅 텍스트가 뜰 높이")]
    public float notificationHeightOffset = 1.5f;

    // 소라의 특수 스탯은 '피로도'임을 UI에게 알려줌
    public override bool HasSpecialStat => true;
    public override float SpecialStatPercentage => (float)currentFatigue / maxFatigue;
    public override string SpecialStatText => $"{currentFatigue}/{maxFatigue}";

    [Header("Hostility (실제 트리거 조건은 추후 스토리/정신력 시스템과 연동 예정)")]
    public bool IsHostileState = false; // npc 공격 가능한지?

    // 소라 본인이 느끼는 친밀도, 플레이어에겐 비공개
    private Dictionary<string, int> _soraPersonalBond = new();

    // ★ 이미 사용한 증가 키. 같은 이벤트를 다시 겪어도 친밀도가 또 오르지 않게 한다
    private HashSet<string> _usedBondKeys = new();

    public int GetPersonalBond(string npc) => _soraPersonalBond.TryGetValue(npc, out var v) ? v : 0;

    public void AddPersonalBond(string npc, int amount)
        => _soraPersonalBond[npc] = GetPersonalBond(npc) + amount;

    // ★ key는 이벤트마다 고유해야 한다 (예: "liel_first_rain")
    public bool AddPersonalBondOnce(string npc, int amount, string key)
    {
        if (string.IsNullOrEmpty(key)) { AddPersonalBond(npc, amount); return true; }
        if (!_usedBondKeys.Add(key)) return false;      // 이미 오른 적 있음
        AddPersonalBond(npc, amount);
        return true;
    }

    public bool HasUsedBondKey(string key) => _usedBondKeys.Contains(key);

    public float MentalRatio => (float)currentMental / maxMental;

    // 개인 친밀도 스냅샷/복원 관련 (디버그 용)
    public Dictionary<string, int> SnapshotPersonalBond() => new Dictionary<string, int>(_soraPersonalBond);
    public void RestorePersonalBond(Dictionary<string, int> s) { _soraPersonalBond.Clear(); if (s != null) foreach (var k in s) _soraPersonalBond[k.Key] = k.Value; }

    protected override void Awake()
    {
        base.Awake();
        currentElement = ElementType.Spacetime; // 소라 전용 속성
        currentMental = maxMental;
        _motor = GetComponent<IPlayerMotor>();
        _dodge = GetComponent<PlayerDodge>(); // ★ 추가 — 이게 빠져서 회피 무적이 전혀 작동 안 하고 있었음 // *

        _baseLevel = level;
        _baseMaxHealth = maxHealth;
        _baseMaxMana = maxMana;
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
        if (!GlobalActionLock.IsLocked
             && InputBindings.GetKeyDown(InputAction.Transform) && !_isTransforming && !isKnockedBack
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
        if (fairyStage > FairyStageNone)
        {
            fairyTimer += Time.deltaTime;
            if (fairyTimer >= fairyMentalDrainInterval)
            {
                fairyTimer = 0f;
                LoseMental(fairyMentalDrainPerStage * fairyStage);
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
        if (fairyManaCostMultipliers == null || fairyManaCostMultipliers.Length == 0) return originalCost;
        int index = Mathf.Clamp(fairyStage, 0, fairyManaCostMultipliers.Length - 1);
        return Mathf.FloorToInt(originalCost * fairyManaCostMultipliers[index]);
    }

    // [기획 반영] 시간 속성의 소라는 역상성(예: Normal)에 맞으면 추가 피해 및 정신력 감소
    public override bool TakeDamage(
        int incomingDamage,
        ElementType attackElement = ElementType.Normal,
        CharacterStats attacker = null,
        Vector2? knockbackDirection = null,
        float knockbackPower = 0f,
        Vector2? attackOriginOverride = null,
        bool piercesDodge = false)
    {
        // ★ B-23: 역상성 여부는 먼저 판정하되, 정신력 감소는 피해가 "실제로 적용된 뒤"에만 처리
        //   (기존엔 무적/회피/패링/완벽 방어로 막아도 정신력이 먼저 깎였음)
        bool isReverseElementHit = fairyStage > 0 && attackElement == reverseElement; // 기획에 따라 상성 정의 필요    //************* 추후 수정
        int finalDamage = isReverseElementHit
            ? Mathf.FloorToInt(incomingDamage * reverseElementDamageMultiplier)
            : incomingDamage;

        // ★ B-23: attackOriginOverride / piercesDodge 가 부모로 전달되지 않던 문제 수정
        bool applied = base.TakeDamage(finalDamage, attackElement, attacker, knockbackDirection, knockbackPower, attackOriginOverride, piercesDodge);

        if (applied && isReverseElementHit)
        {
            LoseMental(reverseElementMentalLoss);
            Debug.Log("[상성 피해] 요정화 상태에서 역상성 공격을 받아 피해가 증가합니다!");
        }
        return applied;
    }

    public override float GetSpeedMultiplier()
        => (currentFatigue >= fatigueSlowThreshold) ? fatigueSlowMultiplier : 1f;

    protected override void PrepareRigidbodyForKnockback(Rigidbody2D rb)
    {
        if (fairyStage == FairyStageFlight || LastAttacker is NPC) rb.linearVelocity = Vector2.zero; // 비행 중엔 기존 비행 속도까지 완전히 리셋
        else base.PrepareRigidbodyForKnockback(rb);
    }

    protected override Vector2 ComputeKnockbackForce(Vector2 direction, float power)
    {
        //Debug.Log($"[넉백 진단] LastAttacker = {(LastAttacker != null ? LastAttacker.GetType().Name : "null")}");
        if (fairyStage == FairyStageFlight || LastAttacker is NPC) return direction.normalized * power; // 순수하게 맞은 반대 방향으로만, 상승 편향 없음
        return base.ComputeKnockbackForce(direction, power);
    }

    #region 소라 고유 시스템 (시간결정체, 정신력, 피로도)
    public void CollectTimeCrystal()
    {
        timeCrystals++;
        Debug.Log($"[시간 결정체] 획득! 소라의 기억이 돌아옵니다. (현재: {timeCrystals}개)");
        FloatingTextManager.Instance?.ShowTimeCrystal(transform.position + Vector3.up * notificationHeightOffset);
        GainExperience(timeCrystalExpReward);
        CallSpecialStatChanged();
    }

    public void LoseMental(int amount) => ChangeMental(-Mathf.Abs(amount));

    // ★ 신규 — NPC의 위로·안정 행동으로 회복 (기획서 7-1)
    public void RecoverMental(int amount) => ChangeMental(Mathf.Abs(amount));

    private void ChangeMental(int delta)
    {
        int before = currentMental;
        currentMental = Mathf.Clamp(currentMental + delta, 0, maxMental);
        if (currentMental == before) return;

        CallSpecialStatChanged();
        OnMentalChanged?.Invoke(before, currentMental);

        if (before >= mentalDangerThreshold && IsMentalDanger)
            Debug.Log("[소라] 정신력 위험 구간 진입 — 환영이 보이기 시작한다");
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
        TimeLoopManager.Instance?.HandleDeath(); // ★ 추가 — 이게 빠져있었음
    }

    public void ResetProgression() // 디버그 하드리셋 전용
    {
        level = _baseLevel;
        highestLevelReached = _baseLevel;
        experience = 0;
        experienceToNextLevel = baseExpToNextLevel;
        maxHealth = _baseMaxHealth;
        maxMana = _baseMaxMana;
        currentHealth = maxHealth;
        currentMana = maxMana;
        CallProgressionChanged();
    }
}