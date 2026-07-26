using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Collections;
using System;

// 슬롯에 낀 스킬을 런타임에 교체 가능하게
public enum SkillSlot { Q, W, E, R }

[DefaultExecutionOrder(-10)] // PlayerController보다 항상 먼저 실행되어, 동시입력 시 공격이 우선권을 갖도록 보장
public class PlayerCombat : MonoBehaviour
{
    private IPlayerMotor _motor;         // 5번에서 정의할 인터페이스 (지상/공중 판정용)
    private IFormStageProvider _formProvider;

    private Rigidbody2D _rb;
    private CharacterStats _stats;
    private SoraStats _soraStats;
    private SpriteRenderer _spriteRenderer;
    private PlayerVisual _playerVisual; // 애니메이션 강제 동기화용

    public bool IsAttacking { get; private set; }
    public PlayerMovementContext CurrentSkillContext { get; private set; } // 히트박스 오버라이드/착지 전환에 사용
    private float _originalGravity;

    [Header("Skill Sequence Slots")] // 콤보 리스트가 담긴 Sequence 꾸러미를 받음
    public SkillSequenceData sequenceQ;
    public SkillSequenceData sequenceW;
    public SkillSequenceData sequenceE;
    public SkillSequenceData sequenceR;

    // --- 콤보 시스템 ---
    private Queue<SkillSequenceData> _inputBuffer = new Queue<SkillSequenceData>();
    private SkillBase _currentPlayingSkill;
    // 슬롯(Q,W,E,R)마다 현재 몇 타째인지 독립적으로 기억하는 딕셔너리
    private Dictionary<SkillSequenceData, int> _comboStepTracker = new Dictionary<SkillSequenceData, int>();

    private float _lastAttackTime;
    private bool _isComboWindowOpen = false;  // 콤보 허용 창. 이게 true일 때 키를 누르면 이전 모션을 씹고 즉시 다음 모션이 나감
    private int _attackSessionId = 0; // 안전장치(watchdog)가 최신 공격인지 판별하기 위한 세션 ID

    // 디버그용 (스킬 클래스에서 호출받음)
    private bool _showHitbox = false;
    private Vector2 _lastHitboxCenter;
    private Vector2 _lastHitboxSize;

    // ** 스킬 클래스(MeleeDashSkill 등)가 플레이어가 바라보는 방향을 쉽게 알 수 있도록 열어주는 프로퍼티
    public float FacingDirection => (_spriteRenderer != null && _spriteRenderer.flipX) ? -1f : 1f;

    public event Action<SkillBase> OnSkillBlockedByMana; // UI 피드백(마나 부족 이펙트 등)용 훅

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _stats = GetComponent<CharacterStats>();
        _soraStats = GetComponent<SoraStats>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _playerVisual = GetComponentInChildren<PlayerVisual>();
        _motor = GetComponent<IPlayerMotor>();
        _formProvider = GetComponent<IFormStageProvider>(); // 없으면 null (정상)

