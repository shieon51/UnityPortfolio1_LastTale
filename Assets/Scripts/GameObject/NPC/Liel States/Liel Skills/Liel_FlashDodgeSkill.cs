using System.Collections;
using UnityEngine;

// 리엘 회피 - '섬광'
[CreateAssetMenu(menuName = "LastMarchan/NPC Skills/Liel/Flash Dodge")]
public class Liel_FlashDodgeSkill : NPCActionBase
{
    [Header("Trigger")]
    public float triggerWithinDistance = 3f;
    public float baseScore = 60f;
    public float dodgeDistance = 4f;
    public float dodgeDuration = 0.3f;

    public override float EvaluateScore(NPCDecisionContext ctx)
    {
        if (!IsContextAllowed(ctx.Self.CurrentMovementContext)) return 0f;
        if (ctx.DistanceToPlayer > triggerWithinDistance) return 0f;
        if (!ctx.PlayerIsAttacking) return 0f; // 위협적인 예고 중일 때만
        return baseScore;
    }

    public override IEnumerator Execute(NPC self, NPCVisual visual, Transform target)
    {
        self.GrantTemporaryInvincibility(dodgeDuration + 0.1f);
        Vector2 startPos = self.transform.position;
        float dir = self.SpriteRenderer.flipX ? -1f : 1f;
        Vector2 endPos = startPos + new Vector2(dir * dodgeDistance, 0f);

        self.GetComponent<AfterimageEffect>()?.Play(dodgeDuration); // 소라 Q에 쓰던 잔상 재활용
        visual.PlayImmediate("Dodge"); // ★ 애니메이터에 State 추가 필요

        float t = 0f;
        while (t < dodgeDuration)
        {
            t += Time.deltaTime;
            self.Rb.position = Vector2.Lerp(startPos, endPos, t / dodgeDuration);
            yield return null;
        }
        self.Rb.position = endPos;
        self.GetComponent<AfterimageEffect>()?.Stop();
    }
}