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

        // 1. 공격하기 딱 좋은 거리 (Min과 Max 사이)
        if (dist <= liel.attackCompo.maxAttack1Range && dist >= liel.attackCompo.minAttack1Range)
        {
            liel.StateMachine.ChangeState(new Liel_Attack1State(liel, animator, player));
        }
        // 2. 너무 가까운 경우 (추후 뒤로 대시하는 백스텝 상태로 연결될 곳)
        else if (dist < liel.attackCompo.minAttack1Range)
        {
            animator.SetBool("IsWalk", true);
            // 플레이어의 반대 방향으로 걸어서 거리 벌리기 (임시)
            float dir = (player.position.x > liel.transform.position.x) ? -1f : 1f;
            liel.transform.position += new Vector3(dir * 2f * Time.deltaTime, 0, 0);
        }
        // 3. 너무 멀 경우 (다가가기)
        else
        {
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