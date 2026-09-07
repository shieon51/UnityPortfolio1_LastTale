// CsvTableLoader.cs (신규)
using System;
using System.IO;
using UnityEngine;

public static class CsvTableLoader
{
    public static void Load(string fileName, Action<string[]> onEachRow)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Datas", fileName);
        if (!File.Exists(path)) { Debug.LogWarning($"[CsvTableLoader] 파일 없음: {path}"); return; }
        var lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            onEachRow(lines[i].Split(','));
        }
    }
}