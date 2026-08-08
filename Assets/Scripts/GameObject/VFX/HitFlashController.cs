using System.Collections;
using UnityEngine;

// HitFlashController.cs (신규) — Player/NPC Visual 오브젝트에 부착
public class HitFlashController : MonoBehaviour
{
    public float flashDuration = 0.1f;
    public Color flashColor = Color.white;

    private SpriteRenderer[] _renderers;
    private Color[] _originalColors;
    private Coroutine _routine;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _originalColors[i] = _renderers[i].color;
    }

    public void Flash()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        foreach (var r in _renderers) if (r != null) r.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].color = _originalColors[i];
        _routine = null;
    }
}