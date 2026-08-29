using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Borrowed Ground Skill", menuName = "LastMarchan/Skills/Borrowed Ground (W)")]
public class Sora_W_BorrowedGroundSkill : SkillBase
{
    [Header("Targeting")]
    [Tooltip("이 반경 안에 유효한 적이 없으면 아예 발동하지 않음")]
    public float targetSearchRadius = 6f;
    [Tooltip("바라보는 방향과 타겟 방향이 이 각도(도) 이내여야 유효한 타겟으로 인정")] 
    public float facingToleranceDegrees = 100f;

    [Header("Warp")]
    [Tooltip("타겟 뒤쪽으로 이 거리만큼 떨어진 지점에 도착")]
    public float arrivalOffsetFromTarget = 1.2f;
    [Tooltip("입력 후 실제로 워프하기까지의 짧은 선딜레이(초). 0이면 즉시.")]
    public float warpDelay = 0f;
    [Tooltip("체크하면 도착 시 타겟과 같은 높이로 맞춤. 끄면 지금 서 있는 높이를 그대로 유지 (지형 파묻힘 방지, 기본 권장)")]
    public bool matchTargetHeight = false;
    [Tooltip("도착 순간 부여되는 무적 시간(초). 겹친 적 사이로 끼어드는 동안 피격 방지용")]
    public float arrivalInvincibility = 0.15f;
    [Tooltip("도착 지점에서 겹친 대상들을 밀어내는 힘")]
    public float arrivalPushForce = 6f;

    [Header("Ground Safety")]
    [Tooltip("도착 지점을 이 레이어들 기준으로 지형 위에 안전하게 스냅시킴 (Ground + OneWayPlatform)")]
    public LayerMask groundSnapLayer;

    [Header("Hit (Q와 동일한 방식 — 도착 후 등 뒤 근접 히트박스)")]
    public float knockbackPower = 5f;
    public float hitStopDuration = 0.08f;

    private float? _preferredFacingWorldDir;

    //[Header("Ripple (도착 파동)")]
    //public float rippleRadius = 2.5f;
    //public float damageMultiplier = 1f;
    //public float knockbackPower = 5f;

    // 이번 시전에서 CanExecute가 찾아낸 타겟을 ExecuteSkillBehavior까지 전달하기 위한 캐시.
    // (같은 애셋 인스턴스를 여러 캐릭터가 동시에 쓰지 않는다는 전제 하의 단순한 방식)
    //private Transform _cachedTarget;

    public override float? GetPreferredFacingDirection() => _preferredFacingWorldDir;

    // 사거리 내 유효 타겟이 없으면 아예 발동하지 않음 (마나도 소모되지 않음)
    public override bool CanExecute(PlayerCombat combat, out string failReason)
    {
        //_cachedTarget = FindTarget(combat);

        if (FindTarget(combat) == null)
        {
            failReason = "타겟이 없거나 너무 멀리 있습니다";
            return false;
        }
        failReason = null;
        return true;
    }

