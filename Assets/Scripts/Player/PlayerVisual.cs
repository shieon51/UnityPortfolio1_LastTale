using System.Collections;
using System.Linq;
using UnityEngine;

// 실제 렌더링/애니메이션은 전부 자식 파츠(Body, Face, Hair...)가 담당.
public class PlayerVisual : MonoBehaviour
{
    private IFormStageProvider _formProvider; // 폼체인지 연출 반응 관련

    private SpriteRenderer[] _allSpriteRenderers; // Awake에서 한 번만 캐싱

    private CharacterStats _stats;

    private IPlayerMotor _controller;
    private Animator _driverAnimator; // Body 파츠의 Animator. 이벤트/상태조회의 유일한 기준점.
    private Animator[] _partAnimators; // 자식으로 있는 모든 애니메이터를 싹 다 관리

    // 좌우 방향 별 모션
    private DirectionalPart[] _directionalParts;
    private DirectionalSprite[] _directionalSprites;

    // 눈깜빡임 모션
    private EyeBlinkController _eyeBlink;

    private float _statSpeedMultiplier = 1f;
    private Coroutine _hitStopRoutine;
    private bool _wasDialogueLocked = false;
    private bool _wasActionLocked = false;
    private bool _wasKnockedBack = false;

    private string _lastCommandedState = PlayerAnimStateNames.Movement; 

    private void Awake()
    {
        _controller = GetComponentInParent<IPlayerMotor>();
        _partAnimators = GetComponentsInChildren<Animator>(true); // true를 넣으면 비활성화된 파츠(ex: 날개)의 애니메이터도 긁어옴
        _driverAnimator = ResolveDriverAnimator();
        _allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _directionalParts = GetComponentsInChildren<DirectionalPart>(true);
        _directionalSprites = GetComponentsInChildren<DirectionalSprite>(true);
        _eyeBlink = GetComponentInChildren<EyeBlinkController>(true);

        Debug.Log($"[PlayerVisual] 총 {_partAnimators.Length}개의 파츠 애니메이터를 동기화합니다.");

        // Controller에서 보내는 '즉시 재생' 이벤트를 구독
        if (_controller != null)
        {
            _controller.OnJumpTriggered += HandleJumpTriggered;
            _controller.OnFallStarted += HandleFallStarted;
            //_controller.OnLandingAnticipated += HandleLandingAnticipated; // ★ OnLanded 대신 이걸 구독
            _controller.OnLanded += HandleLanded;
        }

        _formProvider = GetComponentInParent<IFormStageProvider>();

        if (_formProvider != null)
        {
            _formProvider.OnFormTransformStarted += HandleFormTransformStarted;
            _formProvider.OnFormStageChanged += HandleFormStageChanged;
        }

        _stats = GetComponentInParent<CharacterStats>();

        if (_stats != null)
        {
            _stats.OnKnockbackApplied += HandleKnockbackHit;
        }

        if (_stats != null)
        {
            _stats.OnKnockbackApplied += HandleKnockbackHit;
            _stats.OnGroggyStarted += HandleGroggyStarted;
            _stats.OnGroggyEnded += HandleGroggyEnded;
        }
    }

    private void OnDestroy()
    {
        if (_controller != null)
        {
            _controller.OnJumpTriggered -= HandleJumpTriggered;
            _controller.OnFallStarted -= HandleFallStarted;
            //_controller.OnLandingAnticipated -= HandleLandingAnticipated;
            _controller.OnLanded -= HandleLanded; 
        }

        if (_formProvider != null)
        {
            _formProvider.OnFormTransformStarted -= HandleFormTransformStarted;
            _formProvider.OnFormStageChanged -= HandleFormStageChanged;
        }

        if (_stats != null)
        {
            _stats.OnKnockbackApplied -= HandleKnockbackHit;
        }
    }

    private void Update()
    {
        if (_controller == null) return;

        HandleLockTransitions();
        HandleKnockbackTransition(); // ★ 추가
        UpdateAnimations();
        UpdateSpriteDirection();
        UpdateAnimationSpeed();
        ReconcileAirborneVisual();
    }

    private Animator ResolveDriverAnimator()
    {
        var bodyTag = GetComponentsInChildren<BodyPartSlotTag>(true)
            .FirstOrDefault(p => p.slot == BodyPartSlot.Body);

        if (bodyTag != null) return bodyTag.Animator;

        Debug.LogWarning("[PlayerVisual] Body 슬롯 태그를 찾지 못했습니다. 첫 파츠를 기준 Animator로 사용합니다.");
        return _partAnimators.Length > 0 ? _partAnimators[0] : null;
    }

