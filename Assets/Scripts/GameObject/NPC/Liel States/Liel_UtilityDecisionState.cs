using UnityEngine;

// Liel_UtilityDecisionState.cs (신규, Liel_BattleIdleState를 대체)
public class Liel_UtilityDecisionState : NPCState
{
    private Liel_AI liel;
    private NPCUtilityAI ai;

    public Liel_UtilityDecisionState(Liel_AI npc, NPCVisual visual, Transform p) : base(npc, visual, p)
    {
        liel = npc;
        ai = npc.GetComponent<NPCUtilityAI>();
    }

    public override void Execute()
    {
        if (player == null) return;
        liel.LookAtPlayer_Public();

        NPCActionBase chosen = ai.ChooseNextAction(BuildContext());
        if (chosen != null)
        {
            liel.StateMachine.ChangeState(new Liel_ExecutingActionState(liel, visual, player, chosen));
        }
        else
        {
            visual.PlayIfChanged(NPCAnimStateNames.Idle);
        }
    }

    private NPCDecisionContext BuildContext()
    {
        var playerCombat = player.GetComponentInChildren<PlayerCombat>();
        return new NPCDecisionContext
        {
            Self = liel,
            Player = player,
            DistanceToPlayer = Vector2.Distance(liel.transform.position, player.position),
            SelfHealthPercent = liel.maxHealth > 0 ? (float)liel.currentHealth / liel.maxHealth : 0f,
            SelfManaPercent = liel.maxMana > 0 ? (float)liel.currentMana / liel.maxMana : 0f,
            PlayerIsAttacking = playerCombat != null && playerCombat.IsAttacking,
        };
    }

    public override void Exit() => visual.PlayIfChanged(NPCAnimStateNames.Idle);
}