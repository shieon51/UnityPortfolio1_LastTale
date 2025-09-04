using System;
using UnityEngine;

public class PlayerState : Singleton<PlayerState>
{
    [Header("레벨 및 경험치")]
    public int level = 1;
    public int experience = 0;
    public int experienceToNextLevel = 100; // 레벨업에 필요한 경험치

    [Header("체력 및 마력")]
    public int maxHealth = 100;
    public int currentHealth;

    public int maxMana = 50;
    public int currentMana;

    [Header("피로도 시스템")]
    public int maxFatigue = 100;
    public int currentFatigue; // 높을수록 피곤함

    public event Action OnStatsChanged; // 상태 변화 이벤트 -> UIManager에서 체력바 업데이트
    public event Action<int> OnFatigueChanged; //피로도 변화 이벤트 -> PlayerController에서 확인(이동속도 감소)

    private void Awake()
    {
        currentHealth = maxHealth; //hp
        currentMana = maxMana; //mp
        currentFatigue = 0; //피로도
    }

    public void GainExperience(int amount) //경험치 획득
    {
        experience += amount;
        while (experience >= experienceToNextLevel)
        {
            LevelUp();
        }
        OnStatsChanged?.Invoke(); // ->ui 경험치 바 갱신
    }

    private void LevelUp() //레벨업
    {
        experience -= experienceToNextLevel;
        level++;
        experienceToNextLevel += 50; //다음 레벨까지 경험치 총량 증가
        maxHealth += 10; //max 체력 증가 (임시)
        maxMana += 5; //max 마나 증가 (임시)
        currentHealth = maxHealth; //모두 회복
        currentMana = maxMana;

        Debug.Log($"레벨 업! 현재 레벨: {level}");
    }

    public void TakeDamage(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (currentHealth == 0) Die();
        OnStatsChanged?.Invoke();
    }

    public void Heal(int amount) //** 마나 이용해 -> 체력 회복으로 바꾸기
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnStatsChanged?.Invoke();
    }

    public void FullHP()
    {
        currentHealth = maxHealth;
        OnStatsChanged?.Invoke();
    }

    public void UseMana(int amount) //마나 사용(스킬 사용, 세이브/로드 시 차감, 힐로 사용 가능)
    {
        currentMana = Mathf.Max(0, currentMana - amount);
        OnStatsChanged?.Invoke();
    }

    public void RecoverMana(int amount) //마나 회복 (수련 시, 아이템 사용 등)
    {
        currentMana = Mathf.Min(maxMana, currentMana + amount);
        OnStatsChanged?.Invoke();
    }

    public void IncreaseFatigue(int amount) //피로도 증가 (모든 이벤트별로 적용)
    {
        currentFatigue = Mathf.Min(maxFatigue, currentFatigue + amount);
        OnStatsChanged?.Invoke();
        OnFatigueChanged?.Invoke(currentFatigue);
    }

    public void RecoverFatigue(int amount) //피로도 감소(잠자기)
    {
        currentFatigue = Mathf.Max(0, currentFatigue - amount);
        OnStatsChanged?.Invoke();
        OnFatigueChanged?.Invoke(currentFatigue);
    }

    ///--------------------- 나중에 효율적으로 처리
    //public void SleepEvent(int amount)
    //{
    //
    //}


    private void Die()
    {
        Debug.Log("플레이어가 사망했습니다.");
        // 추가적인 사망 처리
    }

}
