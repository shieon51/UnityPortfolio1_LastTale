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

    [Header("Guard/Parry")]
    public bool isGuarding = false; // 현재 방어 키를 꾹 누르고 있는지 여부
    [Tooltip("가드를 시작한 순간부터 이 시간(초) 안에 맞으면 완벽 방어(패링) 판정")]
    public float parryWindowDuration = 0.15f;
    private float _guardStartTime = -10f;

    [Header("Groggy")] // 그로기
    public bool IsGroggy { get; private set; }
    public event System.Action OnGroggyStarted;
    public event System.Action OnGroggyEnded;
    private Coroutine _groggyRoutine;

    [Header("I-Frames")]
    public float invincibilityDuration = 0.2f; // 맞은 후 0.2초간 무적
    protected float lastHitTime = -1f;

    [Header("Knockback")]
    [Tooltip("피격 시 행동불능(넉백) 유지 시간(초). Hit 애니메이션 클립 길이에 맞춰 조정하세요.")]
    public float knockbackStunDuration = 0.3f;

    // 최근에 나를 공격한 대상 (W 스킬의 "최근 피격 대상 우선" 타겟팅에 사용)
    public CharacterStats LastAttacker { get; private set; }

    // 부모에서 선언된 이벤트 (부모만 쏠 수 있음)
    public event Action OnHealthChanged;
    public event Action OnManaChanged;

    // 총 데미지량 계산 관련 (보스전)
    public event Action<int, CharacterStats> OnDamageTaken; // (데미지량, 공격자)

    // 방어자 기준 이벤트 (누구를 막았는지)
    public event Action<CharacterStats> OnParrySuccess;

    // 맞을 때마다(중첩 포함) 매번 발행
    public event Action OnKnockbackApplied;

    private Coroutine _knockbackRoutine;

    // 넉백 상태인지 확인하는 변수 추가
    public bool isKnockedBack { get; protected set; } = false;

    // 슈퍼아머 변수 추가 (공격 중일 때 true가 됨) -> 공격 모션 중에는 대미지는 받되 밀려나지 않는 상태
    public bool isSuperArmor = false;

    public void StartGuard() { isGuarding = true; _guardStartTime = Time.time; }
    public void StopGuard() => isGuarding = false;
    private bool IsInParryWindow => isGuarding && (Time.time - _guardStartTime) <= parryWindowDuration;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
    }

    public virtual void TakeDamage(int incomingDamage, ElementType attackElement = ElementType.Normal, CharacterStats attacker = null) // 공격자 레벨차 보정 수식 필요 시 마지막 인자 채워넣기
    {
        // 무적 시간 체크: 마지막 맞은 시간 + 무적 시간보다 현재 시간이 커야만 데미지 인정
        if (Time.time < lastHitTime + invincibilityDuration) return; // 무적 시간 중이면 데미지 무시

        if (IsInParryWindow && attacker != null && TryResolveParry(attacker))
        {
            lastHitTime = Time.time;
            OnParrySuccess?.Invoke(attacker);
            FloatingTextManager.Instance?.ShowParry(transform.position + Vector3.up * 1f); 
            SoundManager.Instance?.PlaySFX("parry_success"); // **
            float groggyDuration = CombatFormulaService.Instance.CalculateGroggyDuration(attacker, this);
            attacker.ApplyGroggy(groggyDuration);
            return; // 데미지 0, 완전 무효화
        }

        lastHitTime = Time.time; // 마지막 맞은 시간 갱신
        if (attacker != null) LastAttacker = attacker;

        int finalDamage = ComputeFinalDamage(incomingDamage, attackElement, attacker);

        if (isGuarding && finalDamage <= 0)
        {
            FloatingTextManager.Instance?.ShowGuard(transform.position + Vector3.up * 1f);
            SoundManager.Instance?.PlaySFX("guard_perfect");
            return; // ★ 완벽 방어 — 체력/이펙트 전부 스킵
        }

        // --- 3. 실제로 데미지가 들어가는 모든 경우 (방어 관통 포함) ---
        if (isGuarding)
            SoundManager.Instance?.PlaySFX("guard_break"); // ★ 사운드 위치 ③ (방어했지만 뚫림)
        else
            SoundManager.Instance?.PlaySFX("hit_generic"); // ★ 사운드 위치 ④ (무방비로 맞음)

        GetComponentInChildren<HitFlashController>()?.Flash();
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        OnHealthChanged?.Invoke();
        OnDamageTaken?.Invoke(finalDamage, attacker);
        FloatingTextManager.Instance?.ShowDamage(finalDamage, transform.position + Vector3.up * 1f); 

        Debug.Log($"{gameObject.name}가 {finalDamage} 데미지를 받았습니다. (잔여 HP: {currentHealth})");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected int ComputeFinalDamage(int incomingDamage, ElementType attackElement, CharacterStats attacker)
    {
        if (CombatFormulaService.Instance == null)
        {
            // 씬에 서비스가 없어도 게임이 죽지 않도록 하는 안전 폴백 (기존 로직과 동일)
            int fallbackDefense = isGuarding ? defense.GetValue() * 2 : defense.GetValue();
            return Mathf.Max(1, incomingDamage - fallbackDefense);
        }

        var ctx = new CombatContext
        {
            IncomingDamage = incomingDamage,
            Attacker = attacker,
            Defender = this,
            AttackElement = attackElement,
            IsGuarding = isGuarding,
        };
        return CombatFormulaService.Instance.CalculateDamage(ctx);
    }

    // 도착 순간처럼, '맞아서' 생기는 무적이 아니라 능동적으로 무적을 거는 경우를 위한 헬퍼.
    // 기존 lastHitTime/invincibilityDuration 메커니즘을 그대로 재사용 (새 필드 없음).
    public void GrantTemporaryInvincibility(float duration)
    {
        lastHitTime = Time.time + duration - invincibilityDuration;
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

    // 속도
    public virtual float GetSpeedMultiplier()
    {
        return 1.0f; // 기본은 1배속
    }

    // 요정화 등에 따른 마나 사용 효율 계산 (기본은 그대로 반환)
    public virtual int CalculateManaCost(int originalCost) 
    { 
        return originalCost; 
    }

    // 그로기
    public void ApplyGroggy(float duration)
    {
        if (_groggyRoutine != null) StopCoroutine(_groggyRoutine);
        _groggyRoutine = StartCoroutine(GroggyRoutine(duration));
    }

    private IEnumerator GroggyRoutine(float duration)
    {
        IsGroggy = true;
        OnGroggyStarted?.Invoke();
        yield return new WaitForSeconds(duration);
        IsGroggy = false;
        OnGroggyEnded?.Invoke();
        _groggyRoutine = null;
    }

    // 기본(플레이어): 타이밍만 맞으면 항상 성공. NPC는 아래에서 확률 기반으로 오버라이드.
    protected virtual bool TryResolveParry(CharacterStats attacker) => true;


    public virtual void ApplyKnockback(Vector2 direction, float knockbackPower, float? knockbackTime = null)
    {
        if (isSuperArmor) return;

        float duration = knockbackTime ?? knockbackStunDuration;
        OnKnockbackApplied?.Invoke();

        if (_knockbackRoutine != null) StopCoroutine(_knockbackRoutine); // 중첩 방지: 새 피격이 기존 경직을 갱신
        _knockbackRoutine = StartCoroutine(KnockbackRoutine(direction, knockbackPower, duration));
    }

    protected virtual void PrepareRigidbodyForKnockback(Rigidbody2D rb)
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); // 기본: X만 초기화
    }

    protected virtual Vector2 ComputeKnockbackForce(Vector2 direction, float power)
    {
        return new Vector2(direction.x, 0.5f).normalized * power; // 기본: 대각선 위로
    }


    private IEnumerator KnockbackRoutine(Vector2 direction, float power, float duration)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            isKnockedBack = true;

            // 초기 X축 속도를 초기화하고 밀어냄
            //rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            //Vector2 force = new Vector2(direction.x, 0.5f).normalized * power;
            PrepareRigidbodyForKnockback(rb);
            //rb.AddForce(force, ForceMode2D.Impulse);
            rb.AddForce(ComputeKnockbackForce(direction, power), ForceMode2D.Impulse);

            yield return new WaitForSeconds(duration);

            // 밀려난 후 미끄러짐 방지 (Y축 중력은 유지)
            //if (rb != null) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

            isKnockedBack = false;
        }
        _knockbackRoutine = null;
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} 사망!");
    }
}
