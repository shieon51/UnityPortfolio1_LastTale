=== Liel_Day2_001 ===
심심하십니까. #speak:Liel:리엘
- 할 거 없으시면 저와 대련이라도 하시겠습니까?
 + 네
    ->Liel_Day2_001_A
 + 아니요 
    ->Liel_Day2_001_B


=== Liel_Day2_001_A ===
- 이렇게 흔쾌히 수락하실 줄은 몰랐군요. 알겠습니다. 
- 다만 훈련이 처음이실 것 같으니, 저는 한 손만 쓰도록 하겠습니다.
- 그럼 한 번 해보지요.
- ('훈련 모드'로 전투가 진행됩니다!) #panel #battle:Liel:Liel_Day2_001_BattleWin:Liel_Day2_001_BattleLose
->DONE

=== Liel_Day2_001_B ===
- 하하, 농담입니다. 심각한 표정 짓지 마시지요.
->DONE

=== Liel_Day2_001_BattleWin ===
#speak:Liel:리엘
{get_affection("Liel") >= 5: 제법이군요. 생각보다 재능이 있으신 것 같습니다.}
{get_affection("Liel") < 5: 흠. 제법이군요.}
~ add_affection("Liel", 1)
-> DONE

=== Liel_Day2_001_BattleLose ===
#speak:Liel:리엘
- 체력이 없으시군요. 이쯤에서 그만두겠습니다.
- 다음엔 좀 더 분발해보시죠.
-> DONE