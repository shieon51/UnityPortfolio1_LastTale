using UnityEngine;

// InputBindings.cs (신규, 구조 스케치만)
public static class InputBindings
{
    // 기본값 — 나중에 PlayerPrefs에서 덮어쓰기
    public static KeyCode Attack1 = KeyCode.Q;
    public static KeyCode Attack2 = KeyCode.W;
    public static KeyCode Attack3 = KeyCode.E;
    public static KeyCode Ultimate = KeyCode.R;
    public static KeyCode Jump = KeyCode.Space;
    public static KeyCode Dash = KeyCode.LeftShift;
    public static KeyCode Guard = KeyCode.F;
    public static KeyCode Transform = KeyCode.Tab;
    // ...

    public static void Load() { /* PlayerPrefs.GetInt("bind_Attack1", (int)KeyCode.Q) 식으로 불러오기 */ }
    public static void Rebind(string actionName, KeyCode newKey) { /* 저장 + 필드 갱신 */ }
}