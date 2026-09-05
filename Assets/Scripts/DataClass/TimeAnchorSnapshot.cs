using System.Collections.Generic;
using UnityEngine;

// TimeAnchorSnapshot.cs (신규) — "시간 고정" 시점의 스냅샷
public class TimeAnchorSnapshot
{
    public int sceneID;
    public Vector2 position;
    public int day, hour;
    public int level, maxHealth, maxMana, experience;
    public HashSet<string> acquiredMemoryFlags;
}