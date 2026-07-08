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
    public float groundCheckHeight = 0.1f; // [추가됨] 바닥 체크용 박스 두께 조절 가능
    public LayerMask groundLayer;
    public Vector3 groundCheckOffset = new Vector3(0, -0.5f, 0);

    // --- 상태 프로퍼티 (Visual이나 다른 스크립트에서 읽어갈 수 있게 열어둠) ---
    public float CurrentSpeed => _goToUnder ? 0f : (Mathf.Abs(_horizontalInput) > 0 ? (IsDashing ? baseDashSpeed : baseRunSpeed) : 0f); // BT 파라미터를 위해 실제 속도(0, 3, 6)를 반환하도록 계산!
    public float VelocityY => _rb.linearVelocity.y;
    public bool IsGrounded { get; private set; }
    public bool IsDashing { get; private set; }
    public bool IsAscending => !IsGrounded && VelocityY > 0;

    private float _horizontalInput;
    private bool _goToUnder = false; // 아래 지형 이동키 눌렀을 시
    private float _lastTeleportTime = 0f;

    private bool _isDashLatchedInAir = false; // 공중 대시 관성 유지를 위한 변수
    private float _lastJumpTime = 0f;

    // --- 컴포넌트 캐싱 ---
    private Rigidbody2D _rb;
    private Collider2D _groundCollider;
    private CharacterStats _stats; // (스탯 시스템 의존성)
    private PlayerCombat _playerCombat;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _groundCollider = GetComponent<Collider2D>();
        _stats = GetComponent<CharacterStats>();
        _playerCombat = GetComponentInChildren<PlayerCombat>();

        // 충돌 감지 방식을 Continuous로 설정
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void Update()
    {
        // 1. 행동 불가 상태면 입력 무시
        if (IsActionLocked()) return;

        // 2. 바닥 판정 (매 프레임)
        CheckGrounded();

        // 3. 입력 감지
        HandleInput();
    }

    private void FixedUpdate()
    {
        // 4. 물리 이동 (FixedUpdate에서 처리하는 것이 정석)
        if (IsActionLocked()) return;
        ApplyMovement();
    }

    // 행동 불가 상태인지 체크 (넉백, 대화중, 공격중)
    private bool IsActionLocked()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsTalking) return true;
        if (_stats != null && _stats.isKnockedBack) return true; // 넉백 체크
        if (_playerCombat != null && _playerCombat.IsAttacking) return true;

        return false;
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

        // 점프
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded)
            Jump();

        // 아래 지형 통과
        if (Input.GetKeyDown(KeyCode.DownArrow) && IsGrounded)
            GoUnderGround();
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

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0); // 기존 Y 낙하 관성 무시하고 점프
        _rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        IsGrounded = false;

        Debug.Log($"[PlayerController] Space 점프 발동! 점프력: {jumpForce}, 대시점프 유지: {_isDashLatchedInAir}");
    }

    private void GoUnderGround()
    {
        _goToUnder = true;
        StartCoroutine(ResetColliderTriggerRoutine());
    }

    private IEnumerator ResetColliderTriggerRoutine()
    {
        // Raycast 대신 기존에 밟고 있던 플랫폼 Effector를 찾아 캐싱
        PlatformEffector2D effector = GetCurrentPlatformEffector();
        if (effector != null && _groundCollider != null)
        {
            Collider2D platformCollider = effector.GetComponent<Collider2D>();
            Physics2D.IgnoreCollision(_groundCollider, platformCollider, true); // 지형 통과
            yield return new WaitForSeconds(0.5f);
            Physics2D.IgnoreCollision(_groundCollider, platformCollider, false);
        }
        _goToUnder = false;
    }

    private void CheckGrounded()
    {
        if (_goToUnder)
        {
            IsGrounded = false;
            return;
        }

        // [버그 수정 완료] 점프 직후 0.1초 동안은 바닥에 닿았다고 착각하지 않게 막아줌! (대시 풀림 방지)
        if (Time.time < _lastJumpTime + 0.1f)
        {
            IsGrounded = false;
            return;
        }

        // Raycast 2개를 쏘는 것보다 BoxCast 하나가 지형 판정에 훨씬 빈틈이 없고 완벽함.
        Vector2 origin = transform.position + groundCheckOffset;
        Vector2 size = new Vector2(groundCheckWidth, groundCheckHeight);

        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundLayer);

        bool wasGrounded = IsGrounded;
        IsGrounded = hit.collider != null;

        // 방금 땅에 닿았다면 공중 대시 관성 리셋
        if (IsGrounded && !wasGrounded)
        {
            _isDashLatchedInAir = false;
            Debug.Log("[PlayerController] 바닥 착지 완료.");
        }
    }

    private PlatformEffector2D GetCurrentPlatformEffector()
    {
        Vector2 origin = transform.position + groundCheckOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 1.0f, groundLayer);
        if (hit.collider != null)
            return hit.collider.GetComponent<PlatformEffector2D>();
        return null;
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
        Gizmos.color = IsGrounded ? Color.red : Color.yellow;
        Vector2 origin = transform.position + groundCheckOffset;
        Vector2 size = new Vector2(groundCheckWidth, groundCheckHeight);
        Gizmos.DrawWireCube(origin + Vector2.down * groundCheckDistance, size);
    }
