using UnityEngine;

// 공격 후 잠시 멈추는 '빈틈'을 만드는 상태
public class Liel_RecoveryState : NPCState
{
    private Liel_AI liel;
    private float duration;
    private float timer;

    public Liel_RecoveryState(Liel_AI npc, NPCVisual visual, Transform p, float time) : base(npc, visual, p)
    {
        liel = npc;
        duration = time;
    }

    public override void Enter()
    {
        timer = 0f;
        if (!visual.IsShowingReactionPose)
        {
            visual.PlayIfChanged(NPCAnimStateNames.Idle); 
        }
    }

    public override void Execute()
    {
        timer += Time.deltaTime;
        if (timer >= duration)
        {
            liel.StateMachine.ChangeState(new Liel_UtilityDecisionState(liel, visual, player)); //?
        }
    }
}