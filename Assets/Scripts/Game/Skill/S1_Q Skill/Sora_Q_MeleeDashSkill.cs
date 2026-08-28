using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Melee Dash Skill", menuName = "LastMarchan/Skills/Melee Dash")]
public class Sora_Q_MeleeDashSkill : SkillBase
{
    [Header("Combat Feel")]
    public float dashSpeed = 10f;
    public float dashDuration = 0.1f;
    public float hitStopDuration = 0.08f;
    //public float damageMultiplier = 1f;

    // 앞으로 치고 나가는 동작
    public override IEnumerator ExecuteSkillBehavior(PlayerCombat combat, Rigidbody2D rb, Animator anim, CharacterStats stats)
    {
        // 시각적으로 반전된 flipX 정보를 가져와서 완벽하게 방향을 잡음
        float dir = combat.FacingDirection * -1; // ** 현재 스프라이트 기본 방향이 '왼쪽'이라 임시로 -1 곱함

        Vector2 savedVelocity = rb.linearVelocity;
        float originalGravity = rb.gravityScale;

        // 공중에서 공격할 때 떨어지지 않게 체공
        rb.gravityScale = 0f;

        combat.GetComponent<AfterimageEffect>()?.Play(dashDuration); // ★ 루프 밖으로, 딱 한 번만

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            if (stats.isKnockedBack)
            {
                rb.gravityScale = originalGravity;
                combat.GetComponent<AfterimageEffect>()?.Stop(); // ★ 조기 종료 시에도 반드시 꺼줌 (빠져있었습니다)
                yield break;
            }
            float currentSpeed = Mathf.Lerp(dashSpeed, 0f, elapsed / dashDuration);
            // 바라보는 방향으로 전진
            rb.linearVelocity = new Vector2(dir * currentSpeed, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        combat.GetComponent<AfterimageEffect>()?.Stop(); 
        rb.gravityScale = originalGravity;
        rb.linearVelocity = savedVelocity;
    }

    // 다단히트 방지 히트박스 판정
    public override IEnumerator ExecuteHitbox(PlayerCombat combat, Transform parentTransform, CharacterStats stats)
    {
        float elapsed = 0f;
        HashSet<Collider2D> alreadyHitEnemies = new HashSet<Collider2D>();
        Vector2 attackOriginPos = parentTransform.position;

        while (elapsed < activeDuration)
        {
            // while 문 안에서 매 프레임마다 플레이어의 위치를 다시 가져오므로, 
            // 점프나 대시 중에도 히트박스가 플레이어를 완벽하게 따라다님
            float dir = combat.FacingDirection;
            var (offset, size) = ResolveHitbox(combat.CurrentSkillContext); // * 컨텍스트별 오버라이드가 있으면 그걸 사용

            Vector2 currentOffset = new Vector2(offset.x * dir, offset.y);
            Vector2 center = (Vector2)parentTransform.position + currentOffset;

            // 기즈모 그리기 위해 최신 데이터 넘겨줌 (실시간 반영)
            combat.SetDebugHitbox(center, size);

            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, targetableLayers);
            bool hitSomething = false;

            foreach (var hit in hits)
            {
                if (alreadyHitEnemies.Contains(hit)) continue;
                if (!CombatTargetingUtility.TryGetValidTarget(hit, stats, out CharacterStats enemyStats)) continue;
                alreadyHitEnemies.Add(hit);

                int finalDamage = (int)(stats.attack.GetValue() * GetDamageMultiplier(1));
                Vector2 knockbackDir = (hit.transform.position - parentTransform.position).normalized;

                enemyStats.TakeDamage(finalDamage, stats.currentElement, stats, knockbackDir, 5f, attackOriginPos); // ★ 추가
                hitSomething = true;
            }

            if (hitSomething) combat.TriggerHitStop(hitStopDuration);

            elapsed += Time.deltaTime;
            yield return null;
        }
        // 궤적 판정 끝나면 기즈모 끔
        combat.ClearDebugHitbox();
    }
}