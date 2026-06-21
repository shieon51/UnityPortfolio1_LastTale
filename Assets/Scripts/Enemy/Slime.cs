using UnityEngine;
using System.Collections;
using UnityEditor;

public class Slime : Enemy
{
    [Header("Slime Specific")]
    public float actualAttackRadius = 1.0f; // 실제 공격 판정 반경

    // 디버깅(시각화)용 변수 추가
    private bool showAttackGizmo = false;
    private Vector2 lastAttackCenter;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        currentState = EnemyState.Idle;
        StartCoroutine(StateMachine());
    }

    //private void Update()
    //{
    //    base.Update();

    //    // 💡 넉백 중이거나 죽었으면 AI 추적 로직 중단!
    //    if (isKnockedBack || currentState == EnemyState.Die) return;

    //    if (player == null)
    //        player = GameObject.FindGameObjectWithTag("Player").transform;

    //    if (player != null)
    //    {
    //        float dist = Vector2.Distance(transform.position, player.position);

    //        if (dist <= detectionRange && !isAttacking)
    //        {
    //            currentState = EnemyState.Chase;
    //        }
    //        else if (currentState >= EnemyState.Chase && dist > detectionRange)
    //        {
    //            currentState = EnemyState.Idle;
    //            isAttacking = false;
    //            animator.SetBool("IsAttack", false);
    //        }
    //    }
    //}

    // 부모의 Update() 안에서 호출됨 (넉백 중엔 알아서 안 불림)
    protected override void HandleAI()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player").transform;

        if (player != null)
        {
            float dist = Vector2.Distance(transform.position, player.position);

            if (dist <= detectionRange && !isAttacking)
            {
                currentState = EnemyState.Chase;
            }
            else if (currentState >= EnemyState.Chase && dist > detectionRange)
            {
                currentState = EnemyState.Idle;
                isAttacking = false;
                animator.SetBool("IsAttack", false);
            }
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

        // 방향 바라보기
        spriteRenderer.flipX = player.position.x > transform.position.x;

        // 공격 애니메이션이 실행되면, 그 안에서 애니메이션 이벤트(Animation Event)로 
        // ExecuteActualAttack() 함수를 호출하는 것이 가장 타격감이 좋습니다.
        // 여기선 코루틴으로 임시 타이밍을 맞춥니다.
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        // 1. 선딜레이 (모션 진행)
        yield return new WaitForSeconds(0.4f);

        // 2. 진짜 공격 타격 판정 (오버랩 서클 이용)
        Vector2 attackCenter = transform.position + (Vector3.right * (spriteRenderer.flipX ? 1 : -1) * 0.5f);

        // 💡 [디버깅] 타격 순간 빨간색 원 기즈모를 켜기 위해 위치와 상태 저장
        lastAttackCenter = attackCenter;
        showAttackGizmo = true;

        Collider2D hit = Physics2D.OverlapCircle(attackCenter, actualAttackRadius, LayerMask.GetMask("Player"));

        if (hit != null)
        {
            CharacterStats playerStats = hit.GetComponentInParent<CharacterStats>(); // 부모에서 찾기
            if (playerStats != null)
            {
                // 스킬 공격은 기본 공격력 100% (또는 그 이상) 적용
                int skillDamage = attack.GetValue();
                playerStats.TakeDamage(skillDamage, currentElement);

                // 스킬 공격은 넉백이 더 강함
                Vector2 knockback = (hit.transform.position - transform.position).normalized;
                playerStats.ApplyKnockback(knockback, 7f);

                Debug.Log($"[Slime] 스킬 공격 명중! 데미지: {skillDamage}");
            }
        }

        // [디버깅] 0.15초 동안만 화면에 빨간 원을 띄워두고 끔 (시각적 타격감/확인용)
        yield return new WaitForSeconds(0.15f);
        showAttackGizmo = false;

        // 3. 후딜레이 후 추적 상태로 복귀
        yield return new WaitForSeconds(0.45f);

        if (Vector2.Distance(transform.position, player.position) > attackRange)
        {
            currentState = EnemyState.Chase;
            isAttacking = false;
        }
        else
        {
            // 계속 사거리 안이면 다시 공격
            currentState = EnemyState.Attack;
        }
    }

    // 넉백을 위해 오버라이드
    //public override void TakeDamage(int damage, ElementType attackElement = ElementType.Normal)
    //{
    //    base.TakeDamage(damage, attackElement);

    //    // 플레이어 반대 방향으로 넉백 (기존 코드 개선)
    //    if (player != null)
    //    {
    //        float dir = Mathf.Sign(transform.position.x - player.position.x);
    //        ApplyKnockback(new Vector2(dir, 0.5f), 5f);
    //    }
    //}

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Handles.color = Color.yellow;
        Handles.DrawWireDisc(transform.position, Vector3.forward, detectionRange);
        Handles.color = Color.white;
        Handles.DrawWireDisc(transform.position, Vector3.forward, attackRange);

        //Handles.DrawWireArc(transform.position, Vector3.forward,  Vector3.right, 360, radius);

        // 공격하는 순간 (반투명한 빨간색 원으로 내부를 채워 표시)
        if (showAttackGizmo)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f); // Red 색상에 투명도(Alpha) 50%
            Gizmos.DrawSphere(lastAttackCenter, actualAttackRadius);
        }
        else
        {
            // 평상시에는 얇은 핑크색 선으로 미리보기만 제공
            Handles.color = Color.magenta;
            Vector3 center = transform.position + (Vector3.right * (Application.isPlaying && spriteRenderer != null && spriteRenderer.flipX ? 1 : -1) * 0.5f);
            Handles.DrawWireDisc(center, Vector3.forward, actualAttackRadius);
        }
    }
#endif
}
