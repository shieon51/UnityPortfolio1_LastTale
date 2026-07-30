using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    public override float EvaluateScore(NPCDecisionContext ctx)
    {
        if (ctx.Self.currentMana < manaCost) return 0f;
        bool inRange = ctx.DistanceToPlayer >= minRange && ctx.DistanceToPlayer <= maxRange;
        if (!inRange) return 0f;

        float score = 70f;
        if (ctx.LastUsedSkill == this) score += 25f; // 연계 선호 (기획서 "행동 큐" 반영)
        return score;
    }

    public override IEnumerator Execute(NPC self, NPCVisual visual, Transform target)
    {
        var rb = self.Rb;
        var sr = self.SpriteRenderer;
        float originalDrag = rb.linearDamping;

        visual.PlayImmediate(animStateName);

        yield return new WaitForSeconds(0.2f); // TODO: 실제 클립 완성되면 Animation Event 기반으로 교체

        rb.linearDamping = 0f;
        float dir = sr.flipX ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * dash.burstSpeed, 0f);

        yield return new WaitForSeconds(0.1f);
        rb.linearDamping = dash.slideDrag;

        yield return self.StartCoroutine(ActiveHitboxRoutine(self, sr));

        rb.linearDamping = originalDrag;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
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
            Collider2D[] hits = Physics2D.OverlapBoxAll(fixedCenter, hitbox.size, 0f, targetableLayers);
            foreach (var hit in hits)
            {
                if (alreadyHit.Contains(hit)) continue;
                if (!CombatTargetingUtility.TryGetValidTarget(hit, self, out CharacterStats targetStats)) continue;
                alreadyHit.Add(hit);

                targetStats.TakeDamage(self.attack.GetValue(), self.currentElement, self);
                Vector2 kbDir = ((Vector2)hit.transform.position - (Vector2)self.transform.position).normalized;
                targetStats.ApplyKnockback(kbDir, hitbox.knockbackPower);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        self.isSuperArmor = false;
    }
}