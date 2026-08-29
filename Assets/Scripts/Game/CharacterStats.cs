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
    public float parryWindowDuration = 0.25f;
    private float _guardStartTime = -10f;

    [Header("완벽 방어 시 공격자 밀쳐내기")]
    public float perfectGuardPushback = 3f;

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

    [Header("Knockback Friction")]
    public float knockbackSlideDrag = 15f;

    [Header("Super Armor")]
    [Tooltip("이 값 이상의 넉백파워는 슈퍼아머를 무시하고 관통함")]
    public float superArmorBreakThreshold = 8f;

    [Header("타격감(히트스톱)")]
    public float hitStopDuration = 0.1f;
    public float parryHitStopDuration = 0.25f; // 패링은 더 길게

    // 최근에 나를 공격한 대상 (W 스킬의 "최근 피격 대상 우선" 타겟팅에 사용)
    public CharacterStats LastAttacker { get; protected set; }

    // 부모에서 선언된 이벤트 (부모만 쏠 수 있음)
    public event Action OnHealthChanged;
    public event Action OnManaChanged;

    // 총 데미지량 계산 관련 (보스전)
    public event Action<int, CharacterStats> OnDamageTaken; // (데미지량, 공격자)

    // 방어자 기준 이벤트 (누구를 막았는지)
    public event Action<CharacterStats> OnParrySuccess;

    // 결과(관통/방어/패링)와 무관하게 무적만 아니면 항상 발행
    public event Action<CharacterStats> OnAttackReceived; 

    // 맞을 때마다(중첩 포함) 매번 발행
    public event Action OnKnockbackApplied;

    private Coroutine _knockbackRoutine;

    // 넉백 상태인지 확인하는 변수 추가
    public bool isKnockedBack { get; protected set; } = false;

    // 그로기 진입 시간 관련
    public float GroggyDuration { get; private set; }

    // 슈퍼아머 변수 추가 (공격 중일 때 true가 됨) -> 공격 모션 중에는 대미지는 받되 밀려나지 않는 상태
    public bool isSuperArmor = false;

    // 방향 관련 (방어 방향 판정)
    protected SpriteRenderer spriteRenderer;

    public void StartGuard() { isGuarding = true; _guardStartTime = Time.time; }
    public void StopGuard() => isGuarding = false;
    private bool IsInParryWindow => isGuarding && (Time.time - _guardStartTime) <= parryWindowDuration;

#if UNITY_EDITOR
    [Header("한눈에 보기 (자동 생성 — 손대지 않아도 됨)")]
    [TextArea(6, 14)]
    public string statsSummary;

    private void OnValidate()
    {
        statsSummary =
            $"Lv.{level} | HP {maxHealth} | MP {maxMana}\n" +
            $"공격 {attack.GetValue()} | 방어 {defense.GetValue()} | 민첩 {agility.GetValue()}\n" +
            $"피격 경직 {knockbackStunDuration:F2}s | 슬라이드 마찰 {knockbackSlideDrag}\n" +
            $"슈퍼아머 돌파 임계값 {superArmorBreakThreshold}\n" +
            $"패링 판정창 {parryWindowDuration:F2}s | 무적시간 {invincibilityDuration:F2}s";
    }
