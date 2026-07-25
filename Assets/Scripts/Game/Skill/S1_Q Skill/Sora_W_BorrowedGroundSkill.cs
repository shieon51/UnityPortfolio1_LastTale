using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "New Borrowed Ground Skill", menuName = "LastMarchan/Skills/Borrowed Ground (W)")]
public class Sora_W_BorrowedGroundSkill : SkillBase
{
    [Header("Targeting")]
    [Tooltip("이 반경 안에 유효한 적이 없으면 아예 발동하지 않음")]
    public float targetSearchRadius = 6f;
    public LayerMask enemyLayer;

    [Header("Warp")]
    [Tooltip("타겟 뒤쪽으로 이 거리만큼 떨어진 지점에 도착")]
    public float arrivalOffsetFromTarget = 1.2f;
    [Tooltip("도착 순간 부여되는 무적 시간(초). 겹친 적 사이로 끼어드는 동안 피격 방지용")]
    public float arrivalInvincibility = 0.15f;
    [Tooltip("도착 지점에서 겹친 대상들을 밀어내는 힘")]
    public float arrivalPushForce = 6f;

    [Header("Ripple (도착 파동)")]
    public float rippleRadius = 2.5f;
    public float damageMultiplier = 1f;
    public float knockbackPower = 5f;

    // 이번 시전에서 CanExecute가 찾아낸 타겟을 ExecuteSkillBehavior까지 전달하기 위한 캐시.
    // (같은 애셋 인스턴스를 여러 캐릭터가 동시에 쓰지 않는다는 전제 하의 단순한 방식)
    private Transform _cachedTarget;

    // 사거리 내 유효 타겟이 없으면 아예 발동하지 않음 (마나도 소모되지 않음)
    public override bool CanExecute(PlayerCombat combat)
    {
        _cachedTarget = FindTarget(combat);
        return _cachedTarget != null;
    }

    private Transform FindTarget(PlayerCombat combat)
    {
        var stats = combat.GetComponent<CharacterStats>();

        // 우선순위 1: 가장 최근 나를 공격한 대상 (사거리 안에 있고, 아직 살아있을 때만 유효)
        if (stats.LastAttacker != null
            && stats.LastAttacker.currentHealth > 0
            && Vector2.Distance(combat.transform.position, stats.LastAttacker.transform.position) <= targetSearchRadius)
        {
            return stats.LastAttacker.transform;
        }

        // 우선순위 2: 사거리 내 가장 가까운 적
        Collider2D[] hits = Physics2D.OverlapCircleAll(combat.transform.position, targetSearchRadius, enemyLayer);
        Transform nearest = null;
        float bestDist = float.MaxValue;
        foreach (var hit in hits)
        {
            float dist = Vector2.Distance(combat.transform.position, hit.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = hit.transform;
            }
        }
        return nearest;
    }

    public override IEnumerator ExecuteSkillBehavior(PlayerCombat combat, Rigidbody2D rb, Animator anim, CharacterStats stats)
    {
        Transform target = _cachedTarget;
        if (target == null) yield break; // 안전장치 (CanExecute를 통과했다면 원래는 null이 아니어야 함)

        // 타겟의 반대편(뒤쪽) 좌표 계산
        float sideDir = (combat.transform.position.x < target.position.x) ? 1f : -1f;
        Vector2 arrivalPos = (Vector2)target.position + new Vector2(sideDir * arrivalOffsetFromTarget, 0f);

        // 시전 텀 (애니메이션 선딜과 맞춤 — activeDuration 재사용)
        yield return new WaitForSeconds(activeDuration);

        rb.position = arrivalPos;
        Physics2D.SyncTransforms();

        // 도착 순간 안전장치: 짧은 무적 + 겹친 대상 밀어내기
        stats.GrantTemporaryInvincibility(arrivalInvincibility);
        PushOverlappingColliders(arrivalPos, rb);

        // 도착 파동
        SpawnRipple(combat, stats, arrivalPos);
    }

    private void PushOverlappingColliders(Vector2 center, Rigidbody2D selfRb)
    {
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(center, 0.5f, LayerMask.GetMask("Enemy", "Player"));
        foreach (var col in overlaps)
        {
            if (col.attachedRigidbody == null || col.attachedRigidbody == selfRb) continue;

            Vector2 dir = ((Vector2)col.transform.position - center).normalized;
            if (dir == Vector2.zero) dir = Vector2.right;
            col.attachedRigidbody.AddForce(dir * arrivalPushForce, ForceMode2D.Impulse);
        }
    }

    private void SpawnRipple(PlayerCombat combat, CharacterStats casterStats, Vector2 center)
    {
        combat.SetDebugHitbox(center, Vector2.one * rippleRadius);

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, rippleRadius, enemyLayer);
        foreach (var hit in hits)
        {
            CharacterStats enemyStats = hit.GetComponentInParent<CharacterStats>();
            if (enemyStats == null) continue;

            int damage = Mathf.RoundToInt(casterStats.attack.GetValue() * damageMultiplier);
            enemyStats.TakeDamage(damage, casterStats.currentElement, casterStats);

            Vector2 knockbackDir = ((Vector2)hit.transform.position - center).normalized;
            enemyStats.ApplyKnockback(knockbackDir, knockbackPower);
        }

        combat.ClearDebugHitbox();
        // TODO: 보라색 원형 에너지파 VFX는 원거리 피격 이펙트와 동일하게 별도 이펙트 매니저에서 관리 예정
    }

    // 파동은 ExecuteSkillBehavior 안에서 이미 즉시 처리되므로, 별도의 지속 히트박스는 필요 없음.
    public override IEnumerator ExecuteHitbox(PlayerCombat combat, Transform parentTransform, CharacterStats stats)
    {
        yield break;
    }
}