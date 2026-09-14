// GraphFilter.cs (신규, Editor 폴더)
public class GraphFilter
{
    public int day = 0;              // 0 = 전체
    public string npcTag = "";       // "" = 전체
    public string colorTag = "";
    public int hour = -1;            // -1 = 전체
    public string searchText = "";

    public bool IsEmpty => day == 0 && string.IsNullOrEmpty(npcTag)
        && string.IsNullOrEmpty(colorTag) && hour < 0 && string.IsNullOrEmpty(searchText);

    public bool Matches(GraphNodeData n)
    {
        if (IsEmpty) return true;
        if (day > 0 && n.day != day) return false;
        if (!string.IsNullOrEmpty(npcTag) && n.npcTag != npcTag) return false;
        if (!string.IsNullOrEmpty(colorTag) && n.colorTag != colorTag) return false;
        if (hour >= 0 && n.startHour >= 0 && n.endHour >= 0 && (hour < n.startHour || hour >= n.endHour)) return false;

        if (!string.IsNullOrEmpty(searchText))
        {
            string q = searchText.ToLowerInvariant();
            bool hit = (n.knotName ?? "").ToLowerInvariant().Contains(q)
                    || (n.note ?? "").ToLowerInvariant().Contains(q);
            if (!hit && n.lines != null)
                foreach (var l in n.lines)
                    if ((l.text ?? "").ToLowerInvariant().Contains(q) || (l.speakerName ?? "").ToLowerInvariant().Contains(q))
                    { hit = true; break; }
            if (!hit && n.choiceOptions != null)
                foreach (var o in n.choiceOptions)
                    if ((o.text ?? "").ToLowerInvariant().Contains(q)) { hit = true; break; }
            if (!hit) return false;
        }
        return true;
    }
}