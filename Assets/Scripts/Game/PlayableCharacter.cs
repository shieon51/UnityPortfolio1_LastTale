using System;
using UnityEngine;

// 추상 클래스(abstract): 이 클래스 자체는 객체로 생성할 수 없고, 반드시 상속받아 구현해야 함.
public abstract class PlayableCharacter : CharacterStats
{
    [Header("Playable Common - Progression")]
    public int experience = 0;
    public int experienceToNextLevel = 100; // 레벨업에 필요한 경험치

    [Header("Playable Common - Fairy Mode")]
    public int fairyStage = 0;        // 0 = 변신 안 함, 1 = 비행형, 2 = 강제 발동
    protected float fairyTimer = 0f;  // 요정화 유지 체크용

    [Header("Level Up")]
    [Tooltip("레벨 1에서 다음 레벨까지 필요한 경험치 (리셋 시에도 사용)")]
    public int baseExpToNextLevel = 100;
    [Tooltip("CSV에 없는 레벨일 때의 폴백 증가량")]
    public int fallbackMaxHealthGain = 10;
    public int fallbackMaxManaGain = 5;
    public int fallbackExpGrowth = 50;

    // UI 업데이트용 공통 이벤트
    public event Action OnProgressionChanged;
    public event Action OnSpecialStatChanged; // 피로도, 정신력 등 캐릭터 고유 스탯 UI 갱신용

    // 영혼 레벨 (경험한 최고 레벨) // ?
    public int highestLevelReached { get; protected set; } = 1;

    // 범용 특수 스탯(피로도, 신성력 등) 프로퍼티 정의
    // 자식 클래스(SoraStats, LielStats)가 무조건 이 값을 어떻게 줄지 정의해야 함
    public abstract bool HasSpecialStat { get; }
    public abstract float SpecialStatPercentage { get; }
    public abstract string SpecialStatText { get; }

    // 추상 메서드: 자식 클래스(소라, 리엘 등)가 무조건 각자의 방식으로 구현해야 하는 함수들
    public abstract void OnPossessed();   // 이 캐릭터에 빙의했을 때 발생할 일
    public abstract void OnUnpossessed(); // 이 캐릭터에서 빠져나갈 때 발생할 일
    protected abstract void HandleSpecialMechanics(); // 업데이트문에서 돌릴 고유 시스템

    protected override void Awake()
    {
        base.Awake(); // CharacterStats의 Awake 호출 (HP, MP 초기화)
    }

    protected virtual void Update()
    {
        HandleSpecialMechanics(); // 각 캐릭터의 고유 로직(마나리젠, 패널티 등) 실행
    }

    // 편의성을 위한 풀피/풀마나 회복 함수 (Heal은 부모의 함수를 그대로 씀)
    public void FullHP()
    {
        Heal(maxHealth); // 부모의 Heal 호출 -> 자동으로 이벤트 발생
    }

    public void FullMP()
    {
        RecoverMana(maxMana); // 부모의 RecoverMana 호출 -> 자동으로 이벤트 발생
    }

    // 경험치 및 레벨업 (공통 로직)
    public void GainExperience(int amount)
    {
        experience += amount;

        // ★ experienceToNextLevel이 0 이하로 잘못 설정되면 while이 영원히 돌 수 있다
        int guard = 0;
        while (experienceToNextLevel > 0 && experience >= experienceToNextLevel && guard++ < 100)
        {
            LevelUp();
        }
        if (guard >= 100) Debug.LogError($"[{name}] 레벨업 루프 안전장치 작동 — experienceToNextLevel 값을 확인하세요");

        CallProgressionChanged();
    }

    private void LevelUp() //레벨업
    {
        experience -= experienceToNextLevel;
        level++;
        highestLevelReached = Mathf.Max(highestLevelReached, level); // 영혼 레벨 갱신(최고 도달 레벨)

        var data = LevelDataManager.Instance?.GetLevelData(level);
        if (data != null)
        {
            maxHealth = data.maxHealth;
            maxMana = data.maxMana;
            experienceToNextLevel = data.expToNextLevel;
        }
        else // CSV에 아직 없는 레벨(최대치 초과 등) — 기존 방식으로 폴백
        {
            maxHealth += fallbackMaxHealthGain;
            maxMana += fallbackMaxManaGain;
            experienceToNextLevel += fallbackExpGrowth;
        }

        Heal(maxHealth); // 레벨업 시 풀피 회복

        Debug.Log($"[Level Up] 현재 레벨: {level}");
    }

    // 자식 클래스에서 UI 이벤트를 호출할 수 있게 해주는 헬퍼 함수
    protected void CallProgressionChanged() => OnProgressionChanged?.Invoke();
    protected void CallSpecialStatChanged() => OnSpecialStatChanged?.Invoke();


    //private void Die()
    //{
    //    Debug.Log("플레이어가 사망했습니다.");
    //    // 추가적인 사망 처리
    //}

    // PlayableCharacter.cs 안에 추가 (기존 TakeDamage가 있다면 덮어씌우기)
    public override bool TakeDamage(
        int incomingDamage,
        ElementType attackElement = ElementType.Normal,
        CharacterStats attacker = null,
        Vector2? knockbackDirection = null,
        float knockbackPower = 0f,
        Vector2? attackOriginOverride = null,
        bool piercesDodge = false)
    {
        bool applied = base.TakeDamage(incomingDamage, attackElement, attacker, knockbackDirection, knockbackPower, attackOriginOverride, piercesDodge); // ★ B-23: 공격 시작 위치/회피 관통 여부까지 전부 그대로 전달

        if (applied && isKnockedBack)
        {
            GetComponentInChildren<PlayerCombat>()?.CancelAttack();
        }

        return applied;
    }

    // 스텟 리셋
    public void ResetProgression(int baseLevel, int baseMaxHealth, int baseMaxMana)
    {
        level = baseLevel;
        experience = 0;
        experienceToNextLevel = baseExpToNextLevel;
        maxHealth = baseMaxHealth;
        maxMana = baseMaxMana;
        currentHealth = maxHealth;
        currentMana = maxMana;
        CallProgressionChanged();
    }
}