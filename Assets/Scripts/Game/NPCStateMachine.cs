
// 모든 상태가 상속받을 기본 인터페이스
using UnityEngine;

public interface IState
{
    void Enter();
    void Execute(); // Update에서 매 프레임 호출
    void Exit();
}

// 상태를 관리하는 머신
public class StateMachine
{
    public IState CurrentState { get; private set; }

    public void Initialize(IState startingState)
    {
        CurrentState = startingState;
        CurrentState.Enter();
    }

    public void ChangeState(IState newState)
    {
        if (CurrentState != null) CurrentState.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Update()
    {
        if (CurrentState != null) CurrentState.Execute();
    }
}

public abstract class NPCState : IState
{
    protected NPC npc;
    protected Animator animator;
    protected Transform player;

    public NPCState(NPC npc, Animator animator, Transform player)
    {
        this.npc = npc;
        this.animator = animator;
        this.player = player;
    }

    public virtual void Enter() { }
    public virtual void Execute() { }
    public virtual void Exit() { }
}