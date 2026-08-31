using UnityEngine;

// ManaGatherParticles.cs — 개선판
[RequireComponent(typeof(ParticleSystem))]
public class ManaGatherParticles : MonoBehaviour
{
    public Transform target;
    public float attractStrength = 4f;
    public float accelerationCurvePower = 2f; // ★ 가까울수록 훨씬 빠르게
    public float swirlStrength = 1.5f;
    public float swirlFrequency = 0.5f;
    public float consumeDistance = 0.3f; // ★ 이 거리 안으로 오면 흡수 처리
    public GameObject consumeBurstPrefab; // ★ 선택 — 작은 반짝임, 없어도 동작

    private ParticleSystem _ps;
    private ParticleSystem.Particle[] _particles;

    private void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        _particles = new ParticleSystem.Particle[_ps.main.maxParticles];
    }

    private void LateUpdate()
    {
        if (target == null) return;
        int count = _ps.GetParticles(_particles);
        for (int i = 0; i < count; i++)
        {
            Vector3 toTarget = (Vector3)target.position - _particles[i].position;
            float dist = toTarget.magnitude;

            if (dist < consumeDistance)
            {
                if (consumeBurstPrefab != null) Instantiate(consumeBurstPrefab, _particles[i].position, Quaternion.identity);
                _particles[i].remainingLifetime = 0f;
                continue;
            }

            Vector3 pullDir = toTarget.normalized;
            float proximityFactor = Mathf.Pow(Mathf.Clamp01(1f - dist / 5f), accelerationCurvePower); // ★ 이징
            Vector3 swirl = Vector3.Cross(pullDir, Vector3.forward) * swirlStrength * Mathf.Sin(Time.time * swirlFrequency + i);
            Vector3 finalDir = (pullDir + swirl * (1f - proximityFactor) * 0.3f).normalized; // 가까워질수록 소용돌이 줄고 직진성↑

            _particles[i].velocity = finalDir * attractStrength * (0.4f + proximityFactor); // 최소속도 보장 + 가속
        }
        _ps.SetParticles(_particles, count);
    }

    // ★ Exit()에서 부를 것 — 방출만 멈추고 이미 있는 파티클은 자연스럽게 다 빨려들어갈 때까지 살려둠
    public void StopEmittingAndFinish() => _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    public bool IsFullyFinished => !_ps.IsAlive(true);
}