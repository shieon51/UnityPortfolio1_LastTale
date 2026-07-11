using System;
using System.Collections;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
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

    // 애니메이션 타이밍 제어
    public event Action OnJumpTriggered; // 점프
    public event Action OnLanded;        // 착지
    public event Action OnFallStarted;   // 점프가 아닌 낙하(아래 지형 이동 등)

    // --- 상태 프로퍼티 (Visual이나 다른 스크립트에서 읽어갈 수 있게 열어둠) ---
    public float CurrentSpeed => _goToUnder ? 0f : (Mathf.Abs(_horizontalInput) > 0 ? (IsDashing ? baseDashSpeed : baseRunSpeed) : 0f); // BT 파라미터를 위해 실제 속도(0, 3, 6)를 반환하도록 계산!
    public float VelocityY => _rb.linearVelocity.y;
    public bool IsGrounded { get; private set; }
    public bool IsDashing { get; private set; }
    public bool CanFlip => !IsActionLocked; // 공격 중일 때 좌우 플립(방향 전환)을 막기 위한 프로퍼티
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
            return false;
        }
    }

    private float _horizontalInput;
    private bool _goToUnder = false; // 아래 지형 이동키 눌렀을 시
    private float _lastTeleportTime = 0f;
    private bool _isDashLatchedInAir = false; // 공중 대시 관성 유지를 위한 변수
    private float _lastJumpTime = 0f;

    private int _groundedMismatchStreak = 0;
    private const int GroundedConfirmFrames = 2; // 연속 몇 프레임 동안 같은 값이 나와야 착지/이탈로 확정할지

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
    }

    private void Update()
    {
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
        // 점프하는 순간 대시 중이었다면 상태를 기억함
        _isDashLatchedInAir = IsDashing;
        _lastJumpTime = Time.time;       // 점프 시간 기록
        _groundedMismatchStreak = 0; // 점프는 디바운스 없이 즉시/확정적으로 반영

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0); // 기존 Y 낙하 관성 무시하고 점프
        _rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        IsGrounded = false;

        // Visual 스크립트에게 '점프 트리거 터트려라'고 알림
        OnJumpTriggered?.Invoke();
        Debug.Log($"[PlayerController] Space 점프 발동! 점프력: {jumpForce}, 대시점프 유지: {_isDashLatchedInAir}");
    }

    private void GoUnderGround()
    {
        if (_oneWayPlatform == null) return;

        Vector2 origin = transform.position + groundCheckOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 1.0f, groundLayer);
        if (hit.collider == null || !_oneWayPlatform.IsOneWayPlatformLayer(hit.collider.gameObject.layer)) return;

        if (_oneWayPlatform.TryPassThrough(hit.collider))
        {
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

    private bool ComputeRawGrounded()
    {
        Vector2 origin = transform.position + groundCheckOffset;
        Vector2 size = new Vector2(groundCheckWidth, groundCheckHeight);
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundLayer);
        if (hit.collider == null) return false;

        // 원웨이 플랫폼을 '위로 관통 중'일 때만 이 히트를 무시 (경사로는 예외 대상 아님)
        if (_rb.linearVelocity.y > 0.01f && _oneWayPlatform != null && _oneWayPlatform.IsOneWayPlatformLayer(hit.collider.gameObject.layer))
            return false;

        return true;
    }

    private void EvaluateGroundedWithDebounce(bool rawGrounded)
    {
        if (rawGrounded == IsGrounded)
        {
            _groundedMismatchStreak = 0;
            return;
        }

        _groundedMismatchStreak++;
        if (_groundedMismatchStreak < GroundedConfirmFrames) return; // 순간적 흔들림일 수 있으므로 이전 상태 유지

        _groundedMismatchStreak = 0;
        ApplyGroundedChange(rawGrounded);
    }

    private void SetGroundedImmediate(bool grounded)
    {
        _groundedMismatchStreak = 0;
        if (grounded == IsGrounded) return;
        ApplyGroundedChange(grounded);
    }

    private void ApplyGroundedChange(bool grounded)
    {
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
