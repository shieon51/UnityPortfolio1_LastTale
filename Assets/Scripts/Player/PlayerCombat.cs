using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    private Rigidbody2D _rb;
    private CharacterStats _stats;
    private SoraStats _soraStats;
    private SpriteRenderer _spriteRenderer;
    private PlayerVisual _playerVisual; // 애니메이션 강제 동기화용

    public bool IsAttacking { get; private set; }
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

    // 콤보 허용 창. 이게 true일 때 키를 누르면 이전 모션을 씹고 즉시 다음 모션이 나감
    private bool _isComboWindowOpen = false;

    // 디버그용 (스킬 클래스에서 호출받음)
    private bool _showHitbox = false;
    private Vector2 _lastHitboxCenter;
    private Vector2 _lastHitboxSize;

    // ** 스킬 클래스(MeleeDashSkill 등)가 플레이어가 바라보는 방향을 쉽게 알 수 있도록 열어주는 프로퍼티
    public float FacingDirection => (_spriteRenderer != null && _spriteRenderer.flipX) ? -1f : 1f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _stats = GetComponent<CharacterStats>();
        _soraStats = GetComponent<SoraStats>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _playerVisual = GetComponentInChildren<PlayerVisual>();
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
        _isComboWindowOpen = false; // 새로운 스킬이 시작됐으니 콤보 창 닫음

        // 현재 이 슬롯(예: Q)이 몇 타째인지 가져옴 (없으면 0타)
        int step = _comboStepTracker.GetValueOrDefault(seq, 0);

        // 콤보가 끝났으면 0타로 리셋
        if (step >= seq.comboSteps.Count) step = 0;

        SkillBase skillToPlay = seq.comboSteps[step];

        // --- 마나/오버캐스트 연산 ---
        int actualManaCost = _stats.CalculateManaCost(skillToPlay.requiredMana);
        if (_stats.currentMana < actualManaCost)
        {
            int deficit = actualManaCost - _stats.currentMana;
            _stats.UseMana(_stats.currentMana);

            if (_soraStats != null) _soraStats.IncreaseFatigue(deficit * 2);
            _stats.TakeDamage(deficit, ElementType.Normal);
        }
        else _stats.UseMana(actualManaCost);

        // --- 실행 ---
        IsAttacking = true;
        _stats.isSuperArmor = true;
        _currentPlayingSkill = skillToPlay;

        // 다음 콤보 스텝 미리 증가시켜두기
        _comboStepTracker[seq] = step + 1;

        // Visual 스크립트를 통해 모든 파츠(Body, Hair 등) 애니메이션 동시 재생!
        if (_playerVisual != null) _playerVisual.PlayAttackAnimation(skillToPlay.animStateName);

        StartCoroutine(skillToPlay.ExecuteSkillBehavior(this, _rb, null, _stats));
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
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            _rb.gravityScale = _originalGravity;
        }

        if (_playerVisual != null) _playerVisual.ReturnToMovement();
    }

    public void CancelAttack()
    {
        StopAllCoroutines();
        IsAttacking = false;
        _isComboWindowOpen = false;
        _stats.isSuperArmor = false;
        _currentPlayingSkill = null;
        _inputBuffer.Clear();

        if (_rb != null) _rb.gravityScale = _originalGravity;
        if (_playerVisual != null)
        {
            _playerVisual.ResetAnimationSpeed();
            _playerVisual.ReturnToMovement();
        }
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
                }
            }
        }
    }
#endif

}