        if (_motor != null) _motor.OnLanded += HandleLandedDuringAttack; // ★ 8번: 공중 공격 중 착지 시 지상 모션으로 자연스럽게 전환
    }

    private void OnDestroy()
    {
        if (_motor != null) _motor.OnLanded -= HandleLandedDuringAttack;
    }

    private void Start()
    {
        // 게임 시작 시 캐릭터의 기본 중력값을 기억해둠
        if (_rb != null) _originalGravity = _rb.gravityScale;
    }

    private void Update()
    {
        if (_stats.isKnockedBack || DialogueManager.Instance.IsTalking) return;

        // 키 입력 
        if (Input.GetKeyDown(KeyCode.Q)) HandleInput(sequenceQ);
        else if (Input.GetKeyDown(KeyCode.W)) HandleInput(sequenceW);
        else if (Input.GetKeyDown(KeyCode.E)) HandleInput(sequenceE);
        else if (Input.GetKeyDown(KeyCode.R)) HandleInput(sequenceR);

        // 콤보 윈도우가 열려 있거나, 아예 공격 중이 아닐 때 버퍼를 실행
        if ((!IsAttacking || _isComboWindowOpen) && _inputBuffer.Count > 0)
        {
            TryExecuteSequence(_inputBuffer.Dequeue());
        }
    }

    private void HandleInput(SkillSequenceData seqInput)
    {
        if (seqInput == null || seqInput.comboSteps.Count == 0) return;

        if (!IsAttacking)
        {
            // 공격 중이 아닐 때 들어온 입력 = '이어치기'가 아니라 '새 콤보의 시작'.
            // (콤보 윈도우를 놓치고 애니메이션이 이미 끝난 뒤 눌러도 여기로 들어옴)
            // → 이 슬롯의 진행 단계를 반드시 1타로 리셋한 뒤 버퍼링한다.
            _comboStepTracker[seqInput] = 0;
            BufferInput(seqInput);
        }
        else if (_currentPlayingSkill != null)
        {
            // 궁극기 같은 캔슬기면 버퍼를 비우고 즉시 최우선 예약
            if (seqInput.comboSteps[0].priority == SkillPriority.Cancel || seqInput.comboSteps[0].priority == SkillPriority.Ultimate)
            {
                _inputBuffer.Clear();
                BufferInput(seqInput);
            }
            else
            {
                // 공격이 재생되는 도중에 들어온 정당한 선입력(pre-input).
                // 트래커는 건드리지 않고 그대로 버퍼링 → 콤보 윈도우가 열리는 순간 이어서 재생됨.
                BufferInput(seqInput);
            }
        }
    }

    private void BufferInput(SkillSequenceData seq)
    {
        if (_inputBuffer.Count < 2) _inputBuffer.Enqueue(seq);
    }

    private void TryExecuteSequence(SkillSequenceData seq)
    {
        // 1. 현재 이 슬롯(예: Q)이 몇 타째인지 가져옴 (없으면 0타)
        int step = _comboStepTracker.GetValueOrDefault(seq, 0);
        if (step >= seq.comboSteps.Count) step = 0; // 콤보가 끝났으면 0타로 리셋

        SkillBase skillToPlay = seq.comboSteps[step];

        // 2. 발동 조건 실패(예: W인데 타겟 없음) → 마나/콤보/애니메이션 전혀 건드리지 않고 알리미만 띄움
        if (!skillToPlay.CanExecute(this, out string failReason))
        {
            if (!string.IsNullOrEmpty(failReason))
                NotificationManager.Instance?.Show(failReason, NotificationType.Warning);
            return;
        }

        // 3. --- 마나/오버캐스트 연산 ---
        int actualManaCost = _stats.CalculateManaCost(skillToPlay.requiredMana);
        bool hasEnoughMana = _stats.currentMana >= actualManaCost;

        // 마나가 부족한데 '차단' 정책이면 콤보 진행도, 애니메이션, 아무것도 건드리지 않고 그냥 무시
        if (!hasEnoughMana && skillToPlay.manaCostPolicy == ManaCostPolicy.BlockIfInsufficient)
        {
            OnSkillBlockedByMana?.Invoke(skillToPlay);
            NotificationManager.Instance?.Show("마나가 부족합니다", NotificationType.Warning);
            return;
        }

        // ---------------------------------------------------------------------
        _isComboWindowOpen = false; // 새로운 스킬이 시작됐으니 콤보 창 닫음

        if (hasEnoughMana)
        {
            _stats.UseMana(actualManaCost);
        }
        else // OvercastWithHealth 정책이면서 실제로 마나가 부족한 경우에만 도달
        {
            int deficit = actualManaCost - _stats.currentMana;
            _stats.UseMana(_stats.currentMana);
            if (_soraStats != null) _soraStats.IncreaseFatigue(deficit * 2);
            _stats.TakeDamage(deficit, ElementType.Normal);
        }

        // --- 실행 ---
        IsAttacking = true;
        _stats.isSuperArmor = true;
        _currentPlayingSkill = skillToPlay;
        _lastAttackTime = Time.time;

        // 다음 콤보 스텝 미리 증가시켜두기
        _comboStepTracker[seq] = step + 1;

        var context = ResolveMovementContext();
        CurrentSkillContext = context;

        // 비주얼 파츠 동시 재생
        if (_playerVisual != null)
            _playerVisual.PlayAttackAnimation(skillToPlay.ResolveAnimStateName(context));

        _attackSessionId++;
        int sessionId = _attackSessionId;
        StartCoroutine(skillToPlay.ExecuteSkillBehavior(this, _rb, null, _stats));
        StartCoroutine(AttackWatchdogRoutine(sessionId, skillToPlay));
    }

    // 현재 땅인지, 공중인지, 비행인지에 따라 스킬 모션 결정
    private PlayerMovementContext ResolveMovementContext()
    {
        if (_formProvider != null && _formProvider.IsFlightForm) return PlayerMovementContext.Flying;
        if (_motor != null && _motor.IsGrounded) return PlayerMovementContext.Grounded;
        return PlayerMovementContext.Airborne;
    }

    //  공중 컨텍스트로 공격을 시작했는데, 그 도중에 실제로 착지했다면
    //  같은 재생 시점(normalizedTime)을 유지한 채 지상 컨텍스트 클립으로 자연스럽게 바꿔치기.
    //  (지상/공중 클립의 프레임 타이밍을 맞춰두셨기 때문에 끊김 없이 전환됩니다)
    private void HandleLandedDuringAttack()
    {
        if (!IsAttacking || _currentPlayingSkill == null) return;
        if (CurrentSkillContext == PlayerMovementContext.Grounded) return;

        string groundedState = _currentPlayingSkill.ResolveAnimStateName(PlayerMovementContext.Grounded);
        float normalizedTime = _playerVisual != null ? _playerVisual.GetCurrentNormalizedTime() : 0f;
        _playerVisual?.PlayAttackAnimation(groundedState, normalizedTime);
        CurrentSkillContext = PlayerMovementContext.Grounded;
    }

    // 순간이동형 스킬들이 공용으로 쓸 수 있는 안전 착지 헬퍼
    public Vector2 ResolveSafeGroundedPosition(Vector2 desiredPos, LayerMask groundLayer, float maxSnapDistance = 3f)
    {
        if (groundLayer.value == 0)
        {
            Debug.LogWarning("[PlayerCombat] ResolveSafeGroundedPosition: groundLayer가 설정되지 않아 지형 스냅을 건너뜁니다. 스킬 애셋의 Ground Snap Layer를 확인하세요.");
            return desiredPos; // 미설정 시엔 원래 목표 위치를 그대로 사용 — 스킬을 무력화시키지 않음
        }

        Vector2 rayStart = desiredPos + Vector2.up * 1.5f;
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, maxSnapDistance, groundLayer);

        if (hit.collider != null)
        {
            Collider2D myCol = GetComponent<Collider2D>();
            float pivotToBottom = myCol != null ? (transform.position.y - myCol.bounds.min.y) : 0f;
            return new Vector2(desiredPos.x, hit.point.y + pivotToBottom);
        }

        return (Vector2)transform.position; // 정말로 바닥이 없을 때만(=제대로 설정됐는데도 못 찾음) 현재 위치 유지
    }

    // 애니메이션 이벤트(OnAttackEnd)가 어떤 이유로든 호출되지 못했을 때를 대비한 최종 안전장치.
    // sessionId가 여전히 최신이고 아직 공격 중이라면, 스킬에 설정된 시간 이후 강제로 종료시킨다.
    private IEnumerator AttackWatchdogRoutine(int sessionId, SkillBase skill)
    {
        yield return new WaitForSeconds(skill.maxAnimationDuration);

        if (sessionId == _attackSessionId && IsAttacking)
        {
            Debug.LogWarning($"[PlayerCombat] '{skill.skillName}' 스킬의 OnAttackEnd 이벤트가 {skill.maxAnimationDuration}초 내 호출되지 않아 안전장치가 강제로 종료합니다. 클립의 Animation Event를 확인해보세요.");
            OnAttackEnd();
        }
    }

    // --- 애니메이션 이벤트 (PlayerAnimationRelay에서 전달) ---
    public void EnableAttackCollider()
    {
        if (_currentPlayingSkill != null)
            StartCoroutine(_currentPlayingSkill.ExecuteHitbox(this, transform, _stats));
    }

    public void OnAttackCombo()
    {
        // 애니메이션에서 OnAttackCombo 프레임에 도달하면 콤보 창 개방
        // 이 순간 버퍼에 예약된 게 있으면 Update문에서 즉시 다음 스킬이 나감
        _isComboWindowOpen = true;
        ResolveFacingAtComboWindow();
    }

    public void OnAttackEnd()
    {
        IsAttacking = false;
        _isComboWindowOpen = false;
        _stats.isSuperArmor = false;
        _currentPlayingSkill = null;
        _inputBuffer.Clear();

        if (_rb != null)
        {
            if (CurrentSkillContext == PlayerMovementContext.Grounded)
            {
                _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // 지상 공격만 제자리 정지
            }
            _rb.gravityScale = ResolveRestingGravity(); // ★ 3번 버그 수정
        }

        if (_playerVisual != null) _playerVisual.ReturnToLocomotion();
    }

    public void CancelAttack()
    {
        StopAllCoroutines();
        IsAttacking = false;
        _isComboWindowOpen = false;
        _stats.isSuperArmor = false;
        _currentPlayingSkill = null;
        _inputBuffer.Clear();

        if (_rb != null) _rb.gravityScale = ResolveRestingGravity();
        if (_playerVisual != null)
        {
            _playerVisual.ResetAnimationSpeed();
            _playerVisual.ReturnToLocomotion();
        }
    }

    // 콤보 창이 열리는 그 순간 방향키를 확인: 누르고 있으면 최우선 존중, 없으면 스킬이 추천하는 방향(W의 타겟 등)을 적용
    private void ResolveFacingAtComboWindow()
    {
        if (_currentPlayingSkill == null) return;

        float inputX = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(inputX) > 0.01f)
        {
            FaceDirection(inputX > 0f ? 1f : -1f);
            return;
        }

        float? preferred = _currentPlayingSkill.GetPreferredFacingDirection();
        if (preferred.HasValue) FaceDirection(preferred.Value);
    }

    // 비행 중이면 0, 아니면 원래 지상 중력값 — '무조건 _originalGravity로 되돌리던' 3번 버그의 근본 수정
    private float ResolveRestingGravity()
    {
        return (_formProvider != null && _formProvider.IsFlightForm) ? 0f : _originalGravity;
    }

    // 스킬 장착을 위한 함수 (** 추후 사용)
    public void EquipSkill(SkillSlot slot, SkillSequenceData skill)
    {
        switch (slot)
        {
            case SkillSlot.Q: sequenceQ = skill; break;
            case SkillSlot.W: sequenceW = skill; break;
            case SkillSlot.E: sequenceE = skill; break;
            case SkillSlot.R: sequenceR = skill; break;
        }
        _comboStepTracker.Remove(skill); // 새로 장착한 스킬은 항상 1타부터 시작하도록 초기화
    }

    //  W 도착 후 등 특정 스킬이 방향을 강제로 맞춰야 할 때 호출
    public void FaceDirection(float worldDir)
    {
        // ** 이 프로젝트의 스프라이트 기본 방향 관례상 flipX=true일 때 월드 오른쪽을 바라봅니다.
        // 실제로 반대로 보이면 이 한 줄만 뒤집어서 조정하세요.
        bool flipX = worldDir > 0f;
        if (_spriteRenderer != null) _spriteRenderer.flipX = flipX;
        _playerVisual?.SetFacingDirection(flipX);
    }

    // --- 헬퍼 함수 (SkillBase 자식 클래스들이 타격감/기즈모를 위해 호출) ---
    public void SetDebugHitbox(Vector2 center, Vector2 size) { _showHitbox = true; _lastHitboxCenter = center; _lastHitboxSize = size; }
    public void ClearDebugHitbox() { _showHitbox = false; }
    public void TriggerHitStop(float duration)
    {
        if (_playerVisual != null) _playerVisual.TriggerHitStop(duration);
    }

