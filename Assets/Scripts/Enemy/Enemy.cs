using UnityEngine;
using UnityEngine.UI;

// 나중에 DataManager에서 가져올 데이터의 형태 (Flyweight 패턴)
public class EnemyData
{
    public int EnemyID;
    public string EnemyName;
    public int MaxHP;
    public int Attack;
    public float MoveSpeed;
    // ...
}

// 추상 클래스: 몬스터들의 공통 기능(접촉 데미지, 넉백 등)을 정의
public class Enemy : CharacterStats
{
    [Header("Enemy Stats")] //체력, 공격력, 방어력
    public float attackPower;

    protected enum EnemyState { Idle, Patrol, Chase, Attack, Die }
    [SerializeField]
    protected EnemyState currentState;

    [Header("Movement Settings")] //속도, 탐지 범위, 공격 시행 범위, patrol 텀 
    public float patrolMoveSpeed = 1f;
    public float chaseMoveSpeed = 2f;
    public float detectionRange = 4f;
    public float attackRange = 1.2f;

    [Header("Combat Settings")]
    [Tooltip("기본 공격력 대비 몸통 박치기 데미지 비율")]
    public float contactDamageMultiplier = 0.5f; // 몸빵은 기본 공격력의 50%만 들어감
    private float contactDamageCooldown = 1.0f;  // 1초에 한 번만 몸빵 데미지 들어감 (다단히트 방지)
    private float lastContactTime = -1f;

    protected Transform player; //
    protected Rigidbody2D rb;
    protected Animator animator;
    //protected SpriteRenderer spriteRenderer;

    // 풀에서 빌려온 체력바를 기억하는 변수
    protected EnemyHealthBar activeHealthBar;

    protected bool isMoving = false;
    protected bool isAttacking = false;

    protected override void Awake()
    {
        base.Awake(); // CharacterStats의 HP, MP 초기화
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();

    }

    //protected virtual void Start()
    //{
    //    if (enemyHealthBar != null)
    //    {
    //        enemyHealthBar.Init(maxHealth); // 슬라이더의 최대치를 몬스터의 MaxHP로 설정
    //        enemyHealthBar.HideImmediate(); // 시작할 때는 숨겨둠
    //    }
    //}

    //protected virtual void Update()
    //{
    //    // 부모(CharacterStats)의 넉백 상태를 확인. 넉백 중엔 AI 정지
    //    if (isKnockedBack) return;
    //    // Die 상태일 때는 업데이트 처리 하지 않기
    //    if (currentState == EnemyState.Die) return;
    //}

    // ** 모든 적은 넉백이나 죽음 시 AI 동작을 멈춰야 함
    protected virtual void Update()
    {
        if (isKnockedBack || currentState == EnemyState.Die) return;

        // 자식이 구현할 실제 AI 행동을 호출
        HandleAI();
    }

    // 자식 클래스(Slime 등)가 반드시 구현해야 할 AI 로직
    protected virtual void HandleAI()
    {
        // 빈 껍데기. 슬라임이나 보스가 오버라이드해서 사용.
    }

    // 1. 접촉 데미지 (모든 몬스터 공통)
    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        if (currentState == EnemyState.Die) return; // 죽었으면 데미지 안줌

        if (collision.gameObject.CompareTag("Player"))
        {
            if (Time.time >= lastContactTime + contactDamageCooldown)
            {
                CharacterStats playerStats = collision.gameObject.GetComponent<CharacterStats>();
                if (playerStats != null)
                {
                    // 기본 공격력 * 배율 적용(0.5 임시)
                    int contactDamage = Mathf.RoundToInt(attack.GetValue() * contactDamageMultiplier);

                    // 플레이어 살짝 밀쳐내기
                    Vector2 knockbackDir = (collision.transform.position - transform.position).normalized;
                    playerStats.TakeDamage(contactDamage, currentElement, this, knockbackDir, 3f); // ★ attacker=this 추가

                    lastContactTime = Time.time;
                    Debug.Log($"[Enemy] 몸통 박치기! 데미지: {contactDamage}");
                }
            }
        }
    }

    // 2. 데미지 받았을 때 처리 (CharacterStats 오버라이드)
    public override bool TakeDamage(
        int incomingDamage, 
        ElementType attackElement = ElementType.Normal, 
        CharacterStats attacker = null, 
        Vector2? knockbackDirection = null, 
        float knockbackPower = 0f, 
        Vector2? attackOriginOverride = null,
        bool piercesDodge = false)
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player").transform;

        // 호출부가 별도 넉백을 안 넘겼으면 Enemy 고유의 "플레이어 반대방향" 룰 적용
        if (!knockbackDirection.HasValue && player != null)
        {
            float dir = Mathf.Sign(transform.position.x - player.position.x);
            knockbackDirection = new Vector2(dir, 0.5f);
            knockbackPower = 5f;
        }

        bool applied = base.TakeDamage(incomingDamage, attackElement, attacker, knockbackDirection, knockbackPower, attackOriginOverride, piercesDodge); // ★ 두 매개변수 추가

        if (applied)
        {
            if (activeHealthBar == null || !activeHealthBar.gameObject.activeInHierarchy)
            {
                GameObject hbObj = PoolManager.Instance.SpawnFromPool("EnemyHealthBar", transform.position, Quaternion.identity, PoolType.Global);
                if (hbObj != null)
                {
                    activeHealthBar = hbObj.GetComponent<EnemyHealthBar>();
                    activeHealthBar.Init(this.transform, maxHealth, currentHealth);
                }
            }
            else
            {
                activeHealthBar.UpdateHealth(currentHealth);
            }

            if (currentHealth <= 0 && currentState != EnemyState.Die)
            {
                currentState = EnemyState.Die;
                Die();
            }
        }

        return applied;
    }

    protected override void Die()
    {
        base.Die();
        animator.SetTrigger("Die");
        rb.linearVelocity = Vector2.zero;

        // 연결된 체력바 끄기
        if (activeHealthBar != null) PoolManager.Instance.ReturnToPool(activeHealthBar.gameObject);

        // 몬스터 삭제 대신 풀로 반납
        // PoolManager.Instance.ReturnToPool(gameObject, PoolType.Zone); 
        Destroy(gameObject, 0.6f); // 임시로 Destroy 유지
    }


}
