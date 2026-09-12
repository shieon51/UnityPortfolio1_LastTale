=== Cutscene_Day4_001 ===
해질 무렵. 인기척 하나 없던 길목이었다. #system
그런데 어딘가에서 발소리가 들려온다. #cue:test_shake #lockinput #auto:1.5
//~ move_npc_rel("Liel", -2, 0, 2.0)
...드디어 나타났군. #speak:Liel:??? #auto:2.5
+ 누구세요?
    -> Cutscene_Day4_001_Who
+ {get_suspicion("Liel") >= 30} ...저를 찾고 계셨나요?
    ~ add_suspicion_for("Liel", 10, "Liel_Day4_001_meet")
    -> Cutscene_Day4_001_Expected
+ （조용히 뒤로 물러난다）
    -> Cutscene_Day4_001_Retreat

=== Cutscene_Day4_001_Who ===
그건 중요하지 않습니다. #speak:Liel:???
다만 한 가지만 묻지요. 당신은... 이 길을 몇 번째 걷고 있습니까?
등골이 서늘해지는 것을 느꼈다. #system
~ increment_counter("cutscene_day4_met_stranger")
-> DONE

=== Cutscene_Day4_001_Expected ===
...역시. #speak:Liel:???
알고 계셨군요. 제가 기다리고 있었다는 것을.
~ add_line_crossed("Liel", 1)
~ increment_counter("cutscene_day4_met_stranger")
-> DONE

=== Cutscene_Day4_001_Retreat ===
발걸음을 돌리려는 순간, 그 목소리가 다시 들려왔다. #system
도망치셔도 좋습니다. 어차피 다시 만나게 될 테니까요. #speak:Liel:??? #auto:2.0
~ increment_counter("cutscene_day4_met_stranger")
-> DONE