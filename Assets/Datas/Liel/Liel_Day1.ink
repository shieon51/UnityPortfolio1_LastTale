
=== Liel_Day1_001 ===
#speak:Liel:???
{get_counter("Liel_Day1_001_meet") == 0: 좋은 아침입니다. 초면이군요.}
{get_counter("Liel_Day1_001_meet") == 1: ->Liel_Day1_001_B}
{get_counter("Liel_Day1_001_meet") >= 2: ->Liel_Day1_001_A}
~ increment_counter("Liel_Day1_001_meet")
-> DONE

===Liel_Day1_001_B===
전 리엘이라고 합니다. #speak:Liel:리엘
-제가 좋아하는 과일이 무엇인지 아십니까?
    + {not has_memory("liel_likes_apple")} 뭔데요?
        -> Liel_Day1_001_B_02
    + {has_memory("liel_likes_apple")} 사과
        -> Liel_Day1_001_B_01
    + {has_memory("liel_likes_apple")} 오렌지
        -> Liel_Day1_001_B_wrong
    + {has_memory("liel_likes_apple")} 딸기
        -> Liel_Day1_001_B_wrong

===Liel_Day1_001_B_01===
맞습니다. 어떻게 아셨습니까? #cue:test_shake
~ increment_counter("Liel_Day1_001_meet")
->DONE

===Liel_Day1_001_B_02===
제가 좋아하는 과일은 사과입니다.
~ acquire_memory("liel_likes_apple")
~ increment_counter("Liel_Day1_001_meet")
 ->DONE

===Liel_Day1_001_B_wrong===
아쉽게도 틀렸군요.
-> Liel_Day1_001_B_02

===Liel_Day1_001_A===
이야기하다보니 벌써 시간이 많이 지났네요. #speak:Liel:리엘 #auto:1.5
-전 점심을 먹으러 가보겠습니다. #auto:1.5
네, 다녀오세요. #speak:Player:소라
~ increment_counter("Liel_Day1_001_meet")
~ add_affection("Liel", 1)
-> DONE

// ---------------------------

=== Liel_Day1_002 ===
#speak:Liel:리엘
{get_counter("Liel_Day1_001_meet") >= 1: 
    {get_counter("Liel_Day1_002_meet") == 0: 이름이 {player_name} 씨라고 하셨죠? 또 뵙네요.}
    {get_counter("Liel_Day1_002_meet") == 1: 오늘 날이 참 좋습니다. 그렇지 않나요?}
    {get_counter("Liel_Day1_002_meet") == 2: 이렇게 대화를 많이 하는 것도 오랜만이군요. 즐거웠습니다. 이만 가봐야 할 시간이군요.}
    ~ add_affection("Liel", 1)
}
{get_counter("Liel_Day1_001_meet") == 0:
    {get_counter("Liel_Day1_002_meet") == 0: 좋은 오후입니다. 초면이군요.}
    {get_counter("Liel_Day1_002_meet") == 1: ...하실 말씀이라도 있으신가요?}
    {get_counter("Liel_Day1_002_meet") == 2: ->Liel_Day1_002_A}
}

~ increment_counter("Liel_Day1_002_meet")
-> DONE


===Liel_Day1_002_A===
날이 좋아 산책이라도 가봐야 할 것 같네요. 
- 그럼 {player_name} 씨도 좋은 하루 되시길 바랍니다.
~ add_affection("Liel", 1)
->DONE

//---------------------------
=== Liel_Day1_003 ===
#speak:Liel:리엘
{get_counter("Liel_Day1_001_meet") >= 1 and get_counter("Liel_Day1_002_meet") >= 1: 
    {get_counter("Liel_Day1_003_meet") == 0: -> Liel_Day1_003_A}
    {get_counter("Liel_Day1_003_meet") == 1: -> Liel_Day1_003_B}
} 
{get_counter("Liel_Day1_001_meet") < 1 and get_counter("Liel_Day1_002_meet") >= 1: 
    {get_counter("Liel_Day1_003_meet") == 0: 좋은 저녁입니다. 또 뵙네요.}
    {get_counter("Liel_Day1_003_meet") == 1: 밤산책하기 좋은 시간이네요.}
    ~ add_affection("Liel", 1)
}
{get_counter("Liel_Day1_001_meet") >= 1 and get_counter("Liel_Day1_002_meet") < 1: 
    {get_counter("Liel_Day1_003_meet") == 0: 좋은 저녁입니다. 또 뵙네요.}
    {get_counter("Liel_Day1_003_meet") == 1: 전 아까 저녁먹기 전에 산책을 했습니다. 날씨가 좋아서요.}
    ~ add_affection("Liel", 1)
} 
{get_counter("Liel_Day1_001_meet") < 1 and get_counter("Liel_Day1_002_meet") < 1:
    {get_counter("Liel_Day1_003_meet") == 0: 아, 안녕하세요. 처음 인사드리네요.}
    {get_counter("Liel_Day1_003_meet") == 1: 사실 낮에 봤었는데 바빠보여 인사드리지 못했습니다. 아무튼, 반갑습니다.}
    ~ add_affection("Liel", 1)
}

~ increment_counter("Liel_Day1_003_meet")
->DONE

=== Liel_Day1_003_A ===
오늘 정말 자주 마주치는군요. 좋은 저녁입니다.
{get_affection("Liel") > 3: 전 밤하늘 구경하는 걸 좋아합니다. 제가 별을 참 좋아하거든요.} # ?
{get_affection("Liel") <= 3: 밤산책을 나왔습니다. {player_name} 씨는 어쩐 일인가요?}
~ increment_counter("Liel_Day1_003_meet")
~ add_affection("Liel", 1)
->DONE

=== Liel_Day1_003_B ===
{player_name} 씨는 대화하기를 정말 좋아하시는군요.
저도 {player_name} 씨와의 대화가 즐겁습니다. 내일 뵈어요.
~ add_affection("Liel", 3)
~ increment_counter("Liel_Day1_003_meet")
->DONE






