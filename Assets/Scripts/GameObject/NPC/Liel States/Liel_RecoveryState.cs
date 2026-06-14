using UnityEngine;

// °ø°Ý ÈÄ Àá½Ã ¸ØÃß´Â 'ºóÆ´'À» ¸¸µå´Â »óÅÂ
public class Liel_RecoveryState : NPCState
{
    private Liel_AI liel;
    private float duration;
    private float timer;

    public Liel_RecoveryState(Liel_AI npc, Animator anim, Transform p, float time) : base(npc, anim, p)
    {
        liel = npc;
        duration = time;
    }

    public override void Enter()
    {
        timer = 0f;
        animator.SetBool("IsWalk", false);
    }

    public override void Execute()
    {
        timer += Time.deltaTime;
        if (timer >= duration)
        {
            liel.StateMachine.ChangeState(new Liel_BattleIdleState(liel, animator, player));
        }
    }
}