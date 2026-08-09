using System.Collections;
using UnityEngine;

// HitFlashController.cs (신규) — Player/NPC Visual 오브젝트에 부착
public class HitFlashController : MonoBehaviour
{
    public float flashDuration = 0.1f;
    public Color flashColor = Color.white;

    private SpriteRenderer[] _renderers;
    private float _flashTimer = 0f;
    private bool _isFlashing = false;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public void Flash()
    {
        Debug.Log("[HitFlashController] Flash() 호출됨"); // ★ 임시 — 원인 확인 후 지우세요 //**
        _flashTimer = flashDuration;
        _isFlashing = true;
    }

    // ★ LateUpdate = Animator가 그 프레임의 색상 갱신을 끝낸 '이후'에 실행됨.
    //   Animator가 색상 커브를 갖고 있어도, 이 코드가 매 프레임 다시 덮어써서 항상 이깁니다.
    private void LateUpdate()
    {
        if (!_isFlashing) return;

        foreach (var r in _renderers)
            if (r != null) r.color = flashColor;

        _flashTimer -= Time.deltaTime;
        if (_flashTimer <= 0f) _isFlashing = false; // 종료되면 더 이상 색을 안 건드림 (원래 상태로 자연 복귀)
    }
}