#endif

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public virtual bool TakeDamage(
         int incomingDamage,
         ElementType attackElement = ElementType.Normal,
         CharacterStats attacker = null,
         Vector2? knockbackDirection = null,
         float knockbackPower = 0f,
         Vector2? attackOriginOverride = null) // - 대시형 공격이 "시작 시점" 위치를 넘길 때 사용
    {
        if (Time.time < lastHitTime + invincibilityDuration) return false; // 무적 중 — 넉백 포함 아무 효과 없음

        OnAttackReceived?.Invoke(attacker); // ★ 추가

        bool facingAttacker = IsAttackFromFacingSide(attacker, attackOriginOverride); // attacker null이면 내부에서 true 처리됨
        bool guardActive = isGuarding && facingAttacker; // 방어는 방향이 맞을 때만 유효

        if (guardActive && attacker != null && IsInParryWindow && TryResolveParry(attacker)) // ★ guardActive 기준, attacker null 체크 유지
        {
            lastHitTime = Time.time;
            OnParrySuccess?.Invoke(attacker);
            FloatingTextManager.Instance?.ShowParry(transform.position + Vector3.up * 1f);
            SoundManager.Instance?.PlaySFX("parry_success");
            ScreenFlashOverlay.Instance?.Flash(new Color(1f, 0.9f, 0.3f), 0.15f);
            float groggyDuration = CombatFormulaService.Instance.CalculateGroggyDuration(attacker, this);
            attacker.ApplyGroggy(groggyDuration);
            HitStopManager.Instance?.Trigger(parryHitStopDuration); 
            return false;  // 패링 성공 — 넉백 포함 완전 무효화
        }

        lastHitTime = Time.time;
        if (attacker != null) LastAttacker = attacker;

        int finalDamage = ComputeFinalDamage(incomingDamage, attackElement, attacker, guardActive); // ★ guardActive 전달

        if (guardActive && finalDamage <= 0) // ★
        {
            FloatingTextManager.Instance?.ShowGuard(transform.position + Vector3.up * 1f);
            SoundManager.Instance?.PlaySFX("guard_perfect");
            CameraDirector.Instance?.Shake(0.05f, 0.05f); // ★ 17번 — 관통 때보다 약하게 // ? 하드코딩 빼기
            if (attacker != null && knockbackDirection.HasValue)
            {
                attacker.LastAttacker = this; // ★ 추가 — "지금 나를 밀친 게 나(NPC/플레이어)"라는 걸 명확히 기록
                attacker.ApplyKnockback(-knockbackDirection.Value, perfectGuardPushback); //?
            }
            return false; // 완벽 방어 시 공격자 밀쳐냄
        }

        if (guardActive) // ★ 방향 안 맞으면 여기 안 들어오고 바로 else(무방비)로 감
        {
            FloatingTextManager.Instance?.ShowGuardedDamage(finalDamage, transform.position + Vector3.up * 1f);
            SoundManager.Instance?.PlaySFX("guard_break");
        }
        else
        {
            FloatingTextManager.Instance?.ShowDamage(finalDamage, transform.position + Vector3.up * 1f);
            SoundManager.Instance?.PlaySFX("hit_generic");
        }

        if (attacker is NPC || this is NPC) CameraDirector.Instance?.Shake(0.1f, 0.1f);
        if (knockbackDirection.HasValue && spriteRenderer != null) // ★ 추가
        {
            float hitFromDir = -Mathf.Sign(knockbackDirection.Value.x); // 넉백 반대쪽 = 맞은(공격 온) 방향
            if (hitFromDir != 0f) spriteRenderer.flipX = hitFromDir > 0f;
        }
        GetComponentInChildren<HitFlashController>()?.Flash();
        
        //ScreenFlashOverlay.Instance?.Flash(new Color(1f, 0.3f, 0.3f, 0.3f), 0.08f); // ★ 은은한 빨간 플래시
        HitStopManager.Instance?.Trigger(hitStopDuration); // ★ 히트스톱 추가
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        OnHealthChanged?.Invoke();
        OnDamageTaken?.Invoke(finalDamage, attacker);

        Debug.Log($"{gameObject.name}가 {finalDamage} 데미지를 받았습니다. (잔여 HP: {currentHealth})");

        // ★ 넉백은 실제로 데미지가 "적용된" 이 지점, 한 곳에서만 트리거됨
        if (knockbackDirection.HasValue && knockbackPower > 0f)
            ApplyKnockback(knockbackDirection.Value, knockbackPower);

        if (currentHealth <= 0) Die();

        return true;
    }

    protected int ComputeFinalDamage(int incomingDamage, ElementType attackElement, CharacterStats attacker, bool guardActive)
    {
        if (CombatFormulaService.Instance == null)
        {
            // 씬에 서비스가 없어도 게임이 죽지 않도록 하는 안전 폴백 (기존 로직과 동일)
            int fallbackDefense = defense.GetValue();
            if (guardActive)
            {
                int guardDef = fallbackDefense * 2;
                if (incomingDamage < guardDef) return 0; // ★ 폴백 경로에도 완벽 방어 반영
                return Mathf.Max(1, incomingDamage - guardDef);
            }
            return Mathf.Max(1, incomingDamage - fallbackDefense);
        }

        var ctx = new CombatContext
        {
            IncomingDamage = incomingDamage,
            Attacker = attacker,
            Defender = this,
            AttackElement = attackElement,
            IsGuarding = guardActive,
        };
        return CombatFormulaService.Instance.CalculateDamage(ctx);
    }

    // 방향 판정 (방어 관련)
    protected virtual bool IsAttackFromFacingSide(CharacterStats attacker, Vector2? attackOriginOverride = null)
    {
        if (attacker == null || spriteRenderer == null) return true;
        Vector2 originPos = attackOriginOverride ?? (Vector2)attacker.transform.position;
        float dirToAttacker = Mathf.Sign(originPos.x - transform.position.x);
        float facingDir = spriteRenderer.flipX ? 1f : -1f; // flipX=true→오른쪽, false→왼쪽 (프로젝트 공통 컨벤션)
        return dirToAttacker == 0f || Mathf.Approximately(dirToAttacker, facingDir);
    }

    // 그로기 리셋
    public void ForceResetGroggy()
    {
        if (_groggyRoutine != null) { StopCoroutine(_groggyRoutine); _groggyRoutine = null; }
        IsGroggy = false;
    }

    // 도착 순간처럼, '맞아서' 생기는 무적이 아니라 능동적으로 무적을 거는 경우를 위한 헬퍼.
    // 기존 lastHitTime/invincibilityDuration 메커니즘을 그대로 재사용 (새 필드 없음).
    public void GrantTemporaryInvincibility(float duration)
    {
        lastHitTime = Time.time + duration - invincibilityDuration;
    }

    // 체력 풀 자체를 재설정할 때 사용 (힐/데미지 계산 없이 강제로 세팅). 보스 난이도 프로필 전환 등에서 사용.
    protected void SetHealthDirect(int newMax, int newCurrent)
    {
        maxHealth = newMax;
        currentHealth = Mathf.Clamp(newCurrent, 0, maxHealth);
        OnHealthChanged?.Invoke(); // ★ CharacterStats 안에서 발행하는 거라 문제없음
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
        GroggyDuration = duration;
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
        if (currentHealth <= 0) return;
        if (isSuperArmor && knockbackPower < superArmorBreakThreshold) return; // ★ 약한 넉백만 막음

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
            float originalDrag = rb.linearDamping;
            PrepareRigidbodyForKnockback(rb);
            rb.linearDamping = knockbackSlideDrag; // ★ 추가
            rb.AddForce(ComputeKnockbackForce(direction, power), ForceMode2D.Impulse);
            yield return new WaitForSeconds(duration);
            rb.linearDamping = originalDrag; // ★ 추가
            isKnockedBack = false;
        }
        _knockbackRoutine = null;
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} 사망!");
    }
}
