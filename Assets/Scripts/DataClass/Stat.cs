using System.Collections.Generic;
using UnityEngine;


// [Stat.cs] 하나의 능력치를 관리하는 모듈
[System.Serializable]
public class Stat
{
    [SerializeField]
    private int baseValue;

    private List<int> modifiers = new List<int>(); //버프, 디버프 리스트

    public Stat(int startingValue)
    {
        baseValue = startingValue;
    }

    // 최종 스탯 값 반환 (기본값 + 버프/디버프)
    public int GetValue()
    {
        int finalValue = baseValue;
        modifiers.ForEach(x => finalValue += x); //modifiers 각각의 값을 finalValue에 더해준다
        return finalValue;
    }

    // 수련/이벤트 등으로 영구적으로 스탯을 올릴 때 사용
    // 수련 등으로 기본값 자체를 올린다 (장비 보정과 별개)
    public void AddBaseValue(int amount)
    {
        baseValue += amount;
    }

    // ★ 회귀 시 몸 상태를 되돌리기 위해 기본값을 읽고 쓸 수 있게 한다.
    //   버프·디버프(modifiers)는 건드리지 않는다 (요정화 보정 등은 그대로 유지)
    public int BaseValue => baseValue;
    public void SetBaseValue(int value) => baseValue = value;

    // 일시적인 버프/디버프 추가 (요정화 등)
    public void AddModifier(int modifier)
    {
        if (modifier != 0) modifiers.Add(modifier);
    }

    public void RemoveModifier(int modifier)
    {
        if (modifier != 0) modifiers.Remove(modifier);
    }
}