#endif
    //// 피로도 체크 로직 (이벤트 방식에서 Update 실시간 체크 방식으로 변경하여 안정성 확보)
    //private void CheckFatigueStatus()
    //{
    //    // 현재 조종 중인 캐릭터가 소라(SoraStats)일 때만 피로도 적용
    //    if (PlayerManager.Instance.CurrentCharacter is SoraStats sora)
    //    {
    //        if (sora.currentFatigue >= 30) // 피로도 임계점 도달 시
    //        {
    //            CurRunSpeed = tiredRunSpeed;
    //            CurDashSpeed = tiredRunSpeed * 2;
    //            _animator.speed = 0.5f;
    //        }
    //        else
    //        {
    //            CurRunSpeed = defaultRunSpeed;
    //            CurDashSpeed = defaultDashSpeed;
    //            _animator.speed = 1f;
    //        }
    //    }
    //    else
    //    {
    //        // 소라가 아닌 다른 캐릭터(리엘 등)는 기본 속도로 고정
    //        CurRunSpeed = defaultRunSpeed;
    //        CurDashSpeed = defaultDashSpeed;
    //        _animator.speed = 1f;
    //    }
    //}

    //////피로도 일정 수준 이상 (상태) //
    ////private void HandleFatigueChange(int currentFatigue)
    ////{
    ////    if (currentFatigue >= 30)
    ////    {
    ////        CurRunSpeed = TIRED_RUN_SPEED;
    ////        CurDashSpeed = TIRED_RUN_SPEED * 2; // 피로도 24 이상이면 이동 속도 감소
    ////        _animator.speed = 0.5f;
    ////    } 
    ////    else
    ////    {
    ////        CurRunSpeed = DEFALT_RUN_SPEED;
    ////        CurDashSpeed = DEFALT_DASH_SPEED;
    ////        _animator.speed = 1f;
    ////    }
    ////}



    ////public LayerMask groundLayer;  // Inspector에서 "Ground" 설정
    ////bool IsGrounded()
    ////{
    ////    float rayDistance = 0.2f;
    ////    Vector2 left = (Vector2)transform.position + new Vector2(-0.2f, 0);
    ////    Vector2 center = transform.position;
    ////    Vector2 right = (Vector2)transform.position + new Vector2(0.2f, 0);
    ////
    ////    return Physics2D.Raycast(left, Vector2.down, rayDistance, groundLayer) ||
    ////           Physics2D.Raycast(center, Vector2.down, rayDistance, groundLayer) ||
    ////           Physics2D.Raycast(right, Vector2.down, rayDistance, groundLayer);
    ////}

    //private void Jump()
    //{
    //    // 대화중인 경우에는 움직이지 못하도록
    //    if (DialogueManager.Instance.IsTalking) return;
    //    // 공격 중이라면 점프 못하게
    //    if (_playerAttack._isAttacking) return; 

    //    // 점프 애니메이션
    //    if (Input.GetKeyDown(KeyCode.LeftControl) && _isGrounded)
    //    {
    //        _animator.SetTrigger("Jump");
    //        _animator.SetBool("IsAscending", true);
    //        _animator.SetBool("IsGrounded", false);
    //        //_rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocityX, JumpForce);
    //        _rigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse); // Impulse로 주는 게 점프에 더 적합함
    //        _isAscending = true;
    //        _isGrounded = false;
    //    }
    //}

    //private void GoUnderGround()
    //{
    //    // 대화중인 경우에는 움직이지 못하도록
    //    if (DialogueManager.Instance.IsTalking) return;

    //    // 아래 지형으로 내려가기
    //    if (Input.GetKeyDown(KeyCode.DownArrow) && _isGrounded)
    //    {
    //        _goToUnder = true;

    //        // 잠시 후에 다시 충돌 활성화
    //        StartCoroutine(ResetColliderTrigger());
    //    }
    //}

    //// 잠시 후에 다시 충돌을 활성화하는 코루틴
    //private IEnumerator ResetColliderTrigger()
    //{
    //    // 현재 서 있는 플랫폼 가져오기
    //    PlatformEffector2D effector = GetCurrentPlatformEffector();
    //    if (effector != null)
    //    {
    //        Physics2D.IgnoreCollision(_playerGroundCollider, effector.GetComponent<Collider2D>(), true);
    //        yield return new WaitForSeconds(0.5f);
    //        Physics2D.IgnoreCollision(_playerGroundCollider, effector.GetComponent<Collider2D>(), false);
    //    }
    //    _goToUnder = false;
    //}

    //private void Air()
    //{
    //    //RaycastHit2D hit;
    //    RaycastHit2D leftHit, rightHit;

    //    Vector3 pos = transform.position + _groundCheckLineOffset + Vector3.right * GroundCheckDistanceWidth;
    //    leftHit = Physics2D.Raycast(pos, Vector2.down, GroundCheckDistance, LayerMask.GetMask("Ground"));

    //    pos = transform.position + _groundCheckLineOffset - Vector3.right * GroundCheckDistanceWidth;
    //    rightHit = Physics2D.Raycast(pos, Vector2.down, GroundCheckDistance, LayerMask.GetMask("Ground"));

    //    bool isHit = false;
    //    if (!_goToUnder) // 지형 아래로 내려가는 중이 아닐 때 -> ray 충돌 확인
    //    {
    //        isHit |= leftHit.collider != null;
    //        isHit |= rightHit.collider != null;
    //    }

    //    if (!_isGrounded && isHit) // 착지한 경우
    //    {
    //        _animator.SetBool("IsGrounded", true);
    //    }
    //    if (_isGrounded && !isHit) // 낭떠러지인 경우
    //    {
    //        if (_rigidbody.linearVelocity.y <= 0)
    //            _animator.SetBool("IsAscending", false);
    //    }
    //    if (!_isGrounded) // 땅에 있지 않을 때
    //    {
    //        if (_isAscending && _rigidbody.linearVelocity.y < 0)
    //        {
    //            _animator.SetBool("IsAscending", false);
    //            _isAscending = false;
    //        }
    //    }

    //    _isGrounded = isHit;
    //    _animator.SetBool("IsGrounded", _isGrounded);

    //}

    //private PlatformEffector2D GetCurrentPlatformEffector()
    //{
    //    // 플레이어 아래 위치한 플랫폼 Effector 찾기
    //    RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.2f, LayerMask.GetMask("Ground"));
    //    if (hit.collider != null)
    //    {
    //        return hit.collider.GetComponent<PlatformEffector2D>();
    //    }
    //    return null;
    //}

    //private void Move()
    //{
    //    // 대화중인 경우에는 움직이지 못하도록
    //    if (DialogueManager.Instance.IsTalking) return;

    //    // 1. 넉백(경직) 중이거나 공격 중이면 키보드 입력 완벽 무시!
    //    if (PlayerManager.Instance.CurrentCharacter.isKnockedBack || _playerAttack._isAttacking) return;

    //    // ** 서서히 줄어드는 GetAxis 대신, 바로 0으로 떨어지는 GetAxisRaw를 써야 의도치 않은 브레이크를 막을 수 있음
    //    _horizontalInput = Input.GetAxisRaw("Horizontal");  //GetAxis
    //    _isDash = _isGrounded ? Input.GetKey(KeyCode.LeftShift) : _isDash; //(점프 동작 중에는 마지막 대시 상태를 넘기기)

    //    if (_horizontalInput != 0)
    //    {
    //        _spriteRenderer.flipX = _horizontalInput > 0;
    //    }

    //    // [수정됨] 요정화(Fairy Stage)에 따른 이동 방식(Shift) 처리
    //    HandlePhaseMovement();

    //    if (_isGrounded)
    //    {
    //        //걷기 & 달리기 애니메이션
    //        _animator.SetFloat("Speed", Mathf.Abs(_horizontalInput));
    //        _animator.SetBool("IsDash", _isDash && _horizontalInput != 0);
    //    }

    //    //이동
    //    float moveSpeed = _isDash ? CurDashSpeed : CurRunSpeed;

    //    // 2. 공중 관성 & 에어 컨트롤 로직
    //    if (_isGrounded)
    //    {
    //        // 땅에서는 즉시 움직이고 멈춤
    //        _rigidbody.linearVelocity = new Vector2(_horizontalInput * moveSpeed, _rigidbody.linearVelocity.y);
    //    }
    //    else
    //    {
    //        // 공중에 있을 때
    //        if (_horizontalInput != 0)
    //        {
    //            // 방향키를 누르면 원래 날아가던 관성에 '유저의 이동 의지'를 부드럽게 섞어줌 (Air Strafe)
    //            // (강제로 덮어씌우지 않으므로 넉백 중 키를 눌러도 뚝 떨어지지 않음) ==> 아직 문제 있어 수정중.
    //            float targetSpeed = _horizontalInput * moveSpeed;
    //            float newX = Mathf.MoveTowards(_rigidbody.linearVelocity.x, targetSpeed, 15f * Time.deltaTime);
    //            _rigidbody.linearVelocity = new Vector2(newX, _rigidbody.linearVelocity.y);
    //        }
    //        else
    //        {
    //            // 키보드에서 손을 떼면 아무것도 안 함
    //            // 물리 엔진이 알아서 원래 넉백 힘(포물선) 그대로 날아가게 냅둠.
    //        }
    //    }
    //}

    //// [추가됨] 기획에 맞춰 페이즈 1, 2, 3의 이동기를 분리해둔 함수
    //private void HandlePhaseMovement()
    //{
    //    SoraStats sora = PlayerManager.Instance.CurrentCharacter as SoraStats;
    //    int currentPhase = (sora != null) ? sora.fairyStage : 0;

    //    if (Input.GetKey(KeyCode.LeftShift))
    //    {
    //        switch (currentPhase)
    //        {
    //            case 1: // 2페이즈 (소형 날개) -> 공중에서도 대시/비행 가능
    //                _isDash = true;
    //                // TODO: 필요시 중력을 무시하거나 Y축 이동 로직 추가 (ex. 비행)
    //                break;

    //            case 2: // 3페이즈 (대형 날개) -> 순간이동
    //                if (Input.GetKeyDown(KeyCode.LeftShift) && Time.time >= _lastTeleportTime + _teleportCooldown)
    //                {
    //                    Teleport();
    //                    _lastTeleportTime = Time.time;
    //                }
    //                _isDash = false; // 순간이동은 대시 판정이 아님
    //                break;

    //            default: // 1페이즈 (기본) -> 땅에서만 대시 유지
    //                _isDash = _isGrounded;
    //                break;
    //        }
    //    }
    //    else
    //    {
    //        // Shift 키를 떼면 대시 해제 (점프 중 대시 유지 등은 여기서 조건 추가 가능)
    //        _isDash = false;
    //    }
    //}

    //// [추가됨] 3페이즈 전용 순간이동 로직
    //private void Teleport()
    //{
    //    float teleportDistance = 5f;
    //    float dir = _spriteRenderer.flipX ? 1f : -1f;

    //    // 벽을 뚫지 않도록 Raycast 검사 후 이동
    //    RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.right * dir, teleportDistance, LayerMask.GetMask("Ground"));

    //    Vector3 targetPos = transform.position + new Vector3(dir * teleportDistance, 0, 0);
    //    if (hit.collider != null)
    //    {
    //        targetPos = hit.point - new Vector2(dir * 0.5f, 0); // 벽에 부딪히면 벽 바로 앞까지만
    //    }

    //    transform.position = targetPos;
    //    _animator.SetTrigger("Teleport"); // 순간이동 연출용 트리거
    //    Debug.Log("3페이즈 순간이동 발동!");
    //}


    //private void OnDrawGizmos()
    //{
    //    if (_isGrounded)
    //        Gizmos.color = Color.red;
    //    else
    //        Gizmos.color = Color.yellow;

    //    Vector3 pos = transform.position + _groundCheckLineOffset + Vector3.right * GroundCheckDistanceWidth;
    //    Gizmos.DrawLine(pos, pos + Vector3.down * GroundCheckDistance);
    //    pos = transform.position + _groundCheckLineOffset - Vector3.right * GroundCheckDistanceWidth;
    //    Gizmos.DrawLine(pos, pos + Vector3.down * GroundCheckDistance);
    //}

    
}
