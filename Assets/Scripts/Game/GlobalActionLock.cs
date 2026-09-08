// GlobalActionLock.cs (½Å±Ô)
using System.Collections.Generic;

public static class GlobalActionLock
{
    private static HashSet<object> _lockers = new();
    public static void Lock(object source) => _lockers.Add(source);
    public static void Unlock(object source) => _lockers.Remove(source);
    public static bool IsLocked => _lockers.Count > 0;
}