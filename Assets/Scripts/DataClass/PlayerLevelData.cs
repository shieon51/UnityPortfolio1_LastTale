// PlayerLevelData.cs (신규) - 플레이어 레벨별 수치(총 마나, 체력, 다음 레벨까지 필요 경험치)
[System.Serializable]
public class PlayerLevelData
{
    public int level;
    public int maxHealth;
    public int maxMana;
    public int expToNextLevel;
}