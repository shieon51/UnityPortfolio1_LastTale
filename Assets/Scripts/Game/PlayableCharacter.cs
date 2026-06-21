using System;
using UnityEngine;

// 추상 클래스(abstract): 이 클래스 자체는 객체로 생성할 수 없고, 반드시 상속받아 구현해야 함.
public abstract class PlayableCharacter : CharacterStats
{
    [Header("Playable Common - Progression")]
    public int experience = 0;
    public int experienceToNextLevel = 100; // 레벨업에 필요한 경험치

    [Header("Playable Common - Fairy Mode")]
    public int fairyStage = 0;        // 요정화 단계 (0 = 해제, 1~3단계)
    protected float fairyTimer = 0f;    // 요정화 유지 시간 체크용

    // UI 업데이트용 공통 이벤트
    public event Action OnProgressionChanged;
    public event Action OnSpecialStatChanged; // 피로도, 정신력 등 캐릭터 고유 스탯 UI 갱신용

    //// 2. 이벤트 정의 (기존 코드 유지 및 확장)
    //public event Action OnStatsChanged; // HP, MP, 경험치, 정신력 등 일반 UI 업데이트 -> UIManager에서 체력바 업데이트 // ?
    //public event Action<int> OnFatigueChanged; // 피로도 변화 이벤트 -> PlayerController에서 확인(이동속도 감소)


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
        while (experience >= experienceToNextLevel)
        {
            LevelUp();
        }
        CallProgressionChanged();
    }

    private void LevelUp() //레벨업
    {
        experience -= experienceToNextLevel;
        level++;
        experienceToNextLevel += 50; //다음 레벨까지 경험치 총량 증가

        maxHealth += 10; //max 체력 증가 (임시)
        maxMana += 5; //max 마나 증가 (임시)

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
    public override void TakeDamage(int incomingDamage, ElementType attackElement = ElementType.Normal)
    {
        base.TakeDamage(incomingDamage, attackElement);

        // 만약 방금 맞아서 넉백 상태(isKnockedBack)가 되었다면 공격 모션 강제 취소
        if (isKnockedBack)
        {
            PlayerAttack attackScript = GetComponentInChildren<PlayerAttack>();
            if (attackScript != null)
            {
                attackScript.CancelAttack();
            }

            // 플레이어 피격 애니메이션 트리거 (필요 시)
            // Animator anim = GetComponentInChildren<Animator>();
            // if (anim != null) anim.SetTrigger("Hit");
        }

        CallProgressionChanged(); // UI 갱신 헬퍼
    }

}
