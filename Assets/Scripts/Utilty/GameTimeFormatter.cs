// GameTimeFormatter.cs (신규)
using System;

// 모든 화면의 시각 표기가 이곳을 거친다 (기획서 5-4).
// 12시간제/24시간제를 바꾸면 이벤트를 구독한 화면이 다시 그린다.
public static class GameTimeFormatter
{
    // ★ 설정 화면 연결 전 임시 저장소. 1-B에서 GameSettings 값을 여기로 넘기게 한다
    public static bool Use24Hour { get; private set; } = true;

    public static event Action OnFormatChanged;

    public static void SetUse24Hour(bool value)
    {
        if (Use24Hour == value) return;
        Use24Hour = value;
        OnFormatChanged?.Invoke();
    }

    // "13:00" 또는 "1:00 PM"
    public static string FormatTime(int hour, int minute = 0)
    {
        if (Use24Hour)
            return Format("time_format_24", "{0:00}:{1:00}", hour, minute);

        bool isPm = hour >= 12;
        int h12 = hour % 12;
        if (h12 == 0) h12 = 12;
        string ampm = isPm ? Text("time_pm", "PM") : Text("time_am", "AM");
        // 언어마다 오전/오후 위치가 다르므로 순서는 로컬라이제이션 문구가 정한다
        return Format("time_format_12", "{0}:{1:00} {2}", h12, minute, ampm);
    }

    // "Day 20 · 13:00"
    public static string FormatDayTime(int day, int hour, int minute = 0)
        => Format("daytime_format", "Day {0} · {1}", day, FormatTime(hour, minute));

    private static string Text(string key, string fallback)
    {
        var loc = LocalizationManager.Instance;
        return (loc != null && loc.Has(key)) ? loc.Get(key) : fallback;
    }

    private static string Format(string key, string fallback, params object[] args)
    {
        var loc = LocalizationManager.Instance;
        if (loc != null && loc.Has(key)) return loc.GetFormat(key, args);
        return string.Format(fallback, args);
    }
}