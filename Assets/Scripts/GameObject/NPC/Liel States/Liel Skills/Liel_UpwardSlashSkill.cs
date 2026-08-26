using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/NPC Skills/Liel/Upward Slash")]
public class Liel_UpwardSlashSkill : NPCSkillBase
{
    [Header("Range (Attack1의 minRange와 안 겹치게)")]
    [Tooltip("이 거리 이하일 때만 후보가 됨")]
    public float maxRange = 0.8f;

    [Header("Hitbox")]
    public HitboxSettings hitbox; // Liel_MeleeAttackSkill.cs에 이미 있는 struct 재사용

    [Header("Utility AI Weights")]
    [Tooltip("너무 붙었을 때 강하게 우선시되도록 Attack1보다 높게")]
    public float baseScore = 85f;

    public override float EvaluateScore(NPCDecisionContext ctx)
    {
        if (!IsContextAllowed(ctx.Self.CurrentMovementContext)) return 0f;
        if (ctx.Self.currentMana < GetManaCost(1)) return 0f;
        if (ctx.DistanceToPlayer > maxRange) return 0f; // 근접 전용
        return baseScore;
    }

    public override IEnumerator Execute(NPC self, NPCVisual visual, Transform target)
    {
        self.CurrentPlayingSkill = this;
        visual.SetEyesVisible(false);
        self.isSuperArmor = true;

        var context = self.CurrentMovementContext;
        string resolvedAnim = ResolveAnimStateName(context, animStateName);
        var (hitOffset, hitSize) = ResolveContextHitbox(context, hitbox.offset, hitbox.size);

        visual.PlayImmediate(resolvedAnim); // 제자리라 대시 관련 대기 없이 바로 재생

        yield return WaitForHitboxStart(); // AE_HitboxStart — 검이 위로 올라오며 맞는 타이밍

        PlaySkillVFX("slash", self);
        CameraDirector.Instance?.Shake(0.15f, 0.15f);

        var hitboxCoroutine = self.StartCoroutine(ActiveHitboxRoutine(self, hitOffset, hitSize));

        yield return WaitForActionEndEvent(); // AE_ActionEnd
        yield return hitboxCoroutine;

        self.isSuperArmor = false;
        visual.SetEyesVisible(true);
        self.CurrentPlayingSkill = null;
    }

    private IEnumerator ActiveHitboxRoutine(NPC self, Vector2 offset, Vector2 size)
    {
        float elapsed = 0f;
        var alreadyHit = new HashSet<Collider2D>();
        float dir = self.SpriteRenderer.flipX ? 1f : -1f;
        Vector2 fixedCenter = (Vector2)self.transform.position + new Vector2(offset.x * dir, offset.y);

        while (elapsed < hitbox.activeDuration)
        {
            self.SetDebugHitbox(fixedCenter, size);
            Collider2D[] hits = Physics2D.OverlapBoxAll(fixedCenter, size, 0f, targetableLayers);
            foreach (var hit in hits)
            {
                if (alreadyHit.Contains(hit)) continue;
                if (!CombatTargetingUtility.TryGetValidTarget(hit, self, out CharacterStats targetStats)) continue;
                alreadyHit.Add(hit);

                Vector2 kbDir = ((Vector2)hit.transform.position - (Vector2)self.transform.position).normalized;
                targetStats.TakeDamage(
                    Mathf.RoundToInt(self.attack.GetValue() * GetDamageMultiplier(1)),
                    self.currentElement, self, kbDir, hitbox.knockbackPower);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        self.ClearDebugHitbox();
    }

#if UNITY_EDITOR
    public override void DrawEditorGizmos(Vector3 basePos, float facingDir)
    {
        Gizmos.color = new Color(1f, 0f, 1f, 0.2f);
        Gizmos.DrawWireSphere(basePos, maxRange);
        Gizmos.color = Color.cyan;
        Vector2 center = (Vector2)basePos + new Vector2(hitbox.offset.x * facingDir, hitbox.offset.y);
        Gizmos.DrawWireCube(center, hitbox.size);
    }
#endif
}