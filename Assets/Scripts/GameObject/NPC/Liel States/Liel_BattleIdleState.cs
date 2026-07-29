using UnityEngine;
using static Liel_AI;

public class Liel_BattleIdleState : NPCState
{
    private Liel_AI liel;

    public Liel_BattleIdleState(Liel_AI npc, NPCVisual visual, Transform p) : base(npc, visual, p)
    {
        liel = npc;
    }

    public override void Execute()
    {
        if (player == null) return;

        liel.LookAtPlayer_Public();
        float dist = Vector2.Distance(liel.transform.position, player.position);

        // 거리 & npc 마나 계산 - 부족하면 공격 안 함
        bool inAttackRange = dist <= liel.attackCompo.maxAttack1Range && dist >= liel.attackCompo.minAttack1Range;
        bool hasEnoughMana = liel.currentMana >= liel.attackCompo.attack1ManaCost;

        // 1. 공격하기 딱 좋은 거리 (Min과 Max 사이)
        if (inAttackRange && hasEnoughMana)
        {
            liel.StateMachine.ChangeState(new Liel_AttackState(liel, visual, player, NPCAnimStateNames.Attack1, 1.5f));
        }
        // 2. 너무 가까운 경우 (추후 뒤로 대시하는 백스텝 상태로 연결될 곳)
        else if (dist < liel.attackCompo.minAttack1Range)
        {
            visual.PlayIfChanged(NPCAnimStateNames.Walk);
            float dir = (player.position.x > liel.transform.position.x) ? -1f : 1f;
            liel.transform.position += new Vector3(dir * 2f * Time.deltaTime, 0, 0);
        }
        // 3. 너무 멀 경우 (다가가기) / 마나 부족
        else
        {
            // 사거리 밖이거나 마나 부족 → 접근하며 대기(마나 회복 기대)
            bool tooFar = dist > liel.attackCompo.maxAttack1Range;
            visual.PlayIfChanged(tooFar ? NPCAnimStateNames.Walk : NPCAnimStateNames.Idle);
            if (tooFar)
            {
                float dir = (player.position.x > liel.transform.position.x) ? 1f : -1f;
                liel.transform.position += new Vector3(dir * 3f * Time.deltaTime, 0, 0);
            }
        }
    }
    public override void Exit() => visual.PlayIfChanged(NPCAnimStateNames.Idle);
}