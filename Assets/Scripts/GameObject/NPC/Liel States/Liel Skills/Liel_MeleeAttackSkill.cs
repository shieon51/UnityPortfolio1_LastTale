using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// SOLID를 위한 설정 데이터 분리
[System.Serializable]
public class DashSettings
{
    [Header("빛 속성 대시 세팅")]
    [Tooltip("가속 없이 즉시 도달할 절대 속도 (팍- 치고 나감)")]
    public float burstSpeed = 30f;

    [Tooltip("급브레이크 마찰력 (20~30 정도로 확 높여야 끼이익! 하고 멈춤)")]
    public float slideDrag = 25f;

    [Tooltip("마찰로 완전히 멈추기까지 걸리는 시간 (직접 조절)")]
    public float slideDuration = 0.2f; // ★ 신규
}

[System.Serializable]
public class HitboxSettings
{
    public Vector2 size;
    public Vector2 offset;
    public float activeDuration = 0.15f;
    public float knockbackPower = 5f;
}

// Liel_MeleeAttackSkill.cs (신규) — LielAttackCompo.Attack1 로직을 데이터화
[CreateAssetMenu(menuName = "LastMarchan/NPC Skills/Liel/Melee Attack")]
public class Liel_MeleeAttackSkill : NPCSkillBase
{
    [Header("Range")]
    public float minRange = 0.8f;
    public float maxRange = 2.0f;

    [Header("Dash")]
    public DashSettings dash;

    [Header("Hitbox")]
    public HitboxSettings hitbox;

    [Header("Utility AI Weights")]
    [Tooltip("사거리 안일 때 기본 점수")]
    public float baseScore = 70f;
    [Tooltip("직전에도 이 스킬을 썼을 때(연계) 추가되는 점수")]
    public float comboBonusScore = 25f;

    public override float EvaluateScore(NPCDecisionContext ctx)
    {
        if (ctx.Self.currentMana < GetManaCost(1)) return 0f; // ★ CSV 연동
        bool inRange = ctx.DistanceToPlayer >= minRange && ctx.DistanceToPlayer <= maxRange;
        if (!inRange) return 0f;

        float score = baseScore;
        if (ctx.LastUsedAction == this) score += comboBonusScore; // 연계 선호 (기획서 "행동 큐" 반영)
        return score;
    }

    public override IEnumerator Execute(NPC self, NPCVisual visual, Transform target)
    {
        self.CurrentPlayingSkill = this; // ★ 릴레이가 이 스킬을 찾을 수 있게 등록

        var rb = self.Rb;
        var sr = self.SpriteRenderer;
        float originalDrag = rb.linearDamping;

        // 준비자세: 애니메이션만 재생, 물리적으로 완전 정지
        visual.PlayImmediate(animStateName);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        yield return WaitForActiveStart(); // AE_ActiveStart = 준비자세 끝, 팍 치고 나가는 순간

        // 액티브: 순간 최고속도로 돌진
        rb.linearDamping = 0f;
        float dir = sr.flipX ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * dash.burstSpeed, 0f);
        PlaySkillVFX("swing", self);

        yield return self.StartCoroutine(ActiveHitboxRoutine(self, sr));

        yield return WaitForActiveEnd(); // AE_ActiveEnd = 돌진 끝, 마찰 감속 시작하는 순간

        // 후딜: 마찰로 서서히 멈춤 (수치 조절 가능)
        rb.linearDamping = dash.slideDrag;
        yield return new WaitForSeconds(dash.slideDuration);

        rb.linearDamping = originalDrag;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // 완전 정지
        self.CurrentPlayingSkill = null;
    }

    private IEnumerator ActiveHitboxRoutine(NPC self, SpriteRenderer sr)
    {
        float elapsed = 0f;
        var alreadyHit = new HashSet<Collider2D>();
        self.isSuperArmor = true;

        float dir = sr.flipX ? -1 : 1;
        Vector2 fixedCenter = (Vector2)self.transform.position + new Vector2(hitbox.offset.x * dir, hitbox.offset.y);

        while (elapsed < hitbox.activeDuration)
        {
            self.SetDebugHitbox(fixedCenter, hitbox.size); // ★ 추가
            Collider2D[] hits = Physics2D.OverlapBoxAll(fixedCenter, hitbox.size, 0f, targetableLayers);
            foreach (var hit in hits)
            {
                if (alreadyHit.Contains(hit)) continue;
                if (!CombatTargetingUtility.TryGetValidTarget(hit, self, out CharacterStats targetStats)) continue;
                alreadyHit.Add(hit);

                targetStats.TakeDamage(Mathf.RoundToInt(self.attack.GetValue() * GetDamageMultiplier(1)), self.currentElement, self);
                Vector2 kbDir = ((Vector2)hit.transform.position - (Vector2)self.transform.position).normalized;
                targetStats.ApplyKnockback(kbDir, hitbox.knockbackPower);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        self.isSuperArmor = false;
        self.ClearDebugHitbox(); // ★ 추가
    }

#if UNITY_EDITOR
    public override void DrawEditorGizmos(Vector3 basePos, float facingDir)
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(basePos, maxRange);
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(basePos, minRange);

        Gizmos.color = Color.cyan;
        Vector2 center = (Vector2)basePos + new Vector2(hitbox.offset.x * facingDir, hitbox.offset.y);
        Gizmos.DrawWireCube(center, hitbox.size);
    }
#endif
}