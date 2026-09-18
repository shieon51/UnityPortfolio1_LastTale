using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("LocalizationTable.csv의 키")]
    public string key;

    private TMP_Text _text;
    private object[] _args;

    private void Awake() => _text = GetComponent<TMP_Text>();

    private void OnEnable()
    {
        var loc = LocalizationManager.Instance;
        if (loc != null) loc.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        var loc = LocalizationManager.Instance;
        if (loc != null) loc.OnLanguageChanged -= Refresh;
    }

    // 런타임에 다른 문구로 바꿀 때
    public void SetKey(string newKey)
    {
        key = newKey;
        Refresh();
    }

    // "마나가 {0} 부족합니다" 처럼 값이 들어가는 문구용
    public void SetArgs(params object[] args)
    {
        _args = args;
        Refresh();
    }

    public void Refresh()
    {
        if (_text == null) _text = GetComponent<TMP_Text>();
        var loc = LocalizationManager.Instance;
        if (_text == null || loc == null || string.IsNullOrEmpty(key)) return;

        _text.text = (_args == null || _args.Length == 0)
            ? loc.Get(key)
            : loc.GetFormat(key, _args);
    }
}