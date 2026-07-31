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

        NPCSkillBase chosen = ai.ChooseNextAction(BuildContext());
        if (chosen != null)
        {
            liel.StateMachine.ChangeState(new Liel_ExecutingSkillState(liel, visual, player, chosen));
            return;
        }

        // 마땅한 스킬이 없으면 기본 거리 유지 행동
        float dist = Vector2.Distance(liel.transform.position, player.position);
        if (dist > liel.preferredEngageRange) // ?
        {
            visual.PlayIfChanged(NPCAnimStateNames.Walk);
            float dir = (player.position.x > liel.transform.position.x) ? 1f : -1f;
            liel.transform.position += new Vector3(dir * 3f * Time.deltaTime, 0, 0);
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