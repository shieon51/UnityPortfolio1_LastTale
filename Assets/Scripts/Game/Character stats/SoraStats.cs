using UnityEngine;

// PlayableCharacter를 상속받는 1부 전용 주인공 '소라'
public class SoraStats : PlayableCharacter
{
    [Header("Sora Exclusives - Meta Stats")] //피로도, 정신력 스탯
    public int maxFatigue = 100;
    public int currentFatigue = 0;
    public int maxMental = 100;
    public int currentMental = 100;

    [Header("Sora Exclusives - Time Loop")] // 시간결정체
    public int timeCrystals = 0;


    // 소라의 특수 스탯은 '피로도'임을 UI에게 알려줌
    public override bool HasSpecialStat => true;
    public override float SpecialStatPercentage => (float)currentFatigue / maxFatigue;
    public override string SpecialStatText => $"{currentFatigue}/{maxFatigue}";


    protected override void Awake()
    {
        base.Awake();
        currentElement = ElementType.Spacetime; // 소라 전용 속성
        currentMental = maxMental;
    }

    // 플레이어가 소라를 조종하기 시작할 때 호출됨
    public override void OnPossessed()
    {
        Debug.Log("소라의 시점으로 플레이를 시작합니다.");
        // UI 매니저에게 소라 전용 UI(정신력 바, 피로도 바)를 켜라고 명령
    }

    public override void OnUnpossessed()
    {
        Debug.Log("소라의 시점에서 벗어납니다.");
    }

    // 소라만의 고유 업데이트 로직 (마나 리젠, 요정화 패널티)
    protected override void HandleSpecialMechanics()
    {
        // 1. 시간 결정체에 비례한 마나 리젠 로직
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

    #region 소라 고유 시스템 (결정체, 정신력, 피로도)
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
