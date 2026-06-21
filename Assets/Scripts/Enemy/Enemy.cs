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
    protected SpriteRenderer spriteRenderer;

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
                    playerStats.TakeDamage(contactDamage, currentElement);

                    // 플레이어 살짝 밀쳐내기
                    Vector2 knockbackDir = (collision.transform.position - transform.position).normalized;
                    playerStats.ApplyKnockback(knockbackDir, 3f);

                    lastContactTime = Time.time;
                    Debug.Log($"[Enemy] 몸통 박치기! 데미지: {contactDamage}");
                }
            }
        }
    }

    // 2. 데미지 받았을 때 처리 (CharacterStats 오버라이드)
    public override void TakeDamage(int incomingDamage, ElementType attackElement = ElementType.Normal)
    {
        int preHealth = currentHealth; // 맞기 전 체력 기억
        base.TakeDamage(incomingDamage, attackElement); // 부모의 데미지 계산 및 UI 갱신 로직

        // 체력이 진짜로 깎였을 때만 (무적시간 통과 시에만) 처리
        if (currentHealth < preHealth)
        {
            // 1. 체력바 풀링 요청 및 갱신
            if (activeHealthBar == null || !activeHealthBar.gameObject.activeInHierarchy)
            {
                // PoolManager에서 체력바 하나 꺼내오기! (이름은 풀에 등록한 프리팹 이름과 같아야 함)
                GameObject hbObj = PoolManager.Instance.SpawnFromPool("EnemyHealthBar", transform.position, Quaternion.identity, PoolType.Global);
                if (hbObj != null)
                {
                    activeHealthBar = hbObj.GetComponent<EnemyHealthBar>();
                    activeHealthBar.Init(this.transform, maxHealth, currentHealth);
                }
            }
            else
            {
                // 이미 머리 위에 떠있으면 체력만 갱신 (시간 연장)
                activeHealthBar.UpdateHealth(currentHealth);
            }

            // 2. 넉백 로직 (자식에서 하던 걸 안전한 이곳으로 이동)
            if (player == null) player = GameObject.FindGameObjectWithTag("Player").transform;
            if (player != null)
            {
                float dir = Mathf.Sign(transform.position.x - player.position.x);
                ApplyKnockback(new Vector2(dir, 0.5f), 5f);
            }

            // 3. 사망 체크
            if (currentHealth <= 0 && currentState != EnemyState.Die)
            {
                currentState = EnemyState.Die;
                Die();
            }
        }
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
