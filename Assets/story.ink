//INCLUDE story.ink

VAR day = 1
VAR time = 11
VAR meet_num = 3
VAR friendship = 0
VAR mission = true

=== NPC1 ===
{day == 1:
    {time < 12: -> NPC1_Day1_001}
    {time >= 12 and time < 18:  -> NPC1_Day1_002}
    {time >= 18: ->NPC1_Day1_003 }
}
{day == 2:
    {time > 17: -> NPC1_Day2_001}
}
-> END

=== NPC1_Day1_001 ===
좋은 아침입니다.
- 오늘은 무엇을 하실 예정인가요?
 * 아무것도 안 할 거예요
    집에 있길 좋아하시는군요.. 저도 그렇습니다.
    ~ friendship += 1
    집이 가장 편하지요.
 * 밥 먹으러 갈려구요
    그렇군요. 맛있게 드시길 바랍니다.
- 그럼 나중에 또 봅시다.
~ meet_num += 1
->DONE

=== NPC1_Day1_002 ===
{ meet_num == 1: 또 뵙는군요. 점심은 드셨습니까?}
{ meet_num < 1: 좋은 점심입니다.}
- 말씀드릴 사항이 있습니다.
- ...오늘 점심 제육입니다. 
~ friendship += 1
~ meet_num += 1
->DONE
 
=== NPC1_Day1_003 ===
{meet_num == 2: 
  "오늘 정말 자주 뵙는군요."
- else: 
    { meet_num == 1: 
     "또 뵙는군요. 벌써 저녁입니다."
    - else: "좋은 저녁입니다. 처음 인사드리지요?"
     }
 }
 
- 전 아마 내일 아침 일찍 어디를 다녀올 것 같습니다. 
- 부탁이 있는데 들어주실 수 있겠습니까?
 + 그럼요!
  당신은 참 친절하시군요. 감사합니다.
  ~ friendship += 2
  ~ mission = true
  혹시 괜찮다면 내일 아침, 점심에 아이들 밥을 챙겨주시겠어요?
  내일 저녁 전으로는 돌아오겠습니다.
 + 바빠서...
  그렇군요. 알겠습니다. 어쩔 수 없지요. 마음 쓰지 마십시요.
- 그럼 좋은 저녁 되십시오.
~ meet_num += 1
->DONE

=== NPC1_Day2_001 ===
{ meet_num < 1: 아, 안녕하세요. 어제 통 안 보이시던데, 무슨 일 있으셨습니까?}
{ meet_num >= 1: 아, 안녕하세요. 좋은 오후입니다.}
{ mission == false: 방금 외출을 하고 오느라 정신이 없네요... 별 일은 없었지요?}
{ mission == true: 어제의 부탁을 들어주셔서 고맙습니다.}

{ friendship >= 3: 혹시 시간 되신다면... 제가 저녁을 사도 괜찮을까요?}
{ friendship < 3: 그럼 전 저녁을 먹으러 가보겠습니다. 좋은 저녁 되십시오.}
->DONE
