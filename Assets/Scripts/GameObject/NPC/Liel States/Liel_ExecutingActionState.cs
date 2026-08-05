// Liel_ExecutingActionState.cs 전체 교체
using System.Collections;
using UnityEngine;

public class Liel_ExecutingActionState : NPCState
{
    private Liel_AI liel;
    private NPCActionBase _action;

    public Liel_ExecutingActionState(Liel_AI npc, NPCVisual visual, Transform p, NPCActionBase action) : base(npc, visual, p)
    {
        liel = npc;
        _action = action;
    }

    public override void Enter()
    {
        liel.canRotate = false;
        liel.StartCoroutine(RunAction());
    }

    private IEnumerator RunAction()
    {
        try
        {
            if (_action is NPCSkillBase skill)
            {
                float leadTime = 0f;
                if (skill.hasTelegraph)
                {
                    var defender = PlayerManager.Instance.CurrentCharacter;
                    leadTime = CombatFormulaService.Instance.CalculateTelegraphDuration(skill.baseTelegraphDuration, liel, defender);
                    leadTime = Mathf.Min(leadTime, NPCCombatTuning.MaxTelegraphLeadTime); // ★ 상한선

                    if (WorldSpaceTelegraphIndicator.Instance != null)
                    {
                        WorldSpaceTelegraphIndicator.Instance.SetFollowTarget(liel.transform);
                        liel.StartCoroutine(WorldSpaceTelegraphIndicator.Instance.PlayCountdown(leadTime));
                    }
                }

                float preDelay = Mathf.Max(0f, leadTime - skill.timeToActive);
                if (preDelay > 0f)
                    yield return liel.StartCoroutine(RepositionWhileWaiting(preDelay)); // ★ 가만히 안 서있고 계속 거리 조절

                liel.UseMana(skill.GetManaCost(1));
            }

            yield return liel.StartCoroutine(_action.Execute(liel, visual, player));
            liel.GetComponent<NPCUtilityAI>().NotifyUsed(_action);
        }
        finally
        {
            liel.StateMachine.ChangeState(new Liel_RecoveryState(liel, visual, player, _action.recoveryDuration));
        }
    }

    // 예고가 뜬 채로 대기하는 동안, 이동 행동만 계속 재평가하며 진행
    private IEnumerator RepositionWhileWaiting(float duration)
    {
        var ai = liel.GetComponent<NPCUtilityAI>();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            var movementAction = ai.ChooseMovementOnly(BuildContext());
            if (movementAction != null)
                yield return liel.StartCoroutine(movementAction.Execute(liel, visual, player));
            else
            {
                visual.PlayIfChanged(NPCAnimStateNames.Idle);
                yield return null;
            }
            elapsed += Time.deltaTime;
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

    public override void Exit() => liel.canRotate = true;
}