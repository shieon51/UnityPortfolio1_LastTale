// BattleBarkTrigger.cs (½Å±Ô)
[System.Serializable]
public class BattleBarkTrigger
{
    public enum TriggerType { HealthBelowPercent, PhaseEntered, ParrySuccess, ManaBelowPercent }
    public TriggerType type;
    public float threshold;
    public int phaseNumber;
    public string barkKnotName;
    public bool onceOnly = true;
}