// FloatingTextManager.cs (신규)
using UnityEngine;

public class FloatingTextManager : Singleton<FloatingTextManager>
{
    [Header("획득 알림 (전투 피드백보다 느리게, 오래)")]
    public float acquisitionDuration = 1.6f;
    public float acquisitionRise = 1.6f;

    [Header("알림 색상")]
    public Color newInfoColor = new Color(0.4f, 0.9f, 1f);
    public Color understandingColor = new Color(0.7f, 0.9f, 1f);
    public Color itemColor = new Color(1f, 0.9f, 0.5f);
    public Color expColor = new Color(0.8f, 1f, 0.6f);
    public Color timeCrystalColor = new Color(0.8f, 0.6f, 1f);

    [Header("전투 피드백 색상")]
    public Color damageColor = Color.white;
    public Color guardColor = new Color(0.5f, 0.8f, 1f);
    public Color parryColor = new Color(1f, 0.9f, 0.2f);
    public Color dodgeColor = new Color(0.6f, 1f, 0.6f);

    public void Show(string text, Vector3 worldPos, Color color)
    {
        GameObject go = PoolManager.Instance.SpawnFromPool("FloatingText", worldPos, Quaternion.identity);
        go?.GetComponent<FloatingTextInstance>()?.Play(text, color);
    }

    public void ShowDamage(int amount, Vector3 worldPos) => Show(amount.ToString(), worldPos, damageColor);
    public void ShowGuard(Vector3 worldPos) => Show("방어!", worldPos, guardColor);
    public void ShowParry(Vector3 worldPos) => Show("패링!", worldPos, parryColor);
    public void ShowDodge(Vector3 worldPos) => Show("회피!", worldPos, dodgeColor);

    public void ShowGuardedDamage(int amount, Vector3 worldPos) => Show("일부 방어! " + amount, worldPos, guardColor);

    // -- 획득 관련 ---------
    public void ShowAcquisition(string text, Vector3 worldPos, Color color)
    {
        GameObject go = PoolManager.Instance.SpawnFromPool("FloatingText", worldPos, Quaternion.identity);
        var instance = go?.GetComponent<FloatingTextInstance>();
        if (instance == null) return;
        instance.Play(text, color, acquisitionDuration, acquisitionRise); // ★ 오버라이드 버전 사용
    }

    public void ShowNewInfo(Vector3 worldPos) => ShowAcquisition("+ 새로운 정보", worldPos, newInfoColor);
    public void ShowUnderstandingUp(string npcName, Vector3 worldPos) => ShowAcquisition($"{npcName} 이해도 상승", worldPos, new Color(0.7f, 0.9f, 1f));
    public void ShowItemGain(string itemName, Vector3 worldPos) => ShowAcquisition($"+ {itemName}", worldPos, itemColor);
    public void ShowExpGain(int amount, Vector3 worldPos) => ShowAcquisition($"+{amount} EXP", worldPos, expColor);
    public void ShowTimeCrystal(Vector3 worldPos) => ShowAcquisition("+ 시간의 결정체", worldPos, timeCrystalColor);
}