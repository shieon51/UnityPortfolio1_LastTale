using UnityEngine;

public class Liel_Attack1State : NPCState
{
    private Liel_AI liel;
    private float attackTimer = 0f;
    private float attackDuration = 1.5f; // 애니메이션 재생 길이

    public Liel_Attack1State(Liel_AI npc, Animator anim, Transform p) : base(npc, anim, p)
    {
        liel = npc;
    }

    public override void Enter()
    {
        attackTimer = 0f;
        animator.SetTrigger("Attack1");
        Debug.Log("[Liel] 소라에게 근접 공격 1 시전!");
    }

    public override void Execute()
    {
        attackTimer += Time.deltaTime;

        // 애니메이션이 끝나면 다시 전투 대기로 복귀
        if (attackTimer >= attackDuration)
        {
            liel.StateMachine.ChangeState(new Liel_BattleIdleState(liel, animator, player));
        }
    }
}