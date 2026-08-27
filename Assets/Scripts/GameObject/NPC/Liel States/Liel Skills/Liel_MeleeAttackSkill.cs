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

    [Tooltip("최고 속도까지 가속하는 시간(초). 짧을수록 '확 밟는' 느낌. 0이면 예전처럼 즉시 스냅")]
    public float accelRampDuration = 0.08f;
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
        if (!IsContextAllowed(ctx.Self.CurrentMovementContext)) return 0f;
        if (ctx.Self.currentMana < GetManaCost(1)) return 0f; // CSV 연동

        bool inRange = ctx.DistanceToPlayer >= minRange && ctx.DistanceToPlayer <= maxRange;
        if (!inRange) return 0f;

        float score = baseScore;
        if (ctx.LastUsedAction == this) score += comboBonusScore; // 연계 선호 (기획서 "행동 큐" 반영)
        return score;
    }

    public override IEnumerator Execute(NPC self, NPCVisual visual, Transform target)
    {
        self.CurrentPlayingSkill = this; // ★ 릴레이가 이 스킬을 찾을 수 있게 등록
        visual.SetEyesVisible(false); // ** 모든 공격 모션 앞에 넣을 것
        self.isSuperArmor = true; // ★ 처음부터 슈퍼아머 — 돌진 중 맞아서 넉백당해 멈추는 것 방지

        var context = self.CurrentMovementContext; // 지금은 항상 Grounded, 나중에 비행/점프 붙으면 자동 확장됨
        string resolvedAnim = ResolveAnimStateName(context, animStateName);
        var (hitOffset, hitSize) = ResolveContextHitbox(context, hitbox.offset, hitbox.size);

        var rb = self.Rb;
        var sr = self.SpriteRenderer;
        float originalDrag = rb.linearDamping;

        // 1. 준비동작
        visual.PlayImmediate(resolvedAnim);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        yield return WaitForDashStart(); // AE_DashStart

        // 2. 팍 치고 대시 돌진 (가속 곡선)
        rb.linearDamping = 0f;
        float dir = sr.flipX ? 1f : -1f;
        yield return self.StartCoroutine(BurstAccelerate(rb, dir));

        yield return WaitForHitboxStart(); // AE_HitboxStart — 한발 내밀며 찌르기

        // 3. 찌르기: 판정 + 이펙트
        PlaySkillVFX("stab", self);
        CameraDirector.Instance?.DirectionalShake(0.2f, 0.15f, new Vector2(dir, 0)); // 카메라 흔들림
        var hitboxCoroutine = self.StartCoroutine(ActiveHitboxRoutine(self, hitOffset, hitSize, dir));

        yield return WaitForSlideStart(); // AE_SlideStart — 끼익 멈춤 시작

        // 4. 마찰 감속
        rb.linearDamping = dash.slideDrag;
        yield return WaitForActionEndEvent(); // AE_ActionEnd — 애니메이션 끝

        yield return hitboxCoroutine; // 혹시 아직 안 끝났으면 마저 대기

        // 5. 후딜: 원상복귀
        rb.linearDamping = originalDrag;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        self.isSuperArmor = false; // ★ 여기서 해제
        visual.SetEyesVisible(true); // **
        self.CurrentPlayingSkill = null;
    }

    // Ease-Out 곡선으로 초반에 급가속, 끝에서 매끄럽게 최고속도 도달
    private IEnumerator BurstAccelerate(Rigidbody2D rb, float dir)
    {
        if (dash.accelRampDuration <= 0f)
        {
            rb.linearVelocity = new Vector2(dir * dash.burstSpeed, rb.linearVelocity.y);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < dash.accelRampDuration)
        {
            float t = elapsed / dash.accelRampDuration;
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic: 엑셀 확 밟는 느낌
            rb.linearVelocity = new Vector2(dir * dash.burstSpeed * eased, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = new Vector2(dir * dash.burstSpeed, rb.linearVelocity.y);
    }

    private IEnumerator ActiveHitboxRoutine(NPC self, Vector2 offset, Vector2 size, float dir)
    {
        float elapsed = 0f;
        var alreadyHit = new HashSet<Collider2D>();
        //Vector2 fixedCenter = (Vector2)self.transform.position + new Vector2(offset.x * dir, offset.y);

        while (elapsed < hitbox.activeDuration)
        {
            Vector2 currentCenter = (Vector2)self.transform.position + new Vector2(offset.x * dir, offset.y); // ★ 매 프레임 재계산 (기존엔 루프 밖에서 한 번만 계산했었음)
            self.SetDebugHitbox(currentCenter, size);
            Collider2D[] hits = Physics2D.OverlapBoxAll(currentCenter, size, 0f, targetableLayers);
            foreach (var hit in hits)
            {
                if (alreadyHit.Contains(hit)) continue;
                if (!CombatTargetingUtility.TryGetValidTarget(hit, self, out CharacterStats targetStats)) continue;
                alreadyHit.Add(hit);

                Vector2 kbDir = ((Vector2)hit.transform.position - (Vector2)self.transform.position).normalized;
                targetStats.TakeDamage(Mathf.RoundToInt(self.attack.GetValue() * GetDamageMultiplier(1)), self.currentElement, self, kbDir, hitbox.knockbackPower);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        self.ClearDebugHitbox();
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