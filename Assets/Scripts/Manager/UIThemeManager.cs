using TMPro;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "LastMarchan/UI/UI Theme")]
public class UITheme : ScriptableObject
{
    [Header("Panel")]
    public Sprite panelBackground;
    public Color panelTint = Color.white;

    [Header("Button")]
    public Sprite buttonNormal, buttonHover, buttonPressed;

    [Header("Text")]
    public TMP_FontAsset defaultFont;
    public Color primaryTextColor = Color.white;
    public Color accentColor = new Color(1f, 0.85f, 0.4f);
}

public class UIThemeManager : Singleton<UIThemeManager>
{
    public UITheme CurrentTheme;

    public void SetTheme(UITheme newTheme)
    {
        CurrentTheme = newTheme;
        foreach (var p in FindObjectsOfType<ThemedPanel>(true)) p.Apply();
    }
}

public class ThemedPanel : MonoBehaviour
{
    private Image _bg;
    private void Awake() => _bg = GetComponent<Image>();
    private void OnEnable() => Apply();

    public void Apply()
    {
        var theme = UIThemeManager.Instance?.CurrentTheme;
        if (theme == null || _bg == null) return;
        _bg.sprite = theme.panelBackground;
        _bg.color = theme.panelTint;
        _bg.type = Image.Type.Sliced;
    }
}