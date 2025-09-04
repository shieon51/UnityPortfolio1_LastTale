using UnityEngine;
using System.Collections;
using UnityEditor;

public class Slime : Enemy
{

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        currentState = EnemyState.Idle;
        StartCoroutine(StateMachine());
    }

    private void Update()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player").transform;

        if (player != null && Vector2.Distance(transform.position, player.position) <= detectionRange
            && !isAttacking) //감지 범위 내로 들어오면
        {
            currentState = EnemyState.Chase;
        }
        else if (currentState >= EnemyState.Chase && Vector2.Distance(transform.position, player.position) > detectionRange) //감지 범위 내를 벗어나면
        {
            currentState = EnemyState.Idle;
            isAttacking = false;
            animator.SetBool("IsAttack", false);
        }
    }

    private IEnumerator StateMachine()
    {
        while (currentState != EnemyState.Die)
        {
            switch (currentState)
            {
                case EnemyState.Idle:
                    float waitTime = Random.Range(1f, 2.5f); // 1~2.5초 멈추기
                    yield return new WaitForSeconds(waitTime);
                    currentState = EnemyState.Patrol;
                    break;

                case EnemyState.Patrol:
                    animator.SetBool("IsMove", true);
                    StartCoroutine(MoveRandomly());
                    yield return new WaitForSeconds(3f);
                    currentState = EnemyState.Idle;
                    break;

                case EnemyState.Chase:
                    animator.SetBool("IsAttack", false);
                    animator.SetBool("IsMove", true);
                    ChasePlayer();
                    break;

                case EnemyState.Attack:
                    animator.SetBool("IsAttack", true);
                    //animator.SetBool("IsMove", false);
                    AttackPlayer();
                    yield return new WaitForSeconds(1f);
                    currentState = EnemyState.Chase;
                    break;
            }
            yield return null;
        }
    }
    private IEnumerator MoveRandomly()
    {
        isMoving = true;
        float moveDirection = Random.Range(0, 2) == 0 ? -1f : 1f;

        // 이동 방향에 따라 스프라이트 반전
        if (moveDirection != 0)
            spriteRenderer.flipX = moveDirection > 0;

        float moveDuration = Random.Range(1f, 3f); // 1~3초 동안 이동  
        float elapsedTime = 0f; // 경과 시간 초기화

        while (elapsedTime < moveDuration)
        {
            elapsedTime += Time.deltaTime; // 경과 시간 증가
            rb.linearVelocity = new Vector2(moveDirection * patrolMoveSpeed, rb.linearVelocity.y);
            yield return null; // 다음 프레임까지 대기
        }

        rb.linearVelocity = Vector2.zero;
        isMoving = false;
        animator.SetBool("IsMove", false);
    }

    private void ChasePlayer()
    {
        if (player == null) return;
        float direction = Mathf.Sign(player.position.x - transform.position.x);

        // 이동 방향에 따라 스프라이트 반전
        if (direction != 0)
            spriteRenderer.flipX = direction > 0;

        rb.linearVelocity = new Vector2(direction * chaseMoveSpeed, rb.linearVelocity.y);

        if (Vector2.Distance(transform.position, player.position) <= attackRange) //공격 범위 내로 들어오면
        {
            rb.linearVelocity = Vector2.zero;
            currentState = EnemyState.Attack;
            isAttacking = true;
        }
    }

    private void AttackPlayer()
    {
        if (player == null) return;

        Debug.Log("슬라임->플레이어 공격");

        if (Vector2.Distance(transform.position, player.position) > attackRange) //공격 범위 벗어나면
        {
            rb.linearVelocity = Vector2.zero;
            currentState = EnemyState.Chase;
            isAttacking = false;
            isMoving = false;
        }
    }

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        rb.AddForce(new Vector2(-1, 1) * 3, ForceMode2D.Impulse); //뒤로 넉백
        if (currentHealth <= 0)
        {
            currentState = EnemyState.Die;
            //animator.Play("Die");
            rb.linearVelocity = Vector2.zero;
            //Destroy(gameObject, 1.5f);
        }
    }

    private void OnDrawGizmos()
    {
        Handles.color = Color.yellow;
        Handles.DrawWireDisc(transform.position, Vector3.forward, detectionRange);
        Handles.color = Color.white;
        Handles.DrawWireDisc(transform.position, Vector3.forward, attackRange);

        //Handles.DrawWireArc(transform.position, Vector3.forward,  Vector3.right, 360, radius);
    }
}
