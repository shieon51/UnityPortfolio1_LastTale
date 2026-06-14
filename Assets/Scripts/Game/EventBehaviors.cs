using UnityEngine;

// 모든 이벤트 행동이 구현해야 할 인터페이스
public interface IEventBehavior
{
    void Execute(EventData eventData);
}

// 1. 잠자기 이벤트 로직
public class SleepEventBehavior : IEventBehavior
{
    public void Execute(EventData eventData)
    {
        // 현재 캐릭터가 '소라'일 때만 피로도 로직 적용
        if (PlayerManager.Instance.CurrentCharacter is SoraStats sora)
        {
            sora.RecoverFatigue(eventData.TimeTaken * 2);
        }

        PlayerManager.Instance.CurrentCharacter.Heal(PlayerManager.Instance.CurrentCharacter.maxHealth); // FullHP 대체
        PlayerManager.Instance.CurrentCharacter.RecoverMana(eventData.TimeTaken);
    }
}

// 2. 훈련하기 이벤트 로직
public class TrainingEventBehavior : IEventBehavior
{
    public void Execute(EventData eventData)
    {
        if (PlayerManager.Instance.CurrentCharacter is SoraStats sora)
        {
            sora.IncreaseFatigue(eventData.TimeTaken * 2);
        }
        PlayerManager.Instance.CurrentCharacter.GainExperience(eventData.TimeTaken * 10);
        // 추후 공격력/방어력 증가 로직 추가 가능
    }
}

// 3. 수련하기 이벤트 로직
public class PracticeEventBehavior : IEventBehavior
{
    public void Execute(EventData eventData)
    {
        if (PlayerManager.Instance.CurrentCharacter is SoraStats sora)
        {
            sora.IncreaseFatigue(eventData.TimeTaken);
        }
        PlayerManager.Instance.CurrentCharacter.GainExperience(eventData.TimeTaken * 15);
        PlayerManager.Instance.CurrentCharacter.RecoverMana(eventData.TimeTaken * 20);
        PlayerManager.Instance.CurrentCharacter.Heal(eventData.TimeTaken * 5);
    }
}

// 4. 기본/기타 이벤트 로직 (NPC 대화 등)
public class DefaultEventBehavior : IEventBehavior
{
    public void Execute(EventData eventData)
    {
        if (PlayerManager.Instance.CurrentCharacter is SoraStats sora)
        {
            sora.IncreaseFatigue(eventData.TimeTaken);
        }
    }
}