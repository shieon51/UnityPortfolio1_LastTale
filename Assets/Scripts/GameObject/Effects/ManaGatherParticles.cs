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

    [Header("부드러움")]
    [Tooltip("낮을수록 연기처럼 느긋하게 방향을 바꿈, 높을수록 즉각 반응")]
    public float velocitySmoothing = 3f;

    private Vector3[] _currentVelocities;

    private void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        _particles = new ParticleSystem.Particle[_ps.main.maxParticles];
        _currentVelocities = new Vector3[_ps.main.maxParticles]; // ★ 추가
    }

    private void LateUpdate()
    {
        if (target == null) return;
        int count = _ps.GetParticles(_particles);
        bool isLocalSpace = _ps.main.simulationSpace == ParticleSystemSimulationSpace.Local; // ★ 추가

        for (int i = 0; i < count; i++)
        {
            Vector3 worldPos = isLocalSpace ? transform.TransformPoint(_particles[i].position) : _particles[i].position; // ★ 좌표계 맞춤
            Vector3 toTarget = target.position - worldPos;
            float dist = toTarget.magnitude;

            if (dist < consumeDistance)
            {
                if (consumeBurstPrefab != null) Instantiate(consumeBurstPrefab, worldPos, Quaternion.identity);
                _particles[i].remainingLifetime = 0f;
                continue;
            }

            Vector3 pullDir = toTarget.normalized;
            float proximityFactor = Mathf.Pow(Mathf.Clamp01(1f - dist / 5f), accelerationCurvePower);
            Vector3 swirl = Vector3.Cross(pullDir, Vector3.forward) * swirlStrength * Mathf.Sin(Time.time * swirlFrequency + i);
            Vector3 worldDir = (pullDir + swirl * (1f - proximityFactor) * 0.3f).normalized;
            Vector3 targetVelocity = worldDir * attractStrength * (0.4f + proximityFactor);
            _currentVelocities[i] = Vector3.Lerp(_currentVelocities[i], targetVelocity, Time.deltaTime * velocitySmoothing);
            _particles[i].velocity = isLocalSpace ? transform.InverseTransformDirection(_currentVelocities[i]) : _currentVelocities[i];
        }
        _ps.SetParticles(_particles, count);
    }

    // ★ Exit()에서 부를 것 — 방출만 멈추고 이미 있는 파티클은 자연스럽게 다 빨려들어갈 때까지 살려둠
    public void StopEmittingAndFinish()
    {
        if (_ps == null) _ps = GetComponent<ParticleSystem>(); // ★ 방어적 재확보
        if (_ps == null) return;
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public bool IsFullyFinished => _ps == null || !_ps.IsAlive(true); // ★ null이면 "끝난 것"으로 간주 — 무한 대기 방지
}