using UnityEngine;

// Liel_AttackState.cs (신규, Liel_Attack1State.cs를 대체) — 어떤 공격이든 재사용 가능하게 일반화
public class Liel_AttackState : NPCState
{
    private Liel_AI liel;
    private readonly string _animStateName;
    private readonly float _duration;
    private float _timer;

    public Liel_AttackState(Liel_AI npc, NPCVisual visual, Transform p, string animStateName, float duration)
        : base(npc, visual, p)
    {
        liel = npc;
        _animStateName = animStateName;
        _duration = duration;
    }

    public override void Enter()
    {
        _timer = 0f;
        liel.canRotate = false;
        visual.PlayImmediate(_animStateName); // 공격은 매번 확실히 처음부터 재생
    }

    public override void Execute()
    {
        _timer += Time.deltaTime;
        if (_timer >= _duration)
            liel.StateMachine.ChangeState(new Liel_RecoveryState(liel, visual, player, 0.8f));
    }

    public override void Exit() => liel.canRotate = true;
}