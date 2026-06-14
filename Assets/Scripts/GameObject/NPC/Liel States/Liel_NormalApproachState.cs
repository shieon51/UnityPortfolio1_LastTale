using UnityEngine;

public class Liel_NormalApproachState : NPCState
{
    private Liel_AI liel;

    public Liel_NormalApproachState(Liel_AI npc, Animator anim, Transform p) : base(npc, anim, p)
    {
        liel = npc;
    }

    public override void Execute()
    {
        if (player == null || liel.CurrentRelationship < NPC.RelationshipTier.Friend)
        {
            animator.SetBool("IsWalk", false);
            return;
        }

        float dist = Vector2.Distance(liel.transform.position, player.position);

        if (dist <= liel.approachDistance && !liel.hasApproached)
        {
            liel.LookAtPlayer_Public(); // (접근을 위해 Liel_AI 쪽에 public 래퍼 함수 추가 예정)

            if (dist > liel.stopDistance)
            {
                animator.SetBool("IsWalk", true);
                float dir = (player.position.x > liel.transform.position.x) ? 1f : -1f;
                liel.transform.position += new Vector3(dir * liel.walkSpeed * Time.deltaTime, 0, 0);
            }
            else
            {
                animator.SetBool("IsWalk", false);
                liel.hasApproached = true;
            }
        }
        else
        {
            animator.SetBool("IsWalk", false);
            if (dist > liel.approachDistance * 1.5f) liel.hasApproached = false;
        }
    }
}