using System.Collections;
using UnityEngine;

// NPCMovementAction.cs (신규) — 접근/후퇴처럼 순수 위치 조정 행동. 스킬과 동등한 자격으로 AI가 저울질함.
[CreateAssetMenu(menuName = "LastMarchan/NPC Skills/Movement Action")]
public class NPCMovementAction : NPCActionBase
{
    public enum MoveDirection { TowardPlayer, AwayFromPlayer }

    [Header("Trigger Range (두 원 판정)")]
    [Tooltip("이 거리보다 가까울 때만 유효 (안쪽 원 = 후퇴용). 0이면 무시")]
    public float triggerBelowDistance = 0f;
    [Tooltip("이 거리보다 멀 때만 유효 (바깥쪽 원 = 접근용). 0이면 무시")]
    public float triggerAboveDistance = 0f;

    [Header("Movement")]
    public MoveDirection direction = MoveDirection.TowardPlayer;
    public float moveSpeed = 3f;
    public float duration = 0.5f;

    [Header("Weight")]
    [Tooltip("공격 스킬들보다 낮게 잡으면 '가능하면 공격, 안 되면 이동'이 자연스럽게 됩니다")]
    public float baseScore = 40f;

    public override float EvaluateScore(NPCDecisionContext ctx)
    {
        if (triggerBelowDistance > 0f && ctx.DistanceToPlayer >= triggerBelowDistance) return 0f;
        if (triggerAboveDistance > 0f && ctx.DistanceToPlayer <= triggerAboveDistance) return 0f;
        return baseScore;
    }

    public override IEnumerator Execute(NPC self, NPCVisual visual, Transform target)
    {
        visual.PlayImmediate(NPCAnimStateNames.Walk);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float toward = (target.position.x > self.transform.position.x) ? 1f : -1f;
            float dir = direction == MoveDirection.TowardPlayer ? toward : -toward;
            self.transform.position += new Vector3(dir * moveSpeed * Time.deltaTime, 0, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

#if UNITY_EDITOR
    public override void DrawEditorGizmos(Vector3 basePos, float facingDir)
    {
        if (triggerBelowDistance > 0f) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(basePos, triggerBelowDistance); }
        if (triggerAboveDistance > 0f) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(basePos, triggerAboveDistance); }
    }
#endif
}