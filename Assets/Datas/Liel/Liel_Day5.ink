=== Liel_Day5_001 ===
{
    - get_suspicion("Liel") >= 30:
        -> Auto_bae004
    - get_affection("Liel") >= 5:
        -> Auto_c2fd80
    - else:
        -> Auto_dd1e53
}

=== Auto_bae004 ===
리엘의 눈빛이 평소와 달랐다. #system
...또 오셨군요. #speak:Liel:리엘 #auto:1.5
~ add_suspicion("Liel", 1)
무슨 용건입니까. #speak:Liel:리엘
+ 잘 지내셨어요?
    -> Auto_420920
+ {has_memory("liel_likes_apple")} 사과 좋아하시죠?
    -> Auto_981f26
+ 그냥 가볼게요
    -> Auto_420920

=== Auto_c2fd80 ===
오늘도 뵙네요. #speak:Liel:리엘
+ 잘 지내셨어요?
    -> Auto_420920
+ {has_memory("liel_likes_apple")} 사과 좋아하시죠?
    -> Auto_981f26
+ 그냥 가볼게요
    -> Auto_420920

=== Auto_dd1e53 ===
안녕하십니까. #speak:Liel:리엘
+ 잘 지내셨어요?
    -> Auto_420920
+ {has_memory("liel_likes_apple")} 사과 좋아하시죠?
    -> Auto_981f26
+ 그냥 가볼게요
    -> Auto_420920

=== Auto_420920 ===
덕분에요. #speak:Liel:리엘
~ add_affection("Liel", 1)
-> DONE

=== Auto_981f26 ===
{
    - get_counter("Liel") >= 5:
        -> Auto_a7de4c
    - get_suspicion("Liel") >= 30:
        -> Auto_c65fe3
    - else:
        -> DONE
}

=== Auto_c65fe3 ===
...제가 그 얘길 했던가요? #speak:Liel:리엘
~ add_suspicion("Liel", 5)
-> DONE

=== Auto_a7de4c ===
기억하고 계시는군요. #speak:Liel:리엘
~ add_affection("Liel", 1)
-> DONE

