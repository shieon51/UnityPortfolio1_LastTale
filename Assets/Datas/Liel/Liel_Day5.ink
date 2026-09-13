=== Liel_Day5_001 ===
{
    - get_suspicion("Liel") >= 30:
        -> Auto_0d48da
    - get_affection("Liel") >= 5:
        -> Auto_b5043d
    - else:
        -> Auto_e06bf3
}

=== Auto_0d48da ===
...또 오셨군요. #speak:Liel:리엘
+ 잘 지내셨어요?
    -> Auto_0ee722
+ {has_memory("liel_likes_apple")} 사과 좋아하시죠?
    -> Auto_52b7d5
+ 그냥 가볼게요
    -> Auto_79b016

=== Auto_b5043d ===
오늘도 뵙네요. 반갑습니다. #speak:Liel:리엘
+ 잘 지내셨어요?
    -> Auto_0ee722
+ {has_memory("liel_likes_apple")} 사과 좋아하시죠?
    -> Auto_52b7d5
+ 그냥 가볼게요
    -> Auto_79b016

=== Auto_e06bf3 ===
안녕하십니까. #speak:Liel:리엘
+ 잘 지내셨어요?
    -> Auto_0ee722
+ {has_memory("liel_likes_apple")} 사과 좋아하시죠?
    -> Auto_52b7d5
+ 그냥 가볼게요
    -> Auto_79b016

=== Auto_0ee722 ===
덕분에요. #speak:Liel:리엘
~ add_affection("Liel", 1)
-> DONE

=== Auto_52b7d5 ===
...제가 그 얘길 했던가요? #speak:Liel:리엘
~ add_suspicion("Liel", 5)
-> DONE

=== Auto_79b016 ===
살펴 가시지요. #speak:Liel:리엘
-> DONE

