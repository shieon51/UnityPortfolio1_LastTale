using System.Collections.Generic;
using System.Collections;
using UnityEngine;

// SOLID를 위한 설정 데이터 분리
[System.Serializable]
public class DashSettings
{
    //public float dashForce = 15f; // 팍-치고 나가는 힘
    //public float slideDrag = 5f;  // 끼익-멈출 때 마찰력 (Linear Drag)

    [Header("빛 속성 대시 세팅")]
    [Tooltip("가속 없이 즉시 도달할 절대 속도 (팍- 치고 나감)")]
    public float burstSpeed = 30f;

    [Tooltip("급브레이크 마찰력 (20~30 정도로 확 높여야 끼이익! 하고 멈춤)")]
    public float slideDrag = 25f;
}

[System.Serializable]
public class HitboxSettings
{
    public Vector2 size;
    public Vector2 offset;
    public float activeDuration = 0.15f;
    public float knockbackPower = 5f;
}

public class LielAttackCompo : MonoBehaviour
{
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Liel_AI lielAI; // 체력/마나 감소를 위해 참조

    [Header("Settings")]
    public float minAttack1Range = 0.8f; // 💡 너무 가까우면 안 씀
    public float maxAttack1Range = 2.0f; // 💡 이 거리 안에서만 씀
    public DashSettings dash1;
    public HitboxSettings hitbox1;
    public int attack1ManaCost = 5;

    private float originalDrag;

    [Header("Visual Debug")]
    public bool showGizmos = true;
    private Vector2 lastHitboxCenter;
    private bool isDrawingHitbox = false;


    private void Awake()
    {
        rb = GetComponentInParent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        lielAI = GetComponentInParent<Liel_AI>();
        //originalDrag = rb.drag;
        originalDrag = rb.linearDamping; // 컴포넌트가 움직일 때 공기저항처럼 속도 줄여주는 역할
    }

    // =====================================================================
    // 🎭 애니메이션 이벤트(Animation Event)에서 호출할 함수들 (gif 타이밍 맞춤)
    // =====================================================================

    // 1. [ AE ] 선딜레이 끝, 돌진 시작 타이밍 (팍-!)
    public void AE_StartDash1()
    {
        // 💡 [마나 소모] 정석 타이밍: 기술 발동 순간
        lielAI.UseMana(attack1ManaCost);

        // 물리 마찰력을 0으로 만들어서 촥-미끄러지게 함
        rb.linearDamping = 0f;

        float dir = sr.flipX ? 1f : -1f;

        // 💡 [핵심] AddForce(밀기)를 버리고, 목표 속도(Velocity)를 즉시 덮어씌움!
        // 가속도가 붙는 시간이 생략되어 1프레임만에 최고 속도로 튀어나갑니다.
        rb.linearVelocity = new Vector2(dir * dash1.burstSpeed, 0f);

        Debug.Log("[Liel] 마나 소모 및 빛의 속도로 돌진 시작!");
    }

    // 2. [ AE ] 돌진 중, 끼익-멈추기 시작 타이밍 (선형 마찰 마찰 마찰력-!)
    public void AE_StartSlideStop1()
    {
        // 💡 [핵심] 마찰력(Drag)을 팍 올려서 선형적으로 끼이익-멈추는 느낌 구현
        rb.linearDamping = dash1.slideDrag;
    }

    // 3. [ AE ] 진짜 칼을 휘두르는 타이밍 (판정 켜기)
    public void AE_EnableHitbox1()
    {
        // 💡 PlayerAttack의 영역 확인 로직 그대로 활용!
        StartCoroutine(ActiveHitboxRoutine(hitbox1));
    }

    // 4. [ AE ] 애니메이션 완전히 끝남 (정리)
    public void AE_Attack1End()
    {
        // 💡 마찰력 원상복구
        rb.linearDamping = originalDrag;
        // 미세하게 남은 속도까지 확실히 정지
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    // =====================================================================
    // ⚔️ [공격 판정 코루틴] (PlayerAttack.cs 구조 복사)
    // =====================================================================
    private IEnumerator ActiveHitboxRoutine(HitboxSettings settings)
    {
        float elapsed = 0f;
        HashSet<Collider2D> alreadyHitEnemies = new HashSet<Collider2D>();
        lielAI.isSuperArmor = true;

        // 💡 [완벽 수정] 공격 똭! 시작하는 순간 '부모(몸통)' 위치를 기준으로 고정점 1번만 계산!
        float dir = sr.flipX ? -1 : 1;
        Vector3 basePos = transform.parent != null ? transform.parent.position : transform.position;
        Vector2 fixedCenter = (Vector2)basePos + new Vector2(settings.offset.x * dir, settings.offset.y);

        lastHitboxCenter = fixedCenter; // 이 고정된 위치를 기즈모에 넘겨줌
        isDrawingHitbox = true;         // 그리기 시작!

        while (elapsed < settings.activeDuration)
        {
            // 💡 캐릭터가 돌진해서 앞으로 튕겨 나가도, 판정은 무조건 fixedCenter (제자리)에서만 발생!
            Collider2D[] hits = Physics2D.OverlapBoxAll(fixedCenter, settings.size, 0f, LayerMask.GetMask("Player"));

            foreach (var hit in hits)
            {
                if (!alreadyHitEnemies.Contains(hit))
                {
                    alreadyHitEnemies.Add(hit);
                    CharacterStats playerStats = hit.GetComponent<CharacterStats>();
                    if (playerStats != null)
                    {
                        int damage = lielAI.attack.GetValue();
                        playerStats.TakeDamage(damage, lielAI.currentElement);

                        // 넉백 방향도 부모 위치 기준으로 계산
                        Vector2 knockbackDir = (hit.transform.position - lielAI.transform.position).normalized;
                        playerStats.ApplyKnockback(knockbackDir, settings.knockbackPower);
                    }
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        lielAI.isSuperArmor = false;
        isDrawingHitbox = false;
    }

    // =========================================================
    // 💡 [수정됨] 하드코딩(2.0f) 제거 및 Liel_AI의 사거리 변수 연동
    // =========================================================
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // 무조건 부모(몸통) 좌표 가져옴!
        Vector3 basePos = transform.parent != null ? transform.parent.position : transform.position;

        // 1. 최대 사거리 (주황색)
        Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
        Gizmos.DrawWireSphere(basePos, maxAttack1Range);

        // 💡 2. 최소 사거리 (빨간색) - 이 원 안쪽은 너무 가까워서 못 때리는 데드존!
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawWireSphere(basePos, minAttack1Range);

        // 2. 실시간 히트박스 (빨간색)
        if (isDrawingHitbox)
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            // 💡 매 프레임 위치 재계산 안 함! 아까 코루틴에서 똭 찍어둔 고정 좌표(lastHitboxCenter)를 그대로 그림!
            Gizmos.DrawCube(lastHitboxCenter, hitbox1.size);
        }
    }

    // 💡 [수정됨 1번] 게임 실행 전 에디터에서 히트박스를 미리 볼 수 있게 해줍니다!
    private void OnDrawGizmosSelected()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        float dir = (sr != null && sr.flipX) ? -1 : 1;

        // 💡 선택했을 때 보이는 파란 박스 미리보기도 부모 몸통 기준!
        Vector3 basePos = transform.parent != null ? transform.parent.position : transform.position;
        Vector2 center = (Vector2)basePos + new Vector2(hitbox1.offset.x * dir, hitbox1.offset.y);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, hitbox1.size);
    }

}
