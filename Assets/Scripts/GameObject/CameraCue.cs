// CameraCue.cs (신규) — VFX 큐와 완전히 같은 사상, 스킬별로 다르게 설정 가능

using UnityEngine;

[System.Serializable]
public class CameraCue
{
    public string cueId; // "impact", "cast" 등
    public bool shake;
    public float shakeDuration = 0.15f;
    public float shakeIntensity = 0.2f;
    public bool flash;
    public Color flashColor = Color.white;
    public float flashDuration = 0.1f;
    public bool instantSnap; // W처럼 카메라가 순간이동해야 하는 스킬용
}