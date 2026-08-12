// EyeBlinkController.cs (신규) — Eyes 오브젝트에 부착
using UnityEngine;

public class EyeBlinkController : MonoBehaviour
{
    private SpriteRenderer _sr;

    public Animator eyeAnimator;
    public float minInterval = 2f;
    public float maxInterval = 6f;

    private float _timer;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable() => ResetTimer();

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            eyeAnimator.SetTrigger("Blink");
            ResetTimer();
        }
    }

    private void ResetTimer() => _timer = Random.Range(minInterval, maxInterval);

    public void SetVisible(bool visible)
    {
        if (_sr != null) _sr.enabled = visible;
    }
}