using UnityEngine;

// Visual 하위 각 파츠 오브젝트(Body, Face, Hair...)에 부착
[RequireComponent(typeof(Animator))]
public class BodyPartSlotTag : MonoBehaviour
{
    public BodyPartSlot slot;
    public Animator Animator => GetComponent<Animator>();
}