#if UNITY_EDITOR
    // 공격 히트박스 범위 기즈모
    private void OnDrawGizmosSelected()
    {
        Vector3 basePos = transform.position;
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        float dir = (sr != null && sr.flipX) ? -1f : 1f;

        // Q 슬롯 모든 타수(콤보) 박스 미리보기
        DrawSequenceGizmo(sequenceQ, basePos, dir);
        // W 슬롯 모든 타수 박스 미리보기
        DrawSequenceGizmo(sequenceW, basePos, dir);

        // 실제 런타임 타격 박스
        if (Application.isPlaying && _showHitbox)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawCube(_lastHitboxCenter, _lastHitboxSize);
        }
    }

    private void DrawSequenceGizmo(SkillSequenceData seq, Vector3 basePos, float dir)
    {
        if (!Application.isPlaying && seq != null && seq.comboSteps != null)
        {
            for (int i = 0; i < seq.comboSteps.Count; i++)
            {
                SkillBase skill = seq.comboSteps[i];
                if (skill != null)
                {
                    if (i == 0) Gizmos.color = Color.cyan;
                    else if (i == 1) Gizmos.color = Color.red;
                    else Gizmos.color = Color.yellow;

                    Vector2 previewCenter = (Vector2)basePos + new Vector2(skill.hitboxOffset.x * dir, skill.hitboxOffset.y);
                    Gizmos.DrawWireCube(previewCenter, skill.hitboxSize);

                    skill.DrawEditorGizmos(basePos, dir); // ★ 스킬별 커스텀 기즈모 훅 호출
                }
            }
        }
    }
#endif

}
