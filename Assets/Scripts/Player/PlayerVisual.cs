using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class PlayerVisual : MonoBehaviour
{
    private SpriteRenderer _rootSpriteRenderer;
    private PlayerController _controller;
    private Animator _rootAnimator; // Visual 자신의 Animator (이벤트가 실제로 걸려있는 대표 애니메이터)
    private Animator[] _partAnimators; // 자식으로 있는 모든 애니메이터를 싹 다 관리

    // 상태 이름 오타 방지용 (에디터의 노드 이름과 정확히 일치해야 함)
    private static class AnimState
    {
        public const string Movement = "Movement";
        public const string JumpUp = "Player_JumpUp";
        public const string JumpTree = "JumpTree";
        public const string Ground = "Player_Ground";
    }

    private float _statSpeedMultiplier = 1f;
    private Coroutine _hitStopRoutine;
    private bool _wasDialogueLocked = false;
    private bool _wasActionLocked = false;

    private void Awake()
    {
        _rootSpriteRenderer = GetComponent<SpriteRenderer>();
        _controller = GetComponentInParent<PlayerController>();
        _rootAnimator = GetComponent<Animator>();
        _partAnimators = GetComponentsInChildren<Animator>(true); // true를 넣으면 비활성화된 파츠(ex: 날개)의 애니메이터도 긁어옴
        Debug.Log($"[PlayerVisual] 총 {_partAnimators.Length}개의 파츠 애니메이터를 동기화합니다.");

        // Controller에서 보내는 '즉시 재생' 이벤트를 구독
        if (_controller != null)
        {
            _controller.OnJumpTriggered += HandleJumpTriggered;
            _controller.OnFallStarted += HandleFallStarted;
            _controller.OnLanded += HandleLanded;
        }
    }

    private void OnDestroy()
    {
        if (_controller != null)
        {
            _controller.OnJumpTriggered -= HandleJumpTriggered;
            _controller.OnFallStarted -= HandleFallStarted;
            _controller.OnLanded -= HandleLanded;
        }
    }

    private void Update()
    {
        if (_controller == null) return;

        HandleLockTransitions();
        UpdateAnimations();
        UpdateSpriteDirection();
        UpdateAnimationSpeed();
    }

    // 대화 시작 순간엔 강제로 Idle, 그리고 (모든 종류의) 잠금이 풀리는 순간엔
    // 그동안 억눌러뒀던 실제 물리 상태(공중/지상)와 화면을 다시 동기화한다.
    private void HandleLockTransitions()
    {
        bool isDialogueLocked = _controller.IsDialogueLocked;
        if (isDialogueLocked && !_wasDialogueLocked)
        {
            PlayImmediate(AnimState.Movement);
        }
        _wasDialogueLocked = isDialogueLocked;

        bool isActionLocked = _controller.IsActionLocked;
        if (!isActionLocked && _wasActionLocked)
        {
            SyncVisualToPhysicalState();
        }
        _wasActionLocked = isActionLocked;
    }

    // 잠금(넉백 등)이 풀리는 순간, 실제로 공중이라면 Fall로 바로잡는다.
    // (공격 종료는 OnAttackEnd에서 ReturnToLocomotion을 직접 호출하므로 여기선 지상 케이스는 건드리지 않음)
    private void SyncVisualToPhysicalState()
    {
        if (!_controller.IsGrounded)
        {
            CrossFadeAll(AnimState.JumpTree, 0.05f);
        }
    }

    // 안전장치: 공중 + 비잠금 상태인데 화면상 상태가 Jump/Fall 계열이 아니면 강제로 바로잡는다.
    // (이벤트 유실 등 어떤 경로로 상태가 꼬이든 최종적으로 항상 여기서 걸러진다)
    private void ReconcileAirborneVisual()
    {
        if (_controller.IsGrounded || _controller.IsActionLocked || _rootAnimator == null) return;

        AnimatorStateInfo info = _rootAnimator.GetCurrentAnimatorStateInfo(0);
        bool isAirborneState = info.IsName(AnimState.JumpUp) || info.IsName(AnimState.JumpTree);
        if (!isAirborneState)
        {
            CrossFadeAll(AnimState.JumpTree, 0.05f);
        }
    }

    private void UpdateAnimations()
    {
        //  Idle 강제는 '대화 중'일 때만. 공격/넉백 중엔 실제 속도를 그대로 반영
        //  (공격 중엔 어차피 PlayAttackAnimation이 별도 상태를 직접 재생해서 Speed 파라미터 자체가 영향 없음)
        bool forceIdle = _controller.IsDialogueLocked;
        float displaySpeed = forceIdle ? 0f : _controller.CurrentSpeed;
        bool displayDash = !forceIdle && _controller.IsDashing && displaySpeed > 0f;

        // 모든 파츠에 동시에 같은 파라미터를 쏨
        foreach (var anim in _partAnimators)
        {
            if (anim == null || !anim.gameObject.activeInHierarchy) continue;

            anim.SetFloat("Speed", displaySpeed);  
            anim.SetBool("IsDash", displayDash);    
            anim.SetBool("IsGrounded", _controller.IsGrounded);
            anim.SetFloat("VelocityY", _controller.VelocityY);
        }
    }

    private void UpdateSpriteDirection()
    {
        // 공격 중이거나 대화 중일 땐 좌우 반전(flip) 잠금
        if (!_controller.CanFlip) return;

        float inputX = Input.GetAxisRaw("Horizontal");
        if (inputX != 0)
        {
            bool flip = inputX > 0; // 기존에 맞게 조정 (왼쪽/오른쪽)
            
            // 모든 파츠의 SpriteRenderer를 찾아서 동시에 뒤집음
            SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in allRenderers)
            {
                sr.flipX = flip;
            }
        }
    }

    // 애니메이션 재생속도 관련
    private void UpdateAnimationSpeed()
    {
        _statSpeedMultiplier = _controller.SpeedMultiplier;

        // 히트스톱 진행 중이 아닐 때만 스탯 배속을 실시간 반영
        if (_hitStopRoutine == null)
            ApplyAnimatorSpeed(_statSpeedMultiplier);
    }


    // --- 점프/착지/낙하: Transition에 맡기지 않고 코드가 직접 상태를 강제 지정 ---

    // 대화/공격/넉백 등으로 잠긴 상태에서는 Jump/Fall/Landed 시각 반응을 무시한다.
    // (물리적 IsGrounded 값 자체는 컨트롤러에서 항상 정확히 갱신되고 있으므로,
    //  잠금이 풀리는 순간 SyncVisualToPhysicalState가 최종 상태를 바로 잡아준다.)
    private void HandleJumpTriggered()
    {
        if (_controller.IsActionLocked) return;
        PlayImmediate(AnimState.JumpUp);
    }
    private void HandleFallStarted()
    {
        if (_controller.IsActionLocked) return;
        CrossFadeAll(AnimState.JumpTree, 0.05f);
    }
    private void HandleLanded()
    {
        if (_controller.IsActionLocked) return;
        PlayImmediate(AnimState.Ground);
    }

    // Player_JumpUp 클립 마지막 프레임의 Animation Event에서 호출 (Relay 경유)
    public void OnJumpApex() => CrossFadeAll(AnimState.JumpTree, 0.05f);

    // Player_Ground 클립 마지막 프레임의 Animation Event에서 호출 (Relay 경유)
    public void OnGroundEnd() => ReturnToMovement();

    // --- PlayerCombat에서 호출 ---
    public void PlayAttackAnimation(string stateName) => PlayImmediate(stateName);
    public void ReturnToMovement() => CrossFadeAll(AnimState.Movement, 0.1f);

    // 잠금 상태(공격 등)에서 벗어날 때 호출: 현재 물리 상태에 맞는 이동 모션으로 복귀
    public void ReturnToLocomotion()
    {
        if (_controller.IsGrounded) CrossFadeAll(AnimState.Movement, 0.1f);
        else CrossFadeAll(AnimState.JumpTree, 0.1f);
    }

    public void TriggerHitStop(float duration)
    {
        if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
        _hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    // 공격이 피격 등으로 캔슬됐을 때, 걸려있던 히트스톱을 즉시 풀고 정상 배속(=현재 스탯 배속)으로 복귀
    public void ResetAnimationSpeed()
    {
        if (_hitStopRoutine != null) { StopCoroutine(_hitStopRoutine); _hitStopRoutine = null; }
        ApplyAnimatorSpeed(_statSpeedMultiplier);
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        ApplyAnimatorSpeed(0f);
        yield return new WaitForSecondsRealtime(duration);
        ApplyAnimatorSpeed(_statSpeedMultiplier); // 무조건 1f가 아니라 '현재 스탯 배속'으로 복귀
        _hitStopRoutine = null;
    }

    private void PlayImmediate(string stateName)
    {
        foreach (var anim in _partAnimators)
            if (anim != null && anim.gameObject.activeInHierarchy)
                anim.Play(stateName, -1, 0f);
    }
    private void CrossFadeAll(string stateName, float duration)
    {
        foreach (var anim in _partAnimators)
            if (anim != null && anim.gameObject.activeInHierarchy)
                anim.CrossFade(stateName, duration);
    }

    private void ApplyAnimatorSpeed(float speed)
    {
        foreach (var anim in _partAnimators)
            if (anim != null) anim.speed = speed;
    }
}