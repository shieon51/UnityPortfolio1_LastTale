using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/NPC Skills/Guard Action")]
public class NPCGuardAction : NPCActionBase
{
    [Header("Trigger")]
    public float triggerWithinDistance = 3f;
    public float baseScore = 50f;
    public float duration = 1.0f;
    public string defaultAnimStateName = "Guard";

    [Header("체력 연동")]
    public float lowHealthGuardWeight = 25f;

    public override float EvaluateScore(NPCDecisionContext ctx)
    {
        if (!IsContextAllowed(ctx.Self.CurrentMovementContext)) return 0f;
        if (ctx.DistanceToPlayer > triggerWithinDistance) return 0f;
        if (!ctx.PlayerIsAttacking) return 0f; // 원래 기획서 조건: 플레이어가 강공격 예고 중일 때

        float score = baseScore;
        score += (1f - ctx.SelfHealthPercent) * lowHealthGuardWeight; 
        return score;
    }

    public override IEnumerator Execute(NPC self, NPCVisual visual, Transform target)
    {
        string state = ResolveAnimStateName(self.CurrentMovementContext, defaultAnimStateName);
        visual.PlayImmediate(state);
        self.StartGuard();
        yield return new WaitForSeconds(duration);
        self.StopGuard();
    }
}