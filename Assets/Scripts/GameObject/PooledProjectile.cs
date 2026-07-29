using System.Collections;
using UnityEngine;

// PooledProjectile.cs — 투사체 전용 (스스로 이동 + 충돌 판정)
public class PooledProjectile : MonoBehaviour
{
    public string poolName;
    public PoolType poolType = PoolType.Global;
    public float speed = 10f;
    public float maxLifetime = 3f;
    public LayerMask hitLayer;
    public string hitEffectVFXName;

    private Vector2 _direction;
    private float _damage;
    private CharacterStats _caster;
    private ElementType _element;

    public void Launch(Vector2 direction, float damage, CharacterStats caster, ElementType element)
    {
        _direction = direction.normalized;
        _damage = damage;
        _caster = caster;
        _element = element;
        StopAllCoroutines();
        StartCoroutine(LifetimeRoutine());
    }

    private void Update()
    {
        transform.position += (Vector3)(_direction * speed * Time.deltaTime);
        Collider2D hit = Physics2D.OverlapCircle(transform.position, 0.2f, hitLayer);
        if (hit != null) HandleHit(hit);
    }

    private void HandleHit(Collider2D hit)
    {
        CharacterStats target = hit.GetComponentInParent<CharacterStats>();
        if (target != null) target.TakeDamage(Mathf.RoundToInt(_damage), _element, _caster);

        if (!string.IsNullOrEmpty(hitEffectVFXName))
            VFXManager.Instance.Play(hitEffectVFXName, transform.position, Quaternion.identity);

        PoolManager.Instance.ReturnToPool(gameObject, poolType);
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(maxLifetime);
        PoolManager.Instance.ReturnToPool(gameObject, poolType);
    }
}