    // 우선순위: (바라보는 방향 안에 있는 대상만 후보) → 최근 나를 공격한 대상 > 체력이 적을수록 > 가까울수록
    private Transform FindTarget(PlayerCombat combat)
    {
        var stats = combat.GetComponent<CharacterStats>();

        // Q 스킬과 동일한 스프라이트 기본 방향 보정 (실제로 반대로 보이면 이 줄의 부호만 뒤집어서 조정하세요)
        float worldFacing = combat.FacingDirection * -1f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(combat.transform.position, targetSearchRadius, targetableLayers);

        CharacterStats best = null;
        float bestScore = float.MinValue;

        foreach (var hit in hits)
        {
            if (!CombatTargetingUtility.TryGetValidTarget(hit, stats, out CharacterStats candidate)) continue; 

            Vector2 toTarget = (Vector2)candidate.transform.position - (Vector2)combat.transform.position;
            if (toTarget.sqrMagnitude < 0.0001f) continue;

            float angle = Vector2.Angle(new Vector2(worldFacing, 0f), toTarget);
            if (angle > facingToleranceDegrees) continue; // 바라보는 방향 밖이면 후보 제외

            float dist = toTarget.magnitude;
            float hpRatio = candidate.maxHealth > 0 ? (float)candidate.currentHealth / candidate.maxHealth : 1f;

            float score = 0f;
            if (stats.LastAttacker == candidate) score += 1000f;   // 최우선: 최근 나를 공격한 대상
            score += (1f - hpRatio) * 100f;                        // 체력이 적을수록 가산
            score -= dist * 5f;                                    // 멀수록 감점

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best != null ? best.transform : null;
    }

    public override IEnumerator ExecuteSkillBehavior(PlayerCombat combat, Rigidbody2D rb, Animator anim, CharacterStats stats)
    {
        Transform target = FindTarget(combat); // ★ 필드 캐시 대신 그 순간 다시 탐색 — 공유 상태 경쟁 자체를 제거
        Debug.Log($"[DBG W] target={(target != null ? target.name : "null")}, 플레이어={combat.transform.position}"); // *
        if (target == null) yield break; // 안전장치 (CanExecute를 통과했다면 원래는 null이 아니어야 함)

        // 타겟의 반대편(뒤쪽) 좌표 계산 
        float sideDir = (combat.transform.position.x < target.position.x) ? 1f : -1f; 
        float arrivalY = matchTargetHeight ? target.position.y : rb.position.y; // ★ 기본은 현재 높이 유지 (파묻힘 방지)
        Vector2 arrivalPos = new Vector2(target.position.x + sideDir * arrivalOffsetFromTarget, arrivalY);

        if (warpDelay > 0f) yield return new WaitForSeconds(warpDelay);

        // 이동 위치
        Vector2 desiredPos = new Vector2(target.position.x + sideDir * arrivalOffsetFromTarget, matchTargetHeight ? target.position.y : rb.position.y);
        Vector2 safePos = combat.ResolveSafeGroundedPosition(desiredPos, groundSnapLayer); // ★ 절벽/경사면 안전 보정
        Debug.Log($"[DBG W] desiredPos={desiredPos}, safePos={safePos}, sideDir={sideDir}"); // *

        rb.position = safePos;

        rb.linearVelocity = Vector2.zero; // 순간이동 직후 잔여 낙하/이동 관성 제거
        Physics2D.SyncTransforms();
        CameraDirector.Instance?.SnapToCurrentTargets(); // ** 카메라 즉시 스냅
        CameraDirector.Instance?.TriggerRecenter(0.5f); // ★ 순간이동 직후 0.5초간 중점 프레이밍, 이후 자연스럽게 평소 추적으로 복귀
        combat.PlayCurrentSkillVFX("arrival");

        // 즉시 반영하지 않고 값만 저장 — 실제 적용은 OnAttackCombo 시점
        _preferredFacingWorldDir = -sideDir;

        // 도착 순간 안전장치: 짧은 무적 + 겹친 대상 밀어내기
        stats.GrantTemporaryInvincibility(arrivalInvincibility);
        PushOverlappingColliders(arrivalPos, rb);

        // --- '보라색 파동' 버전에서 쓰던 로직. 근접 히트박스 방식으로 변경하며 사용 중단.
        //     나중에 다시 파동 방식으로 되돌릴 수도 있어 삭제하지 않고 주석 처리만 해둠. ---
        // [Header("Ripple (도착 파동)")]
        // public float rippleRadius = 2.5f;
        // private void SpawnRipple(PlayerCombat combat, CharacterStats casterStats, Vector2 center)
        // {
        //     Collider2D[] hits = Physics2D.OverlapCircleAll(center, rippleRadius, enemyLayer);
        //     foreach (var hit in hits) { ... TakeDamage / ApplyKnockback ... }
        // }
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

    // Q(Sora_Q_MeleeDashSkill)와 완전히 동일한 구조: 지속시간 동안 매 프레임 히트박스를 갱신하며 판정
    public override IEnumerator ExecuteHitbox(PlayerCombat combat, Transform parentTransform, CharacterStats stats)
    {
        float elapsed = 0f;
        HashSet<Collider2D> alreadyHit = new HashSet<Collider2D>();
        var (offset, size) = ResolveHitbox(combat.CurrentSkillContext); // 컨텍스트별 히트박스 오버라이드 반영

        while (elapsed < activeDuration)
        {
            float dir = combat.FacingDirection;
            Vector2 currentOffset = new Vector2(offset.x * dir, offset.y);
            Vector2 center = (Vector2)parentTransform.position + currentOffset;

            combat.SetDebugHitbox(center, size);

            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, targetableLayers);
            foreach (var hit in hits)
            {
                if (alreadyHit.Contains(hit)) continue;
                if (!CombatTargetingUtility.TryGetValidTarget(hit, stats, out CharacterStats enemyStats)) continue; //?
                alreadyHit.Add(hit);

                int damage = Mathf.RoundToInt(stats.attack.GetValue() * GetDamageMultiplier(1));
                Vector2 kbDir = ((Vector2)(hit.transform.position - parentTransform.position)).normalized;

                enemyStats.TakeDamage(damage, stats.currentElement, stats, kbDir, knockbackPower); // ★ 한 줄로 통합

                combat.TriggerHitStop(hitStopDuration);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        combat.ClearDebugHitbox();
    }

#if UNITY_EDITOR
    // 5번 항목: 타겟 탐색 반경을 씬 뷰에서 바로 확인 가능 (보라색 원). 히트박스 자체는 SkillBase 기본 기즈모(SetDebugHitbox)로 이미 보임.
    public override void DrawEditorGizmos(Vector3 basePos, float facingDir)
    {
        base.DrawEditorGizmos(basePos, facingDir); // ★ 컨텍스트별 히트박스 미리보기 공통 로직 재사용
        Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.35f);
        Gizmos.DrawWireSphere(basePos, targetSearchRadius);
    }
#endif
}