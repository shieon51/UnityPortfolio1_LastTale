using UnityEngine;
using static Liel_AI;

public class Liel_BattleIdleState : NPCState
{
    private Liel_AI liel;

    public Liel_BattleIdleState(Liel_AI npc, Animator anim, Transform p) : base(npc, anim, p)
    {
        liel = npc;
    }

    public override void Execute()
    {
        if (player == null) return;

        liel.LookAtPlayer_Public();
        float dist = Vector2.Distance(liel.transform.position, player.position);

        if (dist <= liel.attack1Range)
        {
            // 💡 사거리 안이면 공격 1 상태로 전환!
            liel.StateMachine.ChangeState(new Liel_Attack1State(liel, animator, player));
        }
        else
        {
            // 멀면 쫓아감
            animator.SetBool("IsWalk", true);
            float dir = (player.position.x > liel.transform.position.x) ? 1f : -1f;
            liel.transform.position += new Vector3(dir * 3f * Time.deltaTime, 0, 0);
        }
    }

    public override void Exit()
    {
        animator.SetBool("IsWalk", false);
    }
}