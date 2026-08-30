using System.Collections;
using UnityEngine;

// 소라 - 회피 기능
public class PlayerDodge : MonoBehaviour
{
    public KeyCode dodgeKey = KeyCode.LeftControl; // 임시 배정 — 나중에 InputBindings 도입 시 교체
    [Header("자원 (정신력 소모)")]
    public float mentalCostPerSecond = 15f;
    public float minHoldDuration = 0.3f; // 최소 유지 강제(깜빡임 스팸 방지)
    public float maxHoldDuration = 3f;

    private CharacterStats _stats;
    private SoraStats _soraStats;
    //private PlayerVisual _visual;
    private IPlayerMotor _motor;
    private Rigidbody2D _rb;
    private SpriteRenderer[] _renderers;
    private float _originalGravity;

    public bool IsDodging { get; private set; }
    private float _holdTimer = 0f;

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _soraStats = GetComponent<SoraStats>();
        _motor = GetComponent<IPlayerMotor>();
        _rb = GetComponent<Rigidbody2D>();
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void Update()
    {
        bool wantsDodge = Input.GetKey(dodgeKey);

        if (IsDodging)
        {
            _holdTimer += Time.deltaTime;
            _stats.GrantTemporaryInvincibility(Time.deltaTime + 0.05f); // ★ 매 프레임 살짝 여유 있게 갱신 — 지속 무적

            bool canRelease = !wantsDodge && _holdTimer >= minHoldDuration;
            bool forceEnd = _holdTimer >= maxHoldDuration || _soraStats.currentMental <= 0;
            if (canRelease || forceEnd) EndDodge();
            else _soraStats.LoseMental(Mathf.RoundToInt(mentalCostPerSecond * Time.deltaTime));
            return;
        }

        if (wantsDodge && !_motor.IsActionLocked && _soraStats.currentMental > 0)
            StartDodge();
    }

    private void StartDodge()
    {
        IsDodging = true;
        _holdTimer = 0f;
        _originalGravity = _rb.gravityScale;
        _rb.gravityScale = 0f;
    }

    private void EndDodge()
    {
        IsDodging = false;
        _rb.gravityScale = _originalGravity;
    }
}