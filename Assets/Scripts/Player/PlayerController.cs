using System;
using System.Collections;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour, IPlayerMotor
{
    [Header("Movement Settings")]
    public float baseRunSpeed = 3f;
    public float baseDashSpeed = 6f;
    public float jumpForce = 15f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.2f;// '미리 착지 감지' 거리
    public float groundCheckWidth = 0.4f; // Raycast 2개 대신 BoxCast가 더 안정적임
    public float groundCheckHeight = 0.1f; // 바닥 체크용 박스 두께 조절 가능
    public LayerMask groundLayer; // Solid Ground + One-Way Platform 모두 포함
    public Vector3 groundCheckOffset = new Vector3(0, -0.5f, 0);

    [Header("Ground Embed Failsafe")]
    [Tooltip("지형 파묻힘을 검사할 주기(초)")]
    public float embedCheckInterval = 0.5f;
    [Tooltip("발밑 기준 이 높이 위에서 아래로 검사 — 씬에서 있을 수 있는 가장 깊은 파묻힘보다 확실히 높아야 함")]
    public float embedCheckHeight = 50f;
    public float embedThreshold = 0.5f; // ★ 오탐 줄이려고 0.3 → 0.5로 여유 늘림

    [Header("Form Change Movement")]
    [Tooltip("변신 중 수평 속도가 감속되는 정도 (초당 감소 속도, 클수록 빨리 멈춤)")]
    public float formTransformDeceleration = 20f; // ex. 달리기 속도 6 기준 0.3초 안에 정지함

    private IFormStageProvider _formProvider;
    private bool _isFormTransforming = false;
    private bool _formTransformTargetIsFlight = false;
    private float _originalGravity;

    // 잠금 소스 자동 수집용
    private IActionLockSource[] _lockSources;

    // 애니메이션 타이밍 제어
    public event Action OnJumpTriggered; // 점프
    public event Action OnLanded;        // 착지
    public event Action OnFallStarted;   // 점프가 아닌 낙하(아래 지형 이동 등)

    // --- 상태 프로퍼티 (Visual이나 다른 스크립트에서 읽어갈 수 있게 열어둠) ---
    public float CurrentSpeed
    {
        get
        {
            if (_isFlying) return _rb.linearVelocity.magnitude; // 비행 중엔 실제 이동 속도를 그대로 반영
            return _goToUnder ? 0f : (Mathf.Abs(_horizontalInput) > 0 ? (IsDashing ? baseDashSpeed : baseRunSpeed) : 0f);
        }
    }
    public float VelocityY => _rb.linearVelocity.y;
    public bool IsGrounded { get; private set; }
    public bool IsDashing { get; private set; }
    public bool CanFlip => !IsActionLocked && (_stats == null || !_stats.isGuarding); // ★ 방어 중엔 방향 고정 // 공격 중일 때 좌우 플립(방향 전환)을 막기 위한 프로퍼티
    public bool IsKnockedBack => _stats != null && _stats.isKnockedBack;
    public bool IsGuarding => _stats != null && _stats.isGuarding; 
    public float EffectiveHorizontalInput => _horizontalInput;
    public bool IsDialogueLocked => DialogueManager.Instance != null && DialogueManager.Instance.IsTalking; // 대화 중일 때는 Idle로 모션 변경
    public float SpeedMultiplier => _stats != null ? _stats.GetSpeedMultiplier() : 1f; // 애니메이션 재생 속도 조절 (피로도 등)


    // 대화/공격/넉백 중 어느 하나라도 걸리면 true (행동 불가 상태)
    public bool IsActionLocked
    {
        get
        {
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsTalking) return true;
            if (_stats != null && _stats.isKnockedBack) return true;
            if (_playerCombat != null && _playerCombat.IsAttacking) return true;
            if (_stats != null && _stats.IsGroggy) return true;
            if (_lockSources != null)
                foreach (var source in _lockSources)
                    if (source.IsLocked) 
                    { 
                        //Debug.Log($"[잠금 원인] {source.GetType().Name}"); 
                        return true; 
                    } // ** 
            return false;
        }
    }

    // PlayerCombat 자신의 IsAttacking으로 인한 순환 잠금을 피하기 위해,
    // '내가 공격 중이라서 잠김'을 제외한 나머지 잠금 소스만 검사합니다.
    public bool IsExternallyLocked
    {
        get
        {
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsTalking) return true;
            if (_stats != null && _stats.isKnockedBack) return true;
            if (_stats != null && _stats.IsGroggy) return true;
            if (_lockSources != null)
                foreach (var source in _lockSources)
                    if (source.IsLocked) return true;
            return false;
        }
    }

    private float _horizontalInput;
    private bool _goToUnder = false; // 아래 지형 이동키 눌렀을 시
    private float _lastTeleportTime = 0f;
    private bool _isDashLatchedInAir = false; // 공중 대시 관성 유지를 위한 변수
    private float _lastJumpTime = 0f;

    // 시간, 프레임 수 체크 - 같은 값이 나와야 착지/이탈로 확정할지
    private float _groundedMismatchStartTime = -1f;
    private int _groundedMismatchFrameCount = 0;
    private const float GroundedConfirmDuration = 0.05f;
    private const int GroundedConfirmMinFrames = 2;

    // 비행 여부 체크
    private bool _isFlying = false;

    // 땅 착지 시 예외 관련 (모션) //?
    private float _lastConfirmedGroundedTime = -10f;
    private const float GroundedCoyoteGrace = 0.1f;

    // 지형 파묻힘 예외처리 관련
    private Vector2 _lastSafeGroundedPosition;
    private float _embedCheckTimer = 0f;
    private LayerMask _solidGroundOnlyLayer; // groundLayer에서 OneWayPlatform만 뺀 것

    // --- 컴포넌트 캐싱 ---
    private Rigidbody2D _rb;
    private Collider2D _groundCollider;
    private CharacterStats _stats; // (스탯 시스템 의존성)
    private PlayerCombat _playerCombat;
    private OneWayPlatformController _oneWayPlatform;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _groundCollider = GetComponent<Collider2D>();
        _stats = GetComponent<CharacterStats>();
        _playerCombat = GetComponentInChildren<PlayerCombat>();
        _oneWayPlatform = GetComponent<OneWayPlatformController>();

        // 충돌 감지 방식을 Continuous로 설정
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        _lockSources = GetComponents<IActionLockSource>(); // SoraStats(변신 중) 등을 자동으로 주워담음

        _formProvider = GetComponent<IFormStageProvider>(); // 없으면 null (정상, 폼체인지 없는 캐릭터)

        if (_formProvider != null)
        {
            _formProvider.OnFormTransformStarted += HandleFormTransformStarted;
            _formProvider.OnFormStageChanged += HandleFormStageChanged;
            _formProvider.OnFormStageChanged += HandleFlightStateChanged;
        }

        _solidGroundOnlyLayer = groundLayer;
        if (_oneWayPlatform != null) _solidGroundOnlyLayer &= ~_oneWayPlatform.oneWayPlatformLayer;
    }

    private void Start()
    {
        _originalGravity = _rb.gravityScale;
        _lastSafeGroundedPosition = transform.position;
    }

    private void OnDestroy()
    {
        if (_formProvider != null)
        {
            _formProvider.OnFormTransformStarted -= HandleFormTransformStarted;
            _formProvider.OnFormStageChanged -= HandleFormStageChanged;
            _formProvider.OnFormStageChanged -= HandleFlightStateChanged;
        }
    }

    private void Update()
    {
        // 잠금 여부와 무관하게 항상 체크
        CheckGroundEmbedFailsafe();

        // 비행 중인 경우
        if (_isFlying)
        {
            _horizontalInput = 0f;
            return; // 지상 이동/점프/착지 판정 자체를 아예 돌리지 않음
        }

        // 1. 바닥 판정 (매 프레임)
        // 바닥 여부는 '물리적 사실'이므로 잠금 여부와 무관하게 항상 정확히 추적한다.
        // (대화/공격/넉백 중에도 실제로는 계속 낙하하다 착지할 수 있기 때문)
        CheckGrounded();

        // 2. 행동 불가 상태면 입력 무시
        if (IsActionLocked)
        {
            _horizontalInput = 0f; // 잠금 중엔 입력을 0으로 고정
            return;
        }

        // 3. 입력 감지
        HandleInput();
    }

    private void FixedUpdate()
    {
        if (_isFlying) return; // 비행 물리는 PlayerFlightController가 전담

        // 요정화 변신 중이면 일반 잠금 처리(속도 즉시 0)보다 먼저 부드러운 감속 처리로 분기
        if (_isFormTransforming)
        {
            ApplyFormTransformMovement();
            return;
        }

        // 4. 물리 이동 (FixedUpdate에서 처리하는 것이 정석)
        // - 대화 중이거나 락이 걸렸을 때 미끄러지지 않고 멈추도록 속도를 0으로 강제
        if (IsActionLocked)
        {
            if (_stats != null && !_stats.isKnockedBack && (_playerCombat == null || !_playerCombat.IsAttacking))
            {
                _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            }
            return;
        }
        ApplyMovement();
    }

    private void HandleInput()
    {
        _horizontalInput = Input.GetAxisRaw("Horizontal");

        if (_stats != null && _stats.isGuarding)
        {
            _horizontalInput = 0f;
            return; // ★ 방어 중엔 점프/대시/아래방향키 전부 무시, 여기서 끝
        }

        // 요정화 페이즈에 따른 Shift 이동기 분기
        SoraStats sora = _stats as SoraStats;
        int currentPhase = (sora != null) ? sora.fairyStage : 0;

        // 대시
        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (currentPhase == 2 && Input.GetKeyDown(KeyCode.LeftShift) && Time.time >= _lastTeleportTime + 1f)
            {
                Teleport(); // 3페이즈 순간이동
                _lastTeleportTime = Time.time;
                IsDashing = false;
            }
            else if (currentPhase == 1)
            {
                IsDashing = true; // 2페이즈 비행 대시
            }
            else
            {
                IsDashing = IsGrounded; // 1페이즈 기본 대시 (땅에서만)
            }
        }
        else
        {
            IsDashing = false; 
        }

        // Space 누르면 점프 이벤트(OnJumped) 발송
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded)
        {
            Jump();
        }

        // 아래+스페이스바 조합으로 통과하거나, 원본대로 DownArrow로 통과 (이벤트 발송)
        if (Input.GetKeyDown(KeyCode.DownArrow) && IsGrounded)
        {
            GoUnderGround();
        }
    }

    private void ApplyMovement()
    {
        // 속도 계산: 스탯 스크립트에서 디버프(피로도) 배율을 가져와 곱함 (OCP 준수)
        float speedMod = (_stats != null) ? _stats.GetSpeedMultiplier() : 1f;

        // 공중에 있을 때는 점프 뛸 당시의 대시 상태(_isDashLatchedInAir)를 반영하여 속도 유지
        bool effectiveDash = IsGrounded ? IsDashing : _isDashLatchedInAir;
        float targetSpeed = (effectiveDash ? baseDashSpeed : baseRunSpeed) * speedMod;

        if (IsGrounded)
        {
            // 지상 이동
            _rb.linearVelocity = new Vector2(_horizontalInput * targetSpeed, _rb.linearVelocity.y);
        }
        else
        {
            // 공중 이동 (Air Control)
            if (_horizontalInput != 0)
            {
                float newX = Mathf.MoveTowards(_rb.linearVelocity.x, _horizontalInput * targetSpeed, 15f * Time.fixedDeltaTime);
                _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
            }
        }
    }

    private void Jump()
    {
        //Debug.Log($"[DBG {Time.time:F3}] Jump() 호출");

        // 점프하는 순간 대시 중이었다면 상태를 기억함
        _isDashLatchedInAir = IsDashing;
        _lastJumpTime = Time.time;       // 점프 시간 기록
        //_groundedMismatchStreak = 0; // 점프는 디바운스 없이 즉시/확정적으로 반영

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0); // 기존 Y 낙하 관성 무시하고 점프
        _rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        IsGrounded = false;

        // Visual 스크립트에게 '점프 트리거 터트려라'고 알림
        OnJumpTriggered?.Invoke();
        //Debug.Log($"[PlayerController] Space 점프 발동! 점프력: {jumpForce}, 대시점프 유지: {_isDashLatchedInAir}");
    }

    private void GoUnderGround()
    {
        if (_oneWayPlatform == null) return;

        Vector2 origin = transform.position + groundCheckOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 1.0f, groundLayer);
        if (hit.collider == null || !_oneWayPlatform.IsOneWayPlatformLayer(hit.collider.gameObject.layer)) return;

        if (_oneWayPlatform.TryPassThrough(hit.collider))
        {
            Debug.Log($"[DBG {Time.time:F3}] OneWayPlatform 통과 시작");

            _goToUnder = true;
            StartCoroutine(ResetGoToUnderFlagRoutine(_oneWayPlatform.passThroughDuration));
        }

        //PlatformEffector2D effector = GetCurrentPlatformEffector();
        //if (effector == null) return; // 애초에 원웨이 플랫폼이 아니면 통과 불가

        //// 원웨이 플랫폼 전용 레이어에 속한 지형인지 확인 (메인 바닥은 여기 안 걸림)
        //if ((oneWayPlatformLayer.value & (1 << effector.gameObject.layer)) == 0) return;

        //_goToUnder = true;
        //StartCoroutine(ResetColliderTriggerRoutine(effector));
    }

    private IEnumerator ResetGoToUnderFlagRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        _goToUnder = false;
    }


    //private IEnumerator ResetColliderTriggerRoutine(PlatformEffector2D effector)
    //{
    //    if (effector != null && _groundCollider != null)
    //    {
    //        Collider2D platformCollider = effector.GetComponent<Collider2D>();
    //        Physics2D.IgnoreCollision(_groundCollider, platformCollider, true); // 지형 통과
    //        yield return new WaitForSeconds(0.5f);
    //        Physics2D.IgnoreCollision(_groundCollider, platformCollider, false);
    //    }
    //    _goToUnder = false;
    //}

    private void CheckGrounded()
    {
        //bool wasGroundedPrev = IsGrounded; // 이번 판정 '이전' 프레임 상태를 먼저 저장

        if (_goToUnder)
        {
            //IsGrounded = false;
            //if (wasGroundedPrev) OnFallStarted?.Invoke(); // 지형 통과 → JumpUp 없이 바로 Fall

            SetGroundedImmediate(false);
            return;
        }

        // 점프 직후 0.1초 동안은 바닥에 닿았다고 착각하지 않게 막아줌 (대시 풀림 방지)
        if (Time.time < _lastJumpTime + 0.1f)
        {
            //IsGrounded = false;

            SetGroundedImmediate(false);
            return; // 점프 직후 grace period. 이 낙하는 이미 OnJumpTriggered가 처리했으므로 재발행 안 함
        }

        bool rawGrounded = ComputeRawGrounded();
        EvaluateGroundedWithDebounce(rawGrounded);

        //Vector2 origin = transform.position + groundCheckOffset;
        //Vector2 size = new Vector2(groundCheckWidth, groundCheckHeight);
        //RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundLayer);

        //bool grounded = hit.collider != null;

        //// 원웨이 플랫폼을 '위로 관통 중'일 때만 이 히트를 무시한다.
        //// 경사로(Solid Ground)는 오를 때도 Y속도가 양수가 되므로 이 예외에서 반드시 제외해야 한다.
        //if (grounded && _rb.linearVelocity.y > 0.01f && IsOneWayPlatformLayer(hit.collider.gameObject.layer))
        //{
        //    grounded = false;
        //}

        //IsGrounded = grounded;

        //// 방금 땅에 닿았다면 공중 대시 관성 리셋
        //if (IsGrounded && !wasGroundedPrev)
        //{
        //    _isDashLatchedInAir = false;
        //    Debug.Log("[PlayerController] 바닥 착지 완료.");
        //    OnLanded?.Invoke();
        //}
        //else if (!IsGrounded && wasGroundedPrev)
        //{
        //    // 점프 호출 없이 자연스럽게 공중으로 진입한 경우 (낭떠러지 등)
        //    OnFallStarted?.Invoke();
        //}
    }

    // 예기치 못한 이유로 지형 아래로 파묻혔을 때 위로 올려주는 함수
    private void CheckGroundEmbedFailsafe()
    {
        bool consideredSafe = _isFlying ? !IsEmbeddedInGround() : (IsGrounded && !IsEmbeddedInGround());
        if (consideredSafe) _lastSafeGroundedPosition = transform.position;

        _embedCheckTimer += Time.deltaTime;
        if (_embedCheckTimer < embedCheckInterval) return;
        _embedCheckTimer = 0f;

        TryRecoverFromEmbed();
    }

    private void TryRecoverFromEmbed()
    {
        Vector2 feetPos = (Vector2)transform.position + (Vector2)groundCheckOffset;
        if (!TryFindGroundAbove(out Vector2 groundPoint)) return;
        if (groundPoint.y <= feetPos.y + embedThreshold) return;

        Debug.LogWarning("[PlayerController] 지형 파묻힘 감지 — 현재 위치 바로 위로 복구합니다.");
        float pivotOffsetFromFeet = transform.position.y - feetPos.y;
        transform.position = new Vector3(transform.position.x, groundPoint.y + pivotOffsetFromFeet + 0.05f, transform.position.z);
        _rb.linearVelocity = Vector2.zero;
    }

    private bool TryFindGroundAbove(out Vector2 groundPoint)
    {
        Vector2 feetPos = (Vector2)transform.position + (Vector2)groundCheckOffset;
        Vector2 rayStart = new Vector2(feetPos.x, feetPos.y + embedCheckHeight);
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, embedCheckHeight * 2f, _solidGroundOnlyLayer);
        groundPoint = hit.point;
        return hit.collider != null;
    }

    private bool IsEmbeddedInGround()
    {
        Vector2 feetPos = (Vector2)transform.position + (Vector2)groundCheckOffset;
        if (!TryFindGroundAbove(out Vector2 groundPoint)) return false;
        return groundPoint.y > feetPos.y + embedThreshold; // ★ 공용 threshold
    }

    private bool ComputeRawGrounded()
    {
        Vector2 origin = transform.position + groundCheckOffset;
        Vector2 size = new Vector2(groundCheckWidth, groundCheckHeight);
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundLayer);

        //if (hit.collider == null) return false;

        //// 원웨이 플랫폼을 '위로 관통 중'일 때만 이 히트를 무시 (경사로는 예외 대상 아님)
        //if (_rb.linearVelocity.y > 0.01f && _oneWayPlatform != null && _oneWayPlatform.IsOneWayPlatformLayer(hit.collider.gameObject.layer))
        //    return false;

        //return true;

        //?
        if (hit.collider != null)
        {
            bool isPassingThroughOneWay = _rb.linearVelocity.y > 0.01f
                && _oneWayPlatform != null
                && _oneWayPlatform.IsOneWayPlatformLayer(hit.collider.gameObject.layer);

            if (!isPassingThroughOneWay)
            {
                _lastConfirmedGroundedTime = Time.time;
                return true;
            }
        }

        // 실제 접촉이 안 잡혀도, 방금까지 확실히 땅이었고 거의 정지해 있다면
        // (미세한 파묻힘/지터로 인한 순간적 미검출 가능성) 짧게 착지 상태를 유지시켜 Fall에 갇히는 걸 방지
        bool wasRecentlyGrounded = Time.time - _lastConfirmedGroundedTime < GroundedCoyoteGrace;
        bool nearlyStill = Mathf.Abs(_rb.linearVelocity.y) < 0.5f;
        return wasRecentlyGrounded && nearlyStill;
    }

    private void EvaluateGroundedWithDebounce(bool rawGrounded)
    {
        if (rawGrounded == IsGrounded)
        {
            _groundedMismatchStartTime = -1f;
            _groundedMismatchFrameCount = 0;
            return;
        }

        if (_groundedMismatchStartTime < 0f)
        {
            _groundedMismatchStartTime = Time.time; // 불일치가 시작된 시점 기록
            _groundedMismatchFrameCount = 1;
            return;
        }

        _groundedMismatchFrameCount++;

        bool durationPassed = Time.time - _groundedMismatchStartTime >= GroundedConfirmDuration;
        bool framesPassed = _groundedMismatchFrameCount >= GroundedConfirmMinFrames;

        if (!durationPassed || !framesPassed) return; // 렉 중 단발성 오판 방지: 시간·프레임 둘 다 필요

        _groundedMismatchStartTime = -1f;
        _groundedMismatchFrameCount = 0;
        ApplyGroundedChange(rawGrounded);
    }

    private void SetGroundedImmediate(bool grounded)
    {
        _groundedMismatchStartTime = -1f;
        _groundedMismatchFrameCount = 0; // 추가
        if (grounded == IsGrounded) return;
        ApplyGroundedChange(grounded);
    }

    private void ApplyGroundedChange(bool grounded)
    {
        //Debug.Log($"[DBG {Time.time:F3}] IsGrounded → {grounded}");

        bool wasGroundedPrev = IsGrounded;
        IsGrounded = grounded;

        if (IsGrounded && !wasGroundedPrev)
        {
            _isDashLatchedInAir = false;
            OnLanded?.Invoke();
        }
        else if (!IsGrounded && wasGroundedPrev)
        {
            OnFallStarted?.Invoke();
        }
    }

    //private bool IsOneWayPlatformLayer(int layer)
    //{
    //    return (oneWayPlatformLayer.value & (1 << layer)) != 0;
    //}

    //private PlatformEffector2D GetCurrentPlatformEffector()
    //{
    //    Vector2 origin = transform.position + groundCheckOffset;
    //    RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 1.0f, groundLayer);
    //    if (hit.collider != null)
    //        return hit.collider.GetComponent<PlatformEffector2D>();
    //    return null;
    //}

    // 땅 파묻힘 감지 (W 스킬 관련) //?
    public void CheckGroundEmbedImmediate() => TryRecoverFromEmbed();

    // 변신을 시작하는 순간(날개가 나타나기 시작하는 시점) 호출됨.
    // 목표 폼이 비행형인지 미리 계산해둬야, 낙하 속도를 감속시킬지 말지 판단 가능
    // (이 시점엔 아직 fairyStage가 토글되기 전이라 IsFlightForm이 '변신 전' 값을 그대로 반영함).
    private void HandleFormTransformStarted()
    {
        if (_formProvider == null) return;
        _isFormTransforming = true;
        _formTransformTargetIsFlight = !_formProvider.IsFlightForm;
    }

    // 변신이 완료된 순간(날개가 다 펴지거나 다 접힌 시점) 호출됨.
    private void HandleFormStageChanged(int newStage)
    {
        _isFormTransforming = false;

        if (_formProvider == null) return;

        if (_formProvider.IsFlightForm)
        {
            // 비행형 완성: 완전한 공중부양 상태로 스냅 (중력 없음, 속도 없음)
            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;
        }
        else
        {
            // 지상형 복귀: 중력을 되돌려 자연스럽게 다시 낙하가 재개되도록 함
            _rb.gravityScale = _originalGravity;
        }
    }

    private void HandleFlightStateChanged(int newStage)
    {
        _isFlying = _formProvider.IsFlightForm;
        if (_isFlying)
        {
            SetGroundedImmediate(false);
            IsDashing = false; // 비행 진입 시 지상 대시 상태 흔적 제거
        }
    }

    // 변신 중(약 0.5초) 매 물리 프레임 호출됨.
    // 수평 속도는 방향에 상관없이 항상 부드럽게 0으로 감속.
    // 수직 속도는 '비행형으로 들어가는 중'일 때만 감속시켜 낙하가 서서히 멈추는 것처럼 보이게 함.
    // (반대로 지상형으로 돌아가는 중엔 건드리지 않음 — 이미 공중부양 중이라 속도 0으로 안정적)
    private void ApplyFormTransformMovement()
    {
        float newX = Mathf.MoveTowards(_rb.linearVelocity.x, 0f, formTransformDeceleration * Time.fixedDeltaTime);
        float newY = _rb.linearVelocity.y;

        if (_formTransformTargetIsFlight)
        {
            newY = Mathf.MoveTowards(_rb.linearVelocity.y, 0f, formTransformDeceleration * Time.fixedDeltaTime);
        }

        _rb.linearVelocity = new Vector2(newX, newY);
    }

    private void Teleport()
    {
        float teleportDist = 5f;
        float dir = _horizontalInput != 0 ? Mathf.Sign(_horizontalInput) : 1f; // 바라보는 방향
        Vector3 targetPos = transform.position + new Vector3(dir * teleportDist, 0, 0);

        // 벽 통과 방지
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.right * dir, teleportDist, groundLayer);
        if (hit.collider != null) targetPos = hit.point - new Vector2(dir * 0.5f, 0);

        transform.position = targetPos;
        Debug.Log("[PlayerController] 3페이즈 순간이동 발동!");
        // 비주얼 쪽에서 텔레포트 연출 호출 필요
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? Color.red : Color.green;
        Vector2 origin = transform.position + groundCheckOffset;
        Vector2 size = new Vector2(groundCheckWidth, groundCheckHeight);
        Gizmos.DrawWireCube(origin + Vector2.down * groundCheckDistance, size);
    }
#endif
}
