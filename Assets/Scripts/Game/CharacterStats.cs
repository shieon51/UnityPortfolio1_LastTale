using System;
using System.Collections;
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

    [Header("I-Frames")]
    public float invincibilityDuration = 0.2f; // 맞은 후 0.2초간 무적
    protected float lastHitTime = -1f;

    // 부모에서 선언된 이벤트 (부모만 쏠 수 있음)
    public event Action OnHealthChanged;
    public event Action OnManaChanged;

    // 💡 넉백 상태인지 확인하는 변수 추가
    public bool isKnockedBack { get; protected set; } = false;

    // 💡 슈퍼아머 변수 추가 (공격 중일 때 true가 됨) -> 공격 모션 중에는 대미지는 받되 밀려나지 않는 상태
    public bool isSuperArmor = false;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
    }

    public virtual void TakeDamage(int incomingDamage, ElementType attackElement = ElementType.Normal)
    {
        // 💡 무적 시간 체크: 마지막 맞은 시간 + 무적 시간보다 현재 시간이 커야만 데미지 인정
        if (Time.time < lastHitTime + invincibilityDuration)
        {
            return; // 무적 시간 중이면 데미지 무시!
        }

        lastHitTime = Time.time; // 마지막 맞은 시간 갱신


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

    public virtual void ApplyKnockback(Vector2 direction, float knockbackPower, float knockbackTime = 0.2f)
    {
        if (isSuperArmor) return; // 💡 슈퍼아머(공격중)면 넉백 무시!

        StartCoroutine(KnockbackRoutine(direction, knockbackPower, knockbackTime));
    }


    private IEnumerator KnockbackRoutine(Vector2 direction, float power, float duration)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            isKnockedBack = true;

            // 초기 X축 속도를 초기화하고 밀어냄
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            Vector2 force = new Vector2(direction.x, 0.5f).normalized * power;
            rb.AddForce(force, ForceMode2D.Impulse);

            yield return new WaitForSeconds(duration);

            // 밀려난 후 미끄러짐 방지 (Y축 중력은 유지!)
            //if (rb != null) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

            isKnockedBack = false;
        }
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} 사망!");
    }
}
