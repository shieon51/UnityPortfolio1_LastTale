// CsvTableLoader.cs (신규)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class CsvTableLoader
{
    public static void Load(string fileName, Action<string[]> onEachRow)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Datas", fileName);
        if (!File.Exists(path)) { Debug.LogWarning($"[CsvTableLoader] 파일 없음: {path}"); return; }
        var lines = File.ReadAllLines(path, Encoding.UTF8); // ★ 명시적 UTF-8
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            onEachRow(lines[i].Split(','));
        }
    }

    // ★ 큰따옴표 안의 쉼표는 분리 안 하는 간단한 파서 (문장에 쉼표 들어갈 일이 많아서 추가)
    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') inQuotes = !inQuotes;
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