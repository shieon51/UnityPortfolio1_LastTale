using UnityEngine;

// DirectionalSprite.cs (신규) — 정지 이미지 한 장짜리 파츠용 (작은 장식, 액세서리 등): 정지 이미지, 방향별 전용 그림 필요
public class DirectionalSprite : MonoBehaviour
{
    public Sprite leftSprite;
    public Sprite rightSprite;
    private SpriteRenderer _sr;
    private void Awake() => _sr = GetComponent<SpriteRenderer>();

    public void ApplyFacing(bool facingRight)
    {
        _sr.sprite = facingRight ? rightSprite : leftSprite;
        _sr.flipX = false;
    }
}