using UnityEngine;

public class HitFlashController : MonoBehaviour
{
    public float flashDuration = 0.1f;
    public Color flashColor = Color.white;

    private SpriteRenderer[] _renderers;
    private Color[] _originalColors;
    private float _flashTimer = 0f;
    private bool _isFlashing = false;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _originalColors[i] = _renderers[i].color;
    }

    public void Flash()
    {
        _flashTimer = flashDuration;
        _isFlashing = true;
    }

    private void LateUpdate()
    {
        if (!_isFlashing) return;

        foreach (var r in _renderers)
            if (r != null) r.color = flashColor;

        _flashTimer -= Time.deltaTime;
        if (_flashTimer <= 0f)
        {
            _isFlashing = false;
            for (int i = 0; i < _renderers.Length; i++) // ★ 명시적으로 원래 색 복귀
                if (_renderers[i] != null) _renderers[i].color = _originalColors[i];
        }
    }
}