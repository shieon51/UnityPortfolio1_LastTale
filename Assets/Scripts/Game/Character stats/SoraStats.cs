using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PlayableCharacter를 상속받는 1부 전용 주인공 '소라'
public class SoraStats : PlayableCharacter, IFormStageProvider, IActionLockSource, IRecordable   // ★ 기록 시스템 2단계 — 몸 층위로 등록
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
    private int _baseAttack, _baseDefense, _baseAgility;   // ★ 경로 3에서 되돌릴 시작 공·방·민

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
    {
        int before = GetPersonalBond(npc);
        _soraPersonalBond[npc] = before + amount;
        PlayerActionLog.Instance?.Record(RecordType.PersonalBondChange, npc, before, before + amount);   // ★
    }


    // ★ key는 이벤트마다 고유해야 한다 (예: "liel_first_rain")
    public bool AddPersonalBondOnce(string npc, int amount, string key)
    {
        if (string.IsNullOrEmpty(key)) { AddPersonalBond(npc, amount); return true; }
        if (!_usedBondKeys.Add(key)) return false;      // 이미 오른 적 있음
        PlayerActionLog.Instance?.Record(RecordType.BondKeyUsed, key, source: npc);   // ★ 사용한 키도 상태다
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

        _baseAttack = attack.BaseValue;      // ★ 일시 보정 제외한 기본값만 기억
        _baseDefense = defense.BaseValue;
        _baseAgility = agility.BaseValue;

        RecordSystem.Register(this, replaceExisting: true);   // ★ 소라는 씬마다 새로 생길 수 있어 최신 것으로 교체
        _willState = new WillState(this);                       // ★ 2-B — 의지·플레이어 층위는 어댑터로 따로
        _loopState = new LoopState(this);
        RecordSystem.Register(_willState, replaceExisting: true);
        RecordSystem.Register(_loopState, replaceExisting: true);
    }

    private void OnDestroy()
    {
        RecordSystem.Unregister(this);   // ★
        RecordSystem.Unregister(_willState);
        RecordSystem.Unregister(_loopState);
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
        int before = timeCrystals;
        timeCrystals++;
        PlayerActionLog.Instance?.Record(RecordType.TimeCrystalChange, RecordKeys.TimeCrystal, before, timeCrystals);   // ★
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

        PlayerActionLog.Instance?.Record(RecordType.MentalChange, RecordKeys.Mental, before, currentMental);   // ★
        CallSpecialStatChanged();
        OnMentalChanged?.Invoke(before, currentMental);

        if (before >= mentalDangerThreshold && IsMentalDanger)
            Debug.Log("[소라] 정신력 위험 구간 진입 — 환영이 보이기 시작한다");
    }

    // 2번: 피로도 관련 함수
    public void IncreaseFatigue(int amount) => ChangeFatigue(Mathf.Min(maxFatigue, currentFatigue + amount));
    public void RecoverFatigue(int amount) => ChangeFatigue(Mathf.Max(0, currentFatigue - amount));

    // ★ 피로도 변경의 단일 경로 — 기록과 UI 갱신을 한곳에서
    private void ChangeFatigue(int newValue)
    {
        int before = currentFatigue;
        currentFatigue = newValue;
        if (currentFatigue != before)
            PlayerActionLog.Instance?.Record(RecordType.FatigueChange, RecordKeys.Fatigue, before, currentFatigue);
        CallSpecialStatChanged(); // UI 갱신
    }

    // ★ 훈련·수련으로 공·방·민이 오를 때 쓰는 공통 진입점.
    //   노련미 보정을 함께 적용해, 한 번 도달해본 경지는 빠르게 되찾게 한다
    public int TrainStat(StatType type, int rawAmount)
    {
        if (rawAmount <= 0) return 0;

        int amount = Mathf.Max(1, Mathf.RoundToInt(rawAmount * CatchUpMultiplier));

        Stat target = type switch
        {
            StatType.Attack => attack,
            StatType.Defense => defense,
            StatType.Agility => agility,
            _ => null,
        };
        if (target == null) return 0;

        int before = target.BaseValue;
        target.AddBaseValue(amount);

        // ★ 기록장 태그 — key는 화면에 그대로 쓰이므로 짧게
        string label = type switch
        {
            StatType.Attack => "ATK",
            StatType.Defense => "DEF",
            StatType.Agility => "AGI",
            _ => type.ToString(),
        };
        // ★ 실제 기본값의 전후를 남긴다 (태그는 차이로 그리므로 표시는 그대로)
        PlayerActionLog.Instance?.Record(RecordType.StatGain, label, before, target.BaseValue);

        CallProgressionChanged();
        return amount;      // 실제로 오른 값 (기록장 태그에 그대로 쓴다)
    }

    #endregion

    protected override void Die()
    {
        Debug.Log("소라 사망.");

        // ★ 보스전 패배는 NPCManager가 흐름을 주관한다.
        //   (사망 연출 → 패배 대사 → 해설자 재도전 제안 → 선택에 따라 회귀)
        //   여기서 바로 회귀하면 패배 대사가 회귀 후에 뜨게 된다
        if (NPCManager.Instance != null && NPCManager.Instance.IsDefeatFlowActive)
        {
            Debug.Log("[소라] 보스전 패배 흐름으로 넘깁니다 — 회귀는 대사 이후에");
            return;
        }

        TimeLoopManager.Instance?.HandleDeath();
    }

    // ★ 닻 없이 사망했을 때(경로 3) — 몸만 시작 상태로 되돌린다.
    //   영혼 레벨(highestLevelReached)은 소라의 '의지'에 속하므로 유지한다
    public void ResetBodyForNewLoop()
    {
        level = _baseLevel;
        experience = 0;
        experienceToNextLevel = baseExpToNextLevel;
        maxHealth = _baseMaxHealth;
        maxMana = _baseMaxMana;

        attack.SetBaseValue(_baseAttack);     // ★ 훈련으로 오른 공·방·민도 시작 값으로 (기획서 11-3)
        defense.SetBaseValue(_baseDefense);
        agility.SetBaseValue(_baseAgility);

        currentHealth = 0;
        currentMana = 0;
        FullHP();                 // Heal/RecoverMana를 거쳐야 HUD가 갱신된다
        FullMP();
        CallProgressionChanged();
    }

    // ★ 2-C-2 — RestoreBodyFromAnchor(닻의 손 나열 필드로 몸을 되돌리던 함수)를 제거했다. 아래 ReadState가 대신한다

    // ---------------- IRecordable (★ 기록 시스템 2단계) ----------------
    // 이 클래스 자신은 몸 층위. 의지 층위는 WillState, 플레이어 층위(회차 수)는 LoopState 어댑터가 맡는다
    // 몸: 레벨, 최대 HP·MP, 경험치, 공·방·민 기본값, 피로도(2-B 결정), 현재 HP·MP(2026-10-09 결정)
    //   몸을 되돌리는 경로 2(닻)·3(파트 시작)이 RecordSystem.RestoreLayers로 이 덩어리를 읽는다.
    //   몸을 유지하는 경로 1은 몸 층위를 복원하지 않고, 남은 마나로 회복한다 (기획서 7-2)
    // 덩어리 안의 키. 바꾸지 않는다
    private const string StateKeyFatigue = "fatigue";
    public const string StateKeyLevel = "level";   // ★ 2-C-2 — 오버뷰 창이 닻 레벨을 읽으려고 공개
    private const string StateKeyMaxHealth = "max_health";
    private const string StateKeyMaxMana = "max_mana";
    private const string StateKeyExp = "exp";
    private const string StateKeyExpToNext = "exp_to_next";
    private const string StateKeyAttack = "attack_base";
    private const string StateKeyDefense = "defense_base";
    private const string StateKeyAgility = "agility_base";
    private const string StateKeyHealth = "health";   // ★ 현재 HP
    private const string StateKeyMana = "mana";       // ★ 현재 MP

    public string RecordId => RecordIds.SoraBody;
    public RecordLayer Layer => RecordLayer.Body;
    public int StateVersion => 1;

    public void WriteState(StateWriter writer)
    {
        writer.WriteInt(StateKeyLevel, level);
        writer.WriteInt(StateKeyMaxHealth, maxHealth);
        writer.WriteInt(StateKeyMaxMana, maxMana);
        writer.WriteInt(StateKeyExp, experience);
        writer.WriteInt(StateKeyExpToNext, experienceToNextLevel);
        writer.WriteInt(StateKeyAttack, attack.BaseValue);
        writer.WriteInt(StateKeyDefense, defense.BaseValue);
        writer.WriteInt(StateKeyAgility, agility.BaseValue);
        writer.WriteInt(StateKeyFatigue, currentFatigue);   // ★ 2-B
        writer.WriteInt(StateKeyHealth, currentHealth);     // ★
        writer.WriteInt(StateKeyMana, currentMana);         // ★
    }

    // ★ 경로 2(닻)·경로 3(파트 시작)의 몸 복원이 이 함수를 거친다 (2-C)
    public void ReadState(StateReader reader, int version)
    {
        level = reader.ReadInt(StateKeyLevel, level);
        maxHealth = reader.ReadInt(StateKeyMaxHealth, maxHealth);
        maxMana = reader.ReadInt(StateKeyMaxMana, maxMana);
        experience = reader.ReadInt(StateKeyExp, experience);
        experienceToNextLevel = Mathf.Max(1, reader.ReadInt(StateKeyExpToNext, experienceToNextLevel));

        attack.SetBaseValue(reader.ReadInt(StateKeyAttack, attack.BaseValue));
        defense.SetBaseValue(reader.ReadInt(StateKeyDefense, defense.BaseValue));
        agility.SetBaseValue(reader.ReadInt(StateKeyAgility, agility.BaseValue));
        currentFatigue = Mathf.Clamp(reader.ReadInt(StateKeyFatigue, currentFatigue), 0, maxFatigue);   // ★ 2-B

        // ★ HP·MP는 직접 대입하면 HUD가 갱신되지 않으므로 0으로 비운 뒤 Heal/RecoverMana를 거친다
        int health = Mathf.Clamp(reader.ReadInt(StateKeyHealth, currentHealth), 1, maxHealth);
        int mana = Mathf.Clamp(reader.ReadInt(StateKeyMana, currentMana), 0, maxMana);
        currentHealth = 0;
        currentMana = 0;
        Heal(health);
        RecoverMana(mana);

        CallProgressionChanged();
        CallSpecialStatChanged();   // ★ 피로도 HUD 갱신
    }

    private WillState _willState;
    private LoopState _loopState;

    // ★ 2-B — 소라의 의지 층위. 회귀로 되돌리지 않고 세이브에만 쓰인다
    private class WillState : IRecordable
    {
        private const string KeyMental = "mental";
        private const string KeyPersonalBond = "personal_bond";
        private const string KeyUsedBondKeys = "used_bond_keys";
        private const string KeySoulLevel = "soul_level";
        private const string KeyTimeCrystals = "time_crystals";

        private readonly SoraStats _owner;
        public WillState(SoraStats owner) { _owner = owner; }

        public string RecordId => RecordIds.SoraWill;
        public RecordLayer Layer => RecordLayer.Will;
        public int StateVersion => 1;

        public void WriteState(StateWriter writer)
        {
            writer.WriteInt(KeyMental, _owner.currentMental);
            writer.WriteIntMap(KeyPersonalBond, _owner._soraPersonalBond);
            writer.WriteStringList(KeyUsedBondKeys, _owner._usedBondKeys);
            writer.WriteInt(KeySoulLevel, _owner.highestLevelReached);
            writer.WriteInt(KeyTimeCrystals, _owner.timeCrystals);
        }

        public void ReadState(StateReader reader, int version)
        {
            int beforeMental = _owner.currentMental;
            _owner.currentMental = Mathf.Clamp(reader.ReadInt(KeyMental, beforeMental), 0, _owner.maxMental);
            _owner.RestorePersonalBond(reader.ReadIntMap(KeyPersonalBond));
            _owner._usedBondKeys.Clear();
            foreach (var key in reader.ReadStringList(KeyUsedBondKeys)) _owner._usedBondKeys.Add(key);
            _owner.highestLevelReached = reader.ReadInt(KeySoulLevel, _owner.highestLevelReached);
            _owner.timeCrystals = reader.ReadInt(KeyTimeCrystals, _owner.timeCrystals);

            // 하나씩 기록하지 않는다 — 복원은 회귀·불러오기 기록 하나로 남는다. HUD만 갱신한다
            _owner.CallSpecialStatChanged();
            if (_owner.currentMental != beforeMental) _owner.OnMentalChanged?.Invoke(beforeMental, _owner.currentMental);
        }
    }

    // ★ 2-B — 플레이어 층위: 회차 수. 전적이므로 어떤 경로에서도 절대 되돌리지 않는다
    private class LoopState : IRecordable
    {
        private const string KeyLoopCount = "loop_count";

        private readonly SoraStats _owner;
        public LoopState(SoraStats owner) { _owner = owner; }

        public string RecordId => RecordIds.SoraLoop;
        public RecordLayer Layer => RecordLayer.Player;
        public int StateVersion => 1;

        public void WriteState(StateWriter writer) => writer.WriteInt(KeyLoopCount, _owner.loopCount);
        public void ReadState(StateReader reader, int version) => _owner.loopCount = reader.ReadInt(KeyLoopCount, _owner.loopCount);
    }

    // ★ 2-B — 디버그 도구 전용. 회차 수를 직접 바꾸되 기록을 남긴다 (CLAUDE.md: 디버그 수정도 기록)
    public void SetLoopCountForDebug(int value)
    {
        int before = loopCount;
        if (before == value) return;
        loopCount = value;
        PlayerActionLog.Instance?.Record(RecordType.DebugEdit, RecordKeys.LoopCount, before, value, source: RecordKeys.DebugSource);
    }

    public void ResetProgression() // 디버그 하드리셋 전용
    {
        level = _baseLevel;
        highestLevelReached = _baseLevel;
        experience = 0;
        experienceToNextLevel = baseExpToNextLevel;
        maxHealth = _baseMaxHealth;
        maxMana = _baseMaxMana;
        attack.SetBaseValue(_baseAttack);     // ★ 하드 리셋도 공·방·민을 함께 초기화
        defense.SetBaseValue(_baseDefense);
        agility.SetBaseValue(_baseAgility);
        currentHealth = maxHealth;
        currentMana = maxMana;
        CallProgressionChanged();
    }
}