    // 폼체인지(요정화) 모션 관련
    private void HandleFormTransformStarted()
    {
        if (_formProvider == null) { PlayImmediate(PlayerAnimStateNames.Transform); return; }

        bool enteringFlight = !_formProvider.IsFlightForm; // 토글 전 시점이라 반대가 목표 방향
        PlayImmediate(enteringFlight ? PlayerAnimStateNames.Transform : PlayerAnimStateNames.TransformOut);
    }
    private void HandleFormStageChanged(int newStage) => ReturnToLocomotion();

    // 맞을 때마다 무조건 재생 (연속으로 맞아도 매번 다시 틀어짐)
    private void HandleKnockbackHit()
    {
        PlayImmediate(PlayerAnimStateNames.Hit);
    }

    // 대화/공격 잠금과는 별개로, 순수하게 '넉백 시작/종료' 전이만 감지해서 Hit 모션을 넣고 뺀다.
    private void HandleKnockbackTransition()
    {
        bool isKnockedBack = _controller.IsKnockedBack;

        if (isKnockedBack && !_wasKnockedBack)
        {
            PlayImmediate(PlayerAnimStateNames.Hit);
        }
        else if (!isKnockedBack && _wasKnockedBack)
        {
            ReturnToLocomotion();
        }

        _wasKnockedBack = isKnockedBack;
    }

    // 대화 시작 순간엔 강제로 Idle, 그리고 (모든 종류의) 잠금이 풀리는 순간엔
    // 그동안 억눌러뒀던 실제 물리 상태(공중/지상)와 화면을 다시 동기화한다.
    private void HandleLockTransitions()
    {
        bool isDialogueLocked = _controller.IsDialogueLocked;
        if (isDialogueLocked && !_wasDialogueLocked)
        {
            PlayImmediate(PlayerAnimStateNames.Movement);
        }
        _wasDialogueLocked = isDialogueLocked;

        bool isActionLocked = _controller.IsActionLocked;
        if (!isActionLocked && _wasActionLocked)
        {
            ReturnToLocomotion();   //SyncVisualToPhysicalState();
        }
        _wasActionLocked = isActionLocked;
    }

    // 그로기 상태
    private void HandleGroggyStarted() => PlayImmediate(PlayerAnimStateNames.Groggy);
    private void HandleGroggyEnded() => ReturnToLocomotion();


    // 안전장치: 공중 + 비잠금 상태인데 화면상 상태가 Jump/Fall 계열이 아니면 강제로 바로잡는다.
    // (이벤트 유실 등 어떤 경로로 상태가 꼬이든 최종적으로 항상 여기서 걸러진다)
    // -> Animator에 재질문 대신 우리가 기록한 값으로 판단
    private void ReconcileAirborneVisual()
    {
        if (_controller.IsGrounded || _controller.IsActionLocked) return; //|| _driverAnimator == null //?
        if (_formProvider != null && _formProvider.IsFlightForm) return; // 비행형은 이 안전장치 대상이 아님

        bool isAcceptableAirborneState = _lastCommandedState == PlayerAnimStateNames.JumpUp
        || _lastCommandedState == PlayerAnimStateNames.JumpTree
        || _lastCommandedState == PlayerAnimStateNames.Ground; // 착지 예고 중인 Ground 상태도 정상으로 인정

        if (!isAcceptableAirborneState)
        {
            CrossFadeAll(PlayerAnimStateNames.JumpTree, 0.05f);
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
            foreach (var sr in _allSpriteRenderers) // 캐싱된 배열 재사용, 할당 없음
            {
                if (sr != null) sr.flipX = flip;
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

    // 특정 정규화된 재생 시점부터 상태를 재생 (8번: 착지 순간 끊김 없이 지상 클립으로 전환할 때 사용)
    public void PlayAttackAnimation(string stateName, float normalizedTime = 0f)
    {
        _eyeBlink?.SetVisible(false); // ★ 공격 시작 시 눈 숨김 (Face 자체 그림에 이미 포함되어 있으므로)
        PlayImmediate(stateName, normalizedTime);
    }

    public float GetCurrentNormalizedTime()
    {
        if (_driverAnimator == null) return 0f;
        var info = _driverAnimator.GetCurrentAnimatorStateInfo(0);
        return info.normalizedTime % 1f; // 반복 횟수(정수부) 제거, 0~1 소수부만
    }

    public void SetFacingDirection(bool flipX)
    {
        foreach (var sr in _allSpriteRenderers)
            if (sr != null) sr.flipX = flipX;

        bool facingRight = flipX; // 기존 컨벤션: flipX=true → 오른쪽
        foreach (var part in _directionalParts) part.ApplyFacing(facingRight);
        foreach (var s in _directionalSprites) s.ApplyFacing(facingRight);
    }



    // --- 점프/착지/낙하: Transition에 맡기지 않고 코드가 직접 상태를 강제 지정 ---

    // 대화/공격/넉백 등으로 잠긴 상태에서는 Jump/Fall/Landed 시각 반응을 무시한다.
    // (물리적 IsGrounded 값 자체는 컨트롤러에서 항상 정확히 갱신되고 있으므로,
    //  잠금이 풀리는 순간 SyncVisualToPhysicalState가 최종 상태를 바로 잡아준다.)
    private void HandleJumpTriggered()
    {
        if (_controller.IsActionLocked) return;
        if (_formProvider != null && _formProvider.IsFlightForm) return; // 비행형은 별도 점프 연출 없음
        PlayImmediate(PlayerAnimStateNames.JumpUp);
    }
    private void HandleFallStarted()
    {
        if (_controller.IsActionLocked) return;
        if (_formProvider != null && _formProvider.IsFlightForm)
        {
            CrossFadeAll(PlayerAnimStateNames.Movement, 0.1f);
            return;
        }
        CrossFadeAll(PlayerAnimStateNames.JumpTree, 0.05f);
    }
    //private void HandleLanded()
    //{
    //    if (_controller.IsActionLocked) return;
    //    if (_formProvider != null && _formProvider.IsFlightForm)
    //    {
    //        CrossFadeAll(PlayerAnimStateNames.Movement, 0.1f);
    //        return;
    //    }
    //    PlayImmediate(PlayerAnimStateNames.Ground);
    //}

    // 실제 접촉 전, 예고 시점에 착지 모션을 미리 재생
    private void HandleLanded()
    {
        if (_controller.IsActionLocked) return;
        if (_formProvider != null && _formProvider.IsFlightForm) return; // 비행형은 착지 모션 없음
        PlayImmediate(PlayerAnimStateNames.Ground);
    }

    // Player_JumpUp 클립 마지막 프레임의 Animation Event에서 호출 (Relay 경유)
    public void OnJumpApex() => CrossFadeAll(PlayerAnimStateNames.JumpTree, 0.05f);

    // Player_Ground 클립 마지막 프레임의 Animation Event에서 호출 (Relay 경유)
    public void OnGroundEnd() => ReturnToMovement();

    // --- PlayerCombat에서 호출 ---
    public void PlayAttackAnimation(string stateName) => PlayAttackAnimation(stateName, 0f); // ★ 눈 숨김 로직이 항상 같이 실행됨
    public void ReturnToMovement() => CrossFadeAll(PlayerAnimStateNames.Movement, 0.1f);

    // PlayerGuard에서 호출
    public void PlayState(string stateName) => PlayImmediate(stateName);

    // 잠금 상태(공격 등)에서 벗어날 때 호출: 현재 물리 상태에 맞는 이동 모션으로 복귀
    public void ReturnToLocomotion()
    {
        _eyeBlink?.SetVisible(true); // ★ 공격 끝나면 다시 표시

        if (_formProvider != null && _formProvider.IsFlightForm)
        {
            CrossFadeAll(PlayerAnimStateNames.Movement, 0.1f); // 비행형은 항상 Movement(호버/비행 블렌드트리)
            return;
        }

        if (_controller.IsGrounded) CrossFadeAll(PlayerAnimStateNames.Movement, 0.1f);
        else CrossFadeAll(PlayerAnimStateNames.JumpTree, 0.1f);
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

    private void PlayImmediate(string stateName, float normalizedTime = 0f)
    {
        _lastCommandedState = stateName; // 명령을 내릴 때마다 기록
        foreach (var anim in _partAnimators)
            if (anim != null && anim.gameObject.activeInHierarchy)
                anim.Play(stateName, -1, normalizedTime);
    }
    private void CrossFadeAll(string stateName, float duration)
    {
        _lastCommandedState = stateName; // 여기도 동일
        foreach (var anim in _partAnimators)
            if (anim != null && anim.gameObject.activeInHierarchy)
                anim.CrossFade(stateName, duration);
    }

    private void ApplyAnimatorSpeed(float speed)
    {
        foreach (var anim in _partAnimators)
            if (anim != null) anim.speed = speed;
    }

    // 연출용 컷신 재생
    public void PlayCutscene(string bodyStateName, string faceStateName)
    {
        var appearance = GetComponent<CharacterAppearance>();
        foreach (var anim in _partAnimators)
        {
            if (anim == null || !anim.gameObject.activeInHierarchy) continue;
            bool isFace = appearance != null && appearance.GetSlot(anim) == BodyPartSlot.Face;

            if (isFace && !string.IsNullOrEmpty(faceStateName)) anim.Play(faceStateName, -1, 0f);
            else if (!isFace && !string.IsNullOrEmpty(bodyStateName)) anim.Play(bodyStateName, -1, 0f);
        }
    }
}