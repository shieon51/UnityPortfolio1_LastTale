# ⚔️ Last Märchen (2D Platformer RPG)

<img src="./가온_일러스트.png" width="800">

Last Märchen은 시간 관리와 선택의 누적을 중심으로 전개되는 **2D 액션 RPG 기반의 멀티 엔딩 내러티브 게임**입니다. 
플레이어의 선택, 시간 사용 방식, 이전 회차의 기록이 스토리와 엔딩 분기에 직접적인 영향을 주며, 반복 플레이를 통해 점차 세계의 진실에 접근하도록 설계되었습니다.

### 🎥 프로젝트 시연 영상
[![프로젝트 시연 영상](https://img.youtube.com/vi/J52L2bdI_0Y/0.jpg)](https://www.youtube.com/watch?v=J52L2bdI_0Y)
---

## 🛠️ Tech Highlights (기술적 구현 사항)

### 1. Scalable System Architecture
* **전략 패턴 기반 게임 모드:** `IGameMode` 인터페이스와 전략 패턴을 도입하여 1부(시간 코인제)와 2부(행동력 기반) 등 상이한 규칙을 캡슐화. 기존 로직 수정 없이 신규 모드 확장이 가능한 **OCP(개방-폐쇄 원칙)** 준수 설계
* **데이터 및 메모리 최적화:** `Singleton` 기반 `DataManager`(Repository 패턴)로 데이터 로드 중앙화. `PoolManager`를 통해 구역(Zone) 단위 오브젝트 풀 분리 및 Additive Scene 로딩 결합으로 씬 전환 시 상태 연속성 및 메모리 최적화

### 2. Component-based Combat & Entity System
* **객체 지향 구조:** `CharacterStats` 계층 상속 구조를 통해 '조작(Controller)'과 '데이터(Stat)'를 분리하여 다중 캐릭터 빙의(Possess) 시스템 구현
* **물리/타격감 최적화:** `Physics2D.OverlapBoxAll` 기반 코드 타격 스캔 로직으로 자식 콜라이더 간섭(Bubbling) 문제 해결. 역경직(Hit Stop), 슈퍼아머(Super Armor), 물리 기반 대시 로직을 결합하여 조작 정교함 극대화

### 3. Advanced AI & State Management
* **FSM 기반 동적 AI:** `IState` 인터페이스 활용(Idle/Patrol/Chase/Attack) 모듈화
* **다층적 AI 아키텍처:** 관계 스탯에 반응하는 '일반 모드'와 전투 패턴을 변환하는 '전투 모드'를 유기적으로 전환하는 동적 시스템 구축. Raycast와 Bounding Box를 활용한 **'스마트 지면 안착(Auto-snapping)'** 로직으로 NPC 물리 배치 최적화

### 4. Data-Driven Narrative & Tool Architecture
* **데이터 주도 파이프라인:** Ink 스크립트 엔진과 연동하여 대화 스크립트 내 태그(#anim, #battle 등)를 실시간 파싱. 하드코딩 없이 기획 데이터만으로 인게임 연출 및 보스전 제어
* **인게임 에디터 툴 개발:** 반복적 레벨 디자인을 최소화하는 **'Grid Map Editor'** 직접 구현 및 데이터 직렬화(Serialization)를 통한 이벤트/맵 데이터 관리 프로세스 확립
  
---

## 🕹️ Controls (조작법)

![플레이어 모션](./게임캐릭터_소라모션2.gif)

* **이동:** 좌우 방향키
* **지형 아래로 내려가기:** 아래 방향키
* **점프:** Ctrl 키
* **공격:** Z 키
* **달리기(대시):** Shift 키
* **상호작용:** X 키
---

## 🧩 Game Systems

### Time Coin & Fatigue System
* **Time Coin:** 하루 24개 시간 코인 지급. 모든 행동은 코인을 소모하며, 시간 선택이 곧 스토리 분기의 조건
* **Fatigue:** 활동 시간에 따른 피로도 누적 및 능력 효율 저하, 수면을 통한 차등 회복

### Event & Loop System
* **Time-based Events:** 특정 시간대에만 발생하는 NPC 이벤트 및 위치 변화
* **Loop-based Events:** 회차 재시작 시 이전 회차의 정보/엔딩 반영, 선택지 및 대화 분기 변화

### Save / Load Limitation
* **전략적 세이브:** 세이브 및 시간 회귀를 캐릭터 고유 능력(MP 소모)으로 설정

---

## ℹ️ Development Info
* **개발 기간:** 2025.01.21 ~ 2025.03.09 / 2025.09.04 ~ 2025.09.07 / 2026.01.15 ~ (진행 중) (총 약 52일)
* **담당 영역:** 1인 개발 (기획, 시스템 설계, 구현, 디자인 전반, 아트 리소스/애니메이션 제작)
* **Stack:** C#, Unity, Ink
* **GitHub Repository:** [LastTale Project](https://github.com/shieon51/UnityPortfolio1_LastTale.git)

---

## ✅ 현재 구현된 사항
- 플레이어 기본 이동/대시/연속 공격 및 상태 관리(HP, MP, EXP)
- 시간 코인 및 피로도 시스템 로직
- 시간대별 이벤트 분기 구조 및 몬스터 AI(FSM)
- 씬 이동 포탈 관리 시스템
- 프리팹 자동 로드 및 에디터 편의 기능 등
- NPC 일반모드/전투모드 분류 및 NPC(보스) AI 상태 패턴 기본 완
