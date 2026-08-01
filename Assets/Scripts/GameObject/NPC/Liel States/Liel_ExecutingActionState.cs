using System.Collections;
using UnityEngine;

// Liel_ExecutingSkillState.cs  — 선택된 스킬을 예고 → 실행 순서로 재생
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

                    if (WorldSpaceTelegraphIndicator.Instance != null)
                    {
                        WorldSpaceTelegraphIndicator.Instance.SetFollowTarget(liel.transform);
                        // 예고는 leadTime 전체 동안 독립적으로 진행 (Execute()의 타이밍과 별개)
                        liel.StartCoroutine(WorldSpaceTelegraphIndicator.Instance.PlayCountdown(leadTime));
                    }
                }

                // ★ 핵심: 예고가 선딜보다 길면, 그 차이만큼 '가만히 서서 예고만' 먼저 보여줌
                float preDelay = Mathf.Max(0f, leadTime - skill.timeToActive);
                if (preDelay > 0f)
                {
                    visual.PlayIfChanged(NPCAnimStateNames.Idle);
                    yield return new WaitForSeconds(preDelay);
                }

                liel.UseMana(skill.GetManaCost(1));
            }

            yield return liel.StartCoroutine(_action.Execute(liel, visual, player));
            liel.GetComponent<NPCUtilityAI>().NotifyUsed(_action);
        }
        finally
        {
            // 도중에 무슨 예외가 나든, 반드시 회복 상태로 넘어가고 회전 잠금을 풀어서
            // '영원히 멈춤' 사고를 원천 차단한다.
            liel.StateMachine.ChangeState(new Liel_RecoveryState(liel, visual, player, _action.recoveryDuration));
        }
    }
    public override void Exit() => liel.canRotate = true;
}