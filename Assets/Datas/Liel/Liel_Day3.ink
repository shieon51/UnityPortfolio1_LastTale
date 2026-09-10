=== Liel_Day3_001 ===
#speak:Liel:리엘
{get_counter("Liel_Day3_001_meet") == 0: 이 시간에 무슨 일이십니까?}
{get_counter("Liel_Day3_001_meet") >= 1: 궁금한 게 있습니까?}
 + {not has_memory("liel_sister_exists")} 가족에 대해서
  -> Liel_Day3_001_A
 + {has_memory("liel_sister_exists")} 여동생에 대해서
  -> Liel_Day3_001_B
 + {has_memory("liel_sister_missing")} 여동생의 이름은?
  -> Liel_Day3_001_C
 + {has_memory("liel_sister_exists")} 여동생분은 잘 지내시나요?
    ~ add_suspicion("Liel", 5)      // 은근한 떠보기 — 낮은 위험
    -> Liel_Day3_001_B
 + {has_memory("liel_sister_full_story")} 5년 전에 실종된 거죠?
    ~ add_suspicion("Liel", 30)     // 절대 알 수 없는 걸 정확히 앎 — 높은 위험
    -> Liel_Day3_001_Shock
 + 아무것도 아니예요
- 그렇군요. 밤이 늦었으니 먼저 들어가보시지요.
~ increment_counter("Liel_Day3_001_meet")
-> DONE


=== Liel_Day3_001_A ===
제 가족에 대해 알고 싶으시다고요.
- 제 가족 사진입니다. 옆에는 여동생이고요.
~ acquire_memory("liel_sister_exists")
~ increment_counter("Liel_Day3_001_meet")
-> DONE

=== Liel_Day3_001_B ===
여동생은... 지금은 같이 안 삽니다.
{get_affection("Liel") <= 5: 사정이 있어서 말입니다. ->DONE}
{get_affection("Liel") > 5: 예전에 사고가 좀 있어서, 실종되었거든요.}
~ acquire_memory("liel_sister_missing")
~ increment_counter("Liel_Day3_001_meet")
-> DONE

=== Liel_Day3_001_C ===
여동생 이름은 '홍길동'입니다.
- 동에 번쩍 서에 번쩍 하는 녀석이지요.
~ acquire_memory("liel_sister_full_story")
~ increment_counter("Liel_Day3_001_meet")
-> DONE

=== Liel_Day3_001_Shock ===
#speak:Liel:리엘
{get_suspicion("Liel") >= 50: ...당신, 대체 뭡니까? }
{get_suspicion("Liel") < 50: ...어떻게 그걸 아십니까? }
~ add_affection("Liel", -3)
-> DONE
