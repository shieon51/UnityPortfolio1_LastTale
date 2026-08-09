// FloatingTextManager.cs (신규)
using UnityEngine;

public class FloatingTextManager : Singleton<FloatingTextManager>
{
    public void Show(string text, Vector3 worldPos, Color color)
    {
        GameObject go = PoolManager.Instance.SpawnFromPool("FloatingText", worldPos, Quaternion.identity);
        go?.GetComponent<FloatingTextInstance>()?.Play(text, color);
    }

    public void ShowDamage(int amount, Vector3 worldPos) => Show(amount.ToString(), worldPos, Color.white);
    public void ShowGuard(Vector3 worldPos) => Show("방어!", worldPos, new Color(0.5f, 0.8f, 1f));
    public void ShowParry(Vector3 worldPos) => Show("패링!", worldPos, new Color(1f, 0.9f, 0.2f));
    public void ShowDodge(Vector3 worldPos) => Show("회피!", worldPos, new Color(0.6f, 1f, 0.6f));

    public void ShowGuardedDamage(int amount, Vector3 worldPos)
    {
        Show("일부 방어! " + amount, worldPos, new Color(0.5f, 0.8f, 1f));
    }
}