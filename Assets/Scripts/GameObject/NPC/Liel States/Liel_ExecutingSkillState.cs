using System.Collections;
using UnityEngine;

// Liel_ExecutingSkillState.cs  — 선택된 스킬을 예고 → 실행 순서로 재생
public class Liel_ExecutingSkillState : NPCState
{
    private Liel_AI liel;
    private NPCSkillBase _skill;

    public Liel_ExecutingSkillState(Liel_AI npc, NPCVisual visual, Transform p, NPCSkillBase skill) : base(npc, visual, p)
    {
        liel = npc;
        _skill = skill;
    }

    public override void Enter()
    {
        liel.canRotate = false;
        liel.StartCoroutine(RunSkill());
    }

    private IEnumerator RunSkill()
    {
        if (_skill.hasTelegraph && WorldSpaceTelegraphIndicator.Instance != null)
        {
            WorldSpaceTelegraphIndicator.Instance.SetFollowTarget(liel.transform);
            var defender = PlayerManager.Instance.CurrentCharacter;
            float duration = CombatFormulaService.Instance.CalculateTelegraphDuration(_skill.baseTelegraphDuration, liel, defender);
            yield return liel.StartCoroutine(WorldSpaceTelegraphIndicator.Instance.PlayCountdown(duration));
        }

        liel.UseMana(_skill.GetManaCost(1));
        yield return liel.StartCoroutine(_skill.Execute(liel, visual, player));

        liel.GetComponent<NPCUtilityAI>().NotifyUsed(_skill);
        liel.StateMachine.ChangeState(new Liel_RecoveryState(liel, visual, player, 0.5f));
    }

    public override void Exit() => liel.canRotate = true;
}