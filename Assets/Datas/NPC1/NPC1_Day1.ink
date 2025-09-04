
=== NPC1_Day1_001 ===
{NPC1_Day1_001_meet == 0: 좋은 아침입니다. 초면이군요.}
{NPC1_Day1_001_meet == 1: ->NPC1_Day1_001_B}
{NPC1_Day1_001_meet >= 2: ->NPC1_Day1_001_A}
~ NPC1_Day1_001_meet += 1
-> DONE

===NPC1_Day1_001_B===
아침은 드셨습니까?
 + 네
    ->NPC1_Day1_001_B_01
 + 아니요 
    ->NPC1_Day1_001_B_02

===NPC1_Day1_001_B_01===
아침은 역시 먹어야 힘이 납니다.
~ NPC1_Day1_001_meet += 1
->DONE

===NPC1_Day1_001_B_02===
-이런... 못 드신 건가요, 아니면 안 드신 건가요?
-아침은 챙기시는 걸 추천드립니다.
~ NPC1_Day1_001_meet += 1
 ->DONE

===NPC1_Day1_001_A===
이야기하다보니 벌써 시간이 많이 지났네요.
-전 점심을 먹으러 가보겠습니다. 
~ NPC1_Day1_001_meet += 1
~ NPC1_friendship += 1
-> DONE

// ---------------------------

=== NPC1_Day1_002 ===
{NPC1_Day1_001_meet >= 1: 
    {NPC1_Day1_002_meet == 0: 이름이 {player_name} 씨라고 하셨죠? 또 뵙네요.}
    {NPC1_Day1_002_meet == 1: 오늘 날이 참 좋습니다. 그렇지 않나요?}
    {NPC1_Day1_002_meet == 2: 이렇게 대화를 많이 하는 것도 오랜만이군요. 즐거웠습니다. 이만 가봐야 할 시간이군요.}
    ~ NPC1_friendship += 1
}
{NPC1_Day1_001_meet == 0:
    {NPC1_Day1_002_meet == 0: 좋은 오후입니다. 초면이군요.}
    {NPC1_Day1_002_meet == 1: ...하실 말씀이라도 있으신가요?}
    {NPC1_Day1_002_meet == 2: ->NPC1_Day1_002_A}
}

~ NPC1_Day1_002_meet += 1
-> DONE


===NPC1_Day1_002_A===
날이 좋아 산책이라도 가봐야 할 것 같네요. 
- 그럼 {player_name} 씨도 좋은 하루 되시길 바랍니다.
~ NPC1_friendship += 1
->DONE

//---------------------------
=== NPC1_Day1_003 ===
{NPC1_Day1_001_meet >= 1 and NPC1_Day1_002_meet >= 1: 
    {NPC1_Day1_003_meet == 0: -> NPC1_Day1_003_A}
    {NPC1_Day1_003_meet == 1: -> NPC1_Day1_003_B}
} 
{NPC1_Day1_001_meet < 1 and NPC1_Day1_002_meet >= 1: 
    {NPC1_Day1_003_meet == 0: 좋은 저녁입니다. 또 뵙네요.}
    {NPC1_Day1_003_meet == 1: 밤산책하기 좋은 시간이네요.}
    ~ NPC1_friendship += 1
}
{NPC1_Day1_001_meet >= 1 and NPC1_Day1_002_meet < 1: 
    {NPC1_Day1_003_meet == 0: 좋은 저녁입니다. 또 뵙네요.}
    {NPC1_Day1_003_meet == 1: 전 아까 저녁먹기 전에 산책을 했습니다. 날씨가 좋아서요.}
    ~ NPC1_friendship += 1
} 
{NPC1_Day1_001_meet < 1 and NPC1_Day1_002_meet < 1:
    {NPC1_Day1_003_meet == 0: 아, 안녕하세요. 처음 인사드리네요.}
    {NPC1_Day1_003_meet == 1: 사실 낮에 봤었는데 바빠보여 인사드리지 못했습니다. 아무튼, 반갑습니다.}
    ~ NPC1_friendship += 1
}

~ NPC1_Day1_003_meet += 1
->DONE

=== NPC1_Day1_003_A ===
오늘 정말 자주 마주치는군요. 좋은 저녁입니다.
{NPC1_friendship > 3: 전 밤하늘 구경하는 걸 좋아합니다. 제가 별을 참 좋아하거든요.}
{NPC1_friendship <= 3: 밤산책을 나왔습니다. {player_name} 씨는 어쩐 일인가요?}
~ NPC1_Day1_003_meet += 1
~ NPC1_friendship += 1
->DONE

=== NPC1_Day1_003_B ===
{player_name} 씨는 대화하기를 정말 좋아하시는군요.
저도 {player_name} 씨와의 대화가 즐겁습니다. 내일 뵈어요.
~ NPC1_friendship += 3
~ NPC1_Day1_003_meet += 1
->DONE






