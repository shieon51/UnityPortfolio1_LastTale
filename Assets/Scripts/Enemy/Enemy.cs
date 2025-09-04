using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")] //체력, 공격력, 방어력
    public float maxHealth; 
    public float currentHealth;
    public float attackPower;
    public float defense;

    protected enum EnemyState { Idle, Patrol, Chase, Attack, Die }
    [SerializeField]
    protected EnemyState currentState;

    [Header("Movement Settings")] //속도, 탐지 범위, 공격 시행 범위, patrol 텀 
    public float patrolMoveSpeed = 1f;
    public float chaseMoveSpeed = 2f;
    public float detectionRange = 4f;
    public float attackRange = 1.2f;

    protected Transform player; //
    protected Rigidbody2D rb;
    protected Animator animator;
    protected SpriteRenderer spriteRenderer;
    protected bool isMoving = false;
    protected bool isAttacking = false;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        currentHealth = maxHealth;
    }

    public virtual void TakeDamage(float damage)
    {
        float finalDamage = Mathf.Max(damage - defense, 1); // 최소 1 이상의 피해
        currentHealth -= finalDamage;
        //animator.SetTrigger("Hit"); // 피격 애니메이션 실행

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        animator.SetTrigger("Die");
        //Destroy(gameObject, 1.5f); // 1.5초 후 삭제 (혹은 풀링 활용)
    }

   
}
