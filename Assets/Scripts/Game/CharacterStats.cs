using System;
using UnityEngine;

// 원소('영' 종류) 타입 정의 
public enum ElementType { Normal, Light, Dark, Fire, Water, Wood, Wind, Spacetime }

// [CharacterStats.cs] 캐릭터 공통 스탯 베이스
public class CharacterStats : MonoBehaviour
{
    [Header("Base Stats")]
    public int level = 1;
    public int maxHealth = 100;
    public int currentHealth;
    public int maxMana = 50;
    public int currentMana;

    [Header("Combat Stats")] // 공격력, 방어력, 민첩성
    public Stat attack = new Stat(10);
    public Stat defense = new Stat(5);
    public Stat agility = new Stat(5); // 회피 판정 및 공격 예고 타이밍에 사용

    [Header("Attribute")] // 캐릭터 고유 원소 타입
    public ElementType currentElement = ElementType.Normal;

    [Header("Combat States")]
    public bool isGuarding = false; // 현재 방어 키를 꾹 누르고 있는지 여부

    // 부모에서 선언된 이벤트 (부모만 쏠 수 있음)
    public event Action OnHealthChanged;
    public event Action OnManaChanged;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
    }

    public virtual void TakeDamage(int incomingDamage, ElementType attackElement = ElementType.Normal)
    {
        int finalDamage = incomingDamage;

        // 1. 방어 태세(Guard) 계산
        if (isGuarding)
        {
            // 방어 중일 땐 방어력의 효율이 증가 (예: 방어력의 2배 적용)
            int effectiveDefense = defense.GetValue() * 2;
            finalDamage = Mathf.Max(1, finalDamage - effectiveDefense); // (최소 1은 들어감)
            Debug.Log($"[Guard] 방어 성공! 데미지 감소: {incomingDamage} -> {finalDamage}");
        }
        else
        {
            // 무방비 상태일 땐 일반 방어력 적용
            finalDamage = Mathf.Max(1, finalDamage - defense.GetValue());
        }

        // 2. 원소 상성 연산 (추후 상성표에 따라 증감율 적용 가능)
        // if (attackElement == ElementType.Water && currentElement == ElementType.Fire) finalDamage = (int)(finalDamage * 1.5f);

        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        OnHealthChanged?.Invoke();

        Debug.Log($"{gameObject.name}가 {finalDamage} 데미지를 받았습니다. (잔여 HP: {currentHealth})");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // 체력 회복
    public virtual void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke();
    }

    // 마나 사용
    public virtual void UseMana(int amount)
    {
        currentMana = Mathf.Max(0, currentMana - amount);
        OnManaChanged?.Invoke();
    }

    // 마나 회복
    public virtual void RecoverMana(int amount)
    {
        currentMana = Mathf.Min(maxMana, currentMana + amount);
        OnManaChanged?.Invoke();
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} 사망!");
    }
}
