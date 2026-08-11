using UnityEngine;

// 비행형(요정화 2단계) 전용 이동을 전담하는 컴포넌트.
// IFormStageProvider 이벤트를 구독해 스스로 켜고 끄며, PlayerController의 지상 이동 로직과는 완전히 분리되어 있다.
// 나중에 새 이동 모드(수영 등)가 필요해지면, PlayerController를 건드리지 않고
// 이 클래스를 참고해 같은 패턴(폼/상태 이벤트 구독 → enabled 토글)으로 새 컴포넌트를 나란히 추가하면 된다.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFlightController : MonoBehaviour
{
    [Header("Flight Movement")]
    public float flightMaxSpeed = 8f;
    [Tooltip("목표 속도로 붙는 가속도")]
    public float flightAcceleration = 30f;
    [Tooltip("입력을 떼거나 반대로 줄 때의 감속도. 가속보다 커야 '탁' 멈추는 느낌이 남")]
    public float flightDeceleration = 45f;

    [Header("Dash Burst (12번 항목)")]
    public float dashBurstSpeed = 16f;
    public float dashBurstDuration = 0.15f;
    public float verticalBurstSpeed = 14f;

    private Rigidbody2D _rb;
    private IFormStageProvider _formProvider;
    private PlayerController _playerController;
    private PlayerCombat _playerCombat;

    private bool _isDashBursting = false;
    private float _dashBurstTimer = 0f;
    private Vector2 _dashBurstVelocity;

    public float afterimageTailDuration = 0.2f; // 대시 끝나고 감속하는 동안에도 잔상이 조금 더 남게

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _formProvider = GetComponent<IFormStageProvider>();
        _playerController = GetComponent<PlayerController>();
        _playerCombat = GetComponentInChildren<PlayerCombat>();

        enabled = false; // 기본은 지상형이므로 처음엔 꺼진 채로 시작

        if (_formProvider != null) _formProvider.OnFormStageChanged += HandleFormStageChanged;
    }

    private void OnDestroy()
    {
        if (_formProvider != null) _formProvider.OnFormStageChanged -= HandleFormStageChanged;
    }

    private void HandleFormStageChanged(int newStage)
    {
        bool isFlight = _formProvider.IsFlightForm;
        if (isFlight == enabled) return;

        enabled = isFlight;
        if (isFlight) EnterFlight(); else ExitFlight();
    }

    private void EnterFlight()
    {
        _rb.gravityScale = 0f;

        int playerLayer = gameObject.layer;
        int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
        // 비행 중엔 원웨이 플랫폼을 완전히 무시 (Ground는 절대 건드리지 않으므로 그대로 막힘)
        if (oneWayLayer >= 0) Physics2D.IgnoreLayerCollision(playerLayer, oneWayLayer, true);
    }

    private void ExitFlight()
    {
        int playerLayer = gameObject.layer;
        int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
        if (oneWayLayer >= 0) Physics2D.IgnoreLayerCollision(playerLayer, oneWayLayer, false);

        _isDashBursting = false;
        // gravityScale 복구는 PlayerController.HandleFormStageChanged가 이미 처리 (중복 호출은 무해함)
    }

    private void Update()
    {
        if (_playerController != null && _playerController.IsActionLocked) return; // 공격/변신 등 잠금 중엔 입력 무시
        HandleDashBurstInput();
    }

    private void HandleDashBurstInput()
    {
        if (_isDashBursting) return;

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            float dir = FacingDirectionHint();
            StartDashBurst(new Vector2(dir, 0f) * dashBurstSpeed);
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            bool isDown = Input.GetKey(KeyCode.DownArrow);
            StartDashBurst(new Vector2(0f, isDown ? -verticalBurstSpeed : verticalBurstSpeed));
        }
    }

    private float FacingDirectionHint()
    {
        return _playerCombat != null ? _playerCombat.FacingDirection * -1f : 1f; // Q 스킬과 동일한 스프라이트 방향 보정
    }

    private void StartDashBurst(Vector2 velocity)
    {
        _playerCombat.GetComponent<AfterimageEffect>()?.Play(dashBurstDuration + afterimageTailDuration); // ★ 감속 꼬리까지 커버
        _isDashBursting = true;
        _dashBurstTimer = 0f;
        _dashBurstVelocity = velocity;
    }

    private void FixedUpdate()
    {
        if (_playerController != null && _playerController.IsActionLocked)
        {
            _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, Vector2.zero, flightDeceleration * Time.fixedDeltaTime);
            return;
        }

        if (_isDashBursting)
        {
            _rb.linearVelocity = _dashBurstVelocity;
            _dashBurstTimer += Time.fixedDeltaTime;
            if (_dashBurstTimer >= dashBurstDuration)
            {
                _isDashBursting = false;
            }

            return;
        }

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical"); // 프로젝트 Input Manager의 기본 Up/Down 축 사용

        Vector2 inputDir = new Vector2(inputX, inputY);
        Vector2 targetVelocity = inputDir.sqrMagnitude > 0.01f ? inputDir.normalized * flightMaxSpeed : Vector2.zero;

        float rate = (targetVelocity == Vector2.zero) ? flightDeceleration : flightAcceleration;
        _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
    }
}