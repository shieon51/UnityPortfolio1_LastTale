// ============================================
// Day 4 — 아침 (8~12시) : 이해도/의심 종합 테스트
// ============================================
=== Liel_Day4_001 ===
{get_counter("Liel_Day4_001_meet") == 0: 좋은 아침입니다. 오늘도 뵙는군요. #speak:Liel:리엘}
{get_counter("Liel_Day4_001_meet") >= 1: 아직 하실 말씀이 남으셨습니까? #speak:Liel:리엘}
+ {get_understanding_percent("Liel") >= 3} 리엘 씨는 왜 기사단에 들어가셨어요?
    -> Liel_Day4_001_Knight
+ {has_memory("liel_sister_exists") and not has_memory("liel_monster_incident")} 여동생분 일은... 괜찮으세요?
    ~ add_suspicion_for("Liel", 5, "Liel_Day4_001_meet")
    -> Liel_Day4_001_SisterCare
+ {has_memory("liel_sister_full_story") and not has_memory("liel_monster_incident")} 6년 전 그 사건 말인데요
    ~ add_suspicion_for("Liel", 25, "Liel_Day4_001_meet")
    -> Liel_Day4_001_Incident
+ {get_counter("Liel_Day1_002_meet") == 0 and get_counter("Liel_Day4_001_meet") >= 1} 어제 낮에 하신 말씀이 계속 생각나서요
    ~ add_suspicion_for("Liel", 40, "Liel_Day1_002_meet")
    -> Liel_Day4_001_NeverMet
+ 그냥 인사드리러 왔어요
    -> Liel_Day4_001_Greeting
- ~ increment_counter("Liel_Day4_001_meet")
-> END

=== Liel_Day4_001_Knight ===
갑자기 그런 걸 물으시는군요. #speak:Liel:리엘
{get_affection("Liel") >= 5:
    ...지키고 싶은 것이 있었습니다. 그뿐입니다.
    ~ acquire_memory("liel_knight_reason")
- else:
    글쎄요. 그냥 어쩌다 보니 그렇게 되었습니다.
}
~ add_affection("Liel", 1)
~ increment_counter("Liel_Day4_001_meet")
-> DONE

=== Liel_Day4_001_SisterCare ===
...신경 써주셔서 감사합니다. #speak:Liel:리엘
- 오래된 일입니다. 이제는 괜찮습니다.
~ add_affection("Liel", 2)
~ increment_counter("Liel_Day4_001_meet")
-> DONE

=== Liel_Day4_001_Incident ===
{get_suspicion("Liel") >= 40: 
- ...제가 그 얘길 한 적이 있었던가요? #speak:Liel:리엘
}
{get_suspicion("Liel") >= 15 and get_suspicion("Liel") < 40: 
- 그 일을 어떻게 아십니까? #speak:Liel:리엘
}
{get_suspicion("Liel") < 15: 
- 아, 그 사건 말이군요. 소문이 돌긴 했겠죠. #speak:Liel:리엘
}
- 6년 전, 마물이 마을을 덮쳤습니다. 그날 여동생을 잃어버렸지요.
~ acquire_memory("liel_monster_incident")
~ add_affection("Liel", -1)
~ increment_counter("Liel_Day4_001_meet")
-> DONE

=== Liel_Day4_001_NeverMet ===
...어제 낮이요? #speak:Liel:리엘
- 저는 어제 낮에 {player_name} 씨를 뵌 적이 없습니다만.
{get_suspicion("Liel") >= 50: - ...무슨 말씀을 하시는 겁니까, 대체.}
{get_suspicion("Liel") < 50: 
- 사람을 잘못 보신 게 아닐까요.
}
~ add_affection("Liel", -2)
~ increment_counter("Liel_Day4_001_meet")
-> DONE

=== Liel_Day4_001_Greeting ===
예, 좋은 하루 되시길 바랍니다. #speak:Liel:리엘
~ add_affection("Liel", 1)
~ increment_counter("Liel_Day4_001_meet")
-> DONE

// ============================================
// Day 4 — 오후 (13~16시) : 자동 발동 + 1회 제한 테스트
// ============================================
=== Liel_Day4_002 ===
아, {player_name} 씨. #speak:Liel:리엘 #auto:1.5
마침 잘 오셨습니다. #auto:1.5
- 잠시 시간 괜찮으십니까?
+ 네, 괜찮아요
    -> Liel_Day4_002_Accept
+ 지금은 좀...
    -> Liel_Day4_002_Decline
- ~ increment_counter("Liel_Day4_002_meet")
-> DONE

=== Liel_Day4_002_Accept ===
비가 올 것 같군요. #speak:Liel:리엘
- 사실 저는 비 오는 날을 그리 좋아하지 않습니다.
- 그날도 비가 왔거든요.
~ acquire_memory("liel_hates_rain")
~ add_affection("Liel", 2)
~ increment_counter("Liel_Day4_002_meet")
-> DONE

=== Liel_Day4_002_Decline ===
그러시군요. 그럼 다음에 뵙지요. #speak:Liel:리엘
~ increment_counter("Liel_Day4_002_meet")
-> DONE

// ============================================
// Day 4 — 저녁 (19~23시) : 의심 누적 결과 확인
// ============================================
=== Liel_Day4_003 ===
{get_suspicion("Liel") >= 50: ...또 오셨군요. #speak:Liel:리엘}
{get_suspicion("Liel") >= 20 and get_suspicion("Liel") < 50: 밤늦게 무슨 일이십니까. #speak:Liel:리엘}
{get_suspicion("Liel") < 20: 좋은 저녁입니다. #speak:Liel:리엘}
+ {has_memory("liel_hates_rain") and get_counter("Liel_Day4_002_meet") >= 1} 낮에 하신 비 얘기요
    -> Liel_Day4_003_Rain
+ {has_memory("liel_hates_rain") and get_counter("Liel_Day4_002_meet") == 0} 비 오는 날 싫어하신다면서요
    ~ add_suspicion_for("Liel", 35, "Liel_Day4_002_meet")
    -> Liel_Day4_003_HowKnow
+ {has_memory("liel_star_hobby")} 별 보러 안 가세요?
    ~ increment_counter("Liel_told_about_star")
    -> Liel_Day4_003_Star
+ {get_suspicion("Liel") >= 50} ...저를 의심하고 계신가요?
    -> Liel_Day4_003_Confront
+ 그냥 지나가는 길이었어요
    -> Liel_Day4_003_Pass
- ~ increment_counter("Liel_Day4_003_meet")
-> DONE

=== Liel_Day4_003_Rain ===
기억하고 계셨군요. #speak:Liel:리엘
- 그런 얘길 남에게 한 건 오랜만입니다.
~ add_affection("Liel", 3)
~ increment_counter("Liel_Day4_003_meet")
-> DONE

=== Liel_Day4_003_HowKnow ===
...제가 그런 말을 한 적이 있었나요? #speak:Liel:리엘
- 오늘 {player_name} 씨를 뵌 건 아침이 처음인데 말입니다.
~ add_affection("Liel", -3)
~ increment_counter("Liel_Day4_003_meet")
-> DONE

=== Liel_Day4_003_Star ===
{get_counter("Liel_told_about_star") >= 2: 별 얘기를 참 좋아하시는군요. #speak:Liel:리엘}
{get_counter("Liel_told_about_star") < 2: 오늘은 구름이 많아서요. 아쉽게 됐습니다. #speak:Liel:리엘}
~ add_affection("Liel", 1)
~ increment_counter("Liel_Day4_003_meet")
-> DONE

=== Liel_Day4_003_Confront ===
...솔직히 말씀드리면, 그렇습니다. #speak:Liel:리엘
- {player_name} 씨는 제가 말하지 않은 것들을 너무 많이 알고 계십니다.
- 저는 그런 걸 우연이라고 부르지 않습니다.
~ add_affection("Liel", -2)
~ increment_counter("Liel_Day4_003_meet")
-> DONE

=== Liel_Day4_003_Pass ===
그렇습니까. 밤길 조심히 가시지요. #speak:Liel:리엘
~ increment_counter("Liel_Day4_003_meet")
-> DONE

// ============================================
// 실행 횟수 소진 시 공통 대체 대사
// ============================================
=== Liel_Day4_Exhausted ===
오늘은 이야기를 많이 나눈 것 같군요. #speak:Liel:리엘
- 다음에 또 뵙겠습니다.
-> DONE
