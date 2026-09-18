// CsvTableLoader.cs (신규)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class CsvTableLoader
{
    // UTF-8로 해석할 수 없는 바이트가 바뀌어 들어가는 문자 (유니코드 표준 대체 문자라 설정값 아님)
    private const char InvalidUtf8Char = '\uFFFD';

    public static void Load(string fileName, Action<string[]> onEachRow)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Datas", fileName);
        if (!File.Exists(path)) { Debug.LogWarning($"[CsvTableLoader] 파일 없음: {path}"); return; }
        var lines = File.ReadAllLines(path, Encoding.UTF8); // ★ 명시적 UTF-8

        bool encodingWarned = false;
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue; // ★ 공백만 있는 줄도 건너뜀

            // ★ B-15: CP949 등 UTF-8이 아닌 인코딩으로 저장된 파일 감지 — 한글이 깨진 채 조용히 로드되는 것 방지
            if (!encodingWarned && lines[i].IndexOf(InvalidUtf8Char) >= 0)
            {
                Debug.LogError($"[CsvTableLoader] '{fileName}' {i + 1}번째 줄에서 깨진 문자 발견 — UTF-8로 저장되지 않은 파일일 가능성. 엑셀에서 'CSV UTF-8(쉼표로 분리)'로 다시 저장할 것");
                encodingWarned = true;
            }

            onEachRow(SplitCsvLine(lines[i])); // ★ B-25: Split(',') → 따옴표 안 쉼표를 보존하는 파서로 교체
        }
    }

    // ★ 큰따옴표 안의 쉼표는 분리하지 않음. 따옴표 안의 "" 는 따옴표 한 개로 처리 (엑셀 CSV 저장 규칙)
    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                bool isEscapedQuote = inQuotes && i + 1 < line.Length && line[i + 1] == '"';
                if (isEscapedQuote) { current.Append('"'); i++; } // ★ "" → "
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    public static string Get(string[] cols, int index, string fallback = "")
        => cols.Length > index && !string.IsNullOrWhiteSpace(cols[index]) ? cols[index].Trim() : fallback;

    public static int GetInt(string[] cols, int index, int fallback = 0)
        => int.TryParse(Get(cols, index), out var v) ? v : fallback;

    public static bool GetBool(string[] cols, int index, bool fallback = false)
        => bool.TryParse(Get(cols, index), out var v) ? v : fallback;

    public static float GetFloat(string[] cols, int index, float fallback = 0f)
    => float.TryParse(Get(cols, index), out var v) ? v : fallback;

}