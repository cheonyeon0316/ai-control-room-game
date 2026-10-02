# AI 관제 지휘 게임 SPEC v0.1

**문서 상태:** Concept / Prototype Specification  
**목적:** 학교 과제용 프로토타입 개발 및 특허 아이디어 검증  
**장르:** 자연어 기반 관제 퍼즐 / 실시간 전술 커뮤니케이션 / 시뮬레이션  
**플랫폼:** PC  
**엔진:** Unity 6 LTS  
**예상 플레이타임:** 1 Mission 10~15분  
**프로토타입 목표:** 1 Mission 완성

## 목차
- [1. Project Summary](#1-project-summary)
- [2. Project Goal](#2-project-goal)
- [3. Design Pillars](#3-design-pillars)
- [4. Core Fantasy](#4-core-fantasy)
- [5. Core Gameplay Loop](#5-core-gameplay-loop)
- [6. Prototype Mission](#6-prototype-mission)
- [7. Player Resources](#7-player-resources)
- [8. Natural Language Command System](#8-natural-language-command-system)
- [9. Command Quality](#9-command-quality)
- [10. Field Agent Interpretation](#10-field-agent-interpretation)
- [11. Ambiguous Command](#11-ambiguous-command)
- [12. Information Asymmetry](#12-information-asymmetry)
- [13. AI Literacy Mapping](#13-ai-literacy-mapping)
- [14. Verification System](#14-verification-system)
- [15. Resource System](#15-resource-system)
- [16. Fail State](#16-fail-state)
- [17. Success State](#17-success-state)
- [18. AI Skill Analysis](#18-ai-skill-analysis)
- [19. Adaptive Training](#19-adaptive-training)
- [20. Patent Concept](#20-patent-concept)
- [21. Art Direction](#21-art-direction)
- [22. Visual Priority](#22-visual-priority)
- [23. CCTV Presentation](#23-cctv-presentation)
- [24. UI Layout](#24-ui-layout)
- [25. Technology Stack](#25-technology-stack)
- [26. Game Architecture](#26-game-architecture)
- [27. Command Pipeline](#27-command-pipeline)
- [28. Important Technical Rule](#28-important-technical-rule)
- [29. Prototype Scope](#29-prototype-scope)
- [30. Prototype Content](#30-prototype-content)
- [31. Core Fun Validation](#31-core-fun-validation)
- [32. Acceptance Criteria — Prototype](#32-acceptance-criteria-prototype)
- [33. Development Priority](#33-development-priority)
- [34. Main Design Rule](#34-main-design-rule)
- [35. Final Product Identity](#35-final-product-identity)
- [36. Core Statement](#36-core-statement)

---

## 1. Project Summary

### 1.1 한 줄 정의

> CCTV를 통해 현장을 관찰하고, 현장 요원과 분석 시스템에 자연어로 업무를 지시하여 제한시간 안에 사건을 해결하는 관제 지휘 게임.

플레이어는 직접 현장을 조작할 수 없다.

플레이어가 사용할 수 있는 수단은 다음과 같다.

- CCTV 관찰
- 현장 요원에게 명령
- 분석 담당에게 업무 위임
- 확보 정보 확인
- 분석 결과 검증
- 추가 지시
- 최종 판단

게임의 핵심은 캐릭터 조작 능력이 아니라

> **“필요한 결과를 얻기 위해 상대에게 무엇을, 어떻게 요청할 것인가?”**

이다.

---

## 2. Project Goal

본 프로젝트는 생성형 AI 사용법을 직접적으로 설명하는 교육 프로그램이 아니다.

플레이어가 게임을 성공적으로 플레이하기 위해 자연스럽게 다음 능력을 사용하도록 설계한다.

1. 문제 정의
2. 목표 설정
3. 업무 분해
4. 적절한 대상에게 위임
5. 맥락 제공
6. 조건 및 제약 설정
7. 원하는 출력 형태 지정
8. 결과 검증
9. 추가 질문 및 수정
10. 최종 의사결정

해당 능력들은 실제 생성형 AI를 업무에 활용할 때 요구되는 사고과정과 연결된다.

---

## 3. Design Pillars

### 3.1 Observe

플레이어는 모든 정보를 알고 있지 않다.

CCTV, 현장 요원, 분석 결과 등 서로 다른 정보원을 통해 상황을 파악한다.

---

### 3.2 Delegate

플레이어가 직접 모든 일을 할 수 없다.

필요한 작업을 판단하고 사람 또는 시스템에게 업무를 위임해야 한다.

---

### 3.3 Verify

게임에서 제공되는 모든 정보가 완벽하게 정확하지는 않다.

플레이어는 결과의 근거를 확인하고 여러 정보원을 교차 검증해야 한다.

---

### 3.4 Adapt

새로운 정보가 등장하면 기존 계획을 수정해야 한다.

처음부터 완벽한 명령을 내리는 것이 아니라

> 요청 → 결과 → 검증 → 수정

과정을 반복하는 것이 핵심이다.

---

## 4. Core Fantasy

플레이어는 사건 현장에 직접 들어가는 사람이 아니다.

플레이어는 여러 정보를 동시에 바라보며 현장의 인력을 지휘하는

> **상황실 책임자 / Operator**

역할을 맡는다.

플레이어가 느껴야 하는 핵심 감정은 다음과 같다.

- 내가 전체 상황을 통제하고 있다.
- 정보가 부족해서 불안하다.
- 내가 내린 명령 때문에 실제 상황이 변한다.
- 좋은 판단으로 복잡한 상황을 해결했다.
- 흩어진 정보를 연결해 답을 찾아냈다.

---

## 5. Core Gameplay Loop

```text
상황 발생
    ↓
CCTV / 자료 확인
    ↓
현재 필요한 정보 판단
    ↓
업무 분해
    ↓
사람 / 시스템 선택
    ↓
자연어 명령 입력
    ↓
작업 수행
    ↓
결과 반환
    ↓
결과 검증
    ↓
추가 지시 또는 행동 결정
    ↓
현장 상황 변화
    ↓
새로운 문제 발생
```

해당 루프를 Mission 종료까지 반복한다.

---

## 6. Prototype Mission

### 6.1 Mission 01 — DATA LEAK

#### 상황

보안 연구시설에서 중요 데이터가 외부로 유출될 가능성이 확인되었다.

시설 전체가 봉쇄되기까지 남은 시간은 약 10분이다.

플레이어는 상황실에서 현장 요원 한 명을 지휘하여 유출된 저장장치를 찾아 회수해야 한다.

---

### 6.2 Mission Goal

#### Primary

기밀 데이터가 저장된 USB 확보.

#### Secondary

침입자 또는 내부 협력자 식별.

#### Optional

현장 요원이 경비에게 발각되지 않음.

---

## 7. Player Resources

프로토타입에서는 다음 네 가지 수단만 제공한다.

### 7.1 CCTV

총 4개.

#### CAM 01

메인 복도.

#### CAM 02

연구실 입구.

#### CAM 03

창고.

#### CAM 04

서버실.

플레이어는 카메라를 전환하여 현장 상황을 직접 관찰한다.

---

## 7.2 Field Agent

현장에서 실제 행동을 수행하는 NPC.

가능 행동:

- 이동
- 대기
- 조사
- 물체 확인
- 물체 획득
- 문 열기
- 숨기
- 사진 촬영
- 주변 정보 보고

플레이어는 현장 요원을 직접 조작할 수 없다.

---

## 7.3 Analysis System

자료 및 CCTV 기록을 분석한다.

가능 업무 예:

- 특정 시간 CCTV 검색
- 인물 이동 경로 확인
- 문서 비교
- 특정 키워드 탐색
- 정보 요약
- 조건에 따른 정보 필터링
- 정보 간 차이 비교

---

## 7.4 Evidence Database

현재까지 확보된 모든 정보를 저장한다.

예:

- CCTV 스크린샷
- 문서
- 현장 사진
- 요원 보고
- 분석 결과
- 로그

---

## 8. Natural Language Command System

### 8.1 Command Structure

게임은 사용자의 자연어 요청을 다음 요소로 분석한다.

```text
Target
Action
Object
Context
Condition
Priority
Sequence
Exception
Output Format
```

예:

> “22시부터 23시 사이 CAM 2와 CAM 4에서 연구실에 출입한 사람을 시간순으로 정리해.”

분석:

```text
Target:
CCTV 분석 시스템

Action:
검색 및 정리

Source:
CAM 2 / CAM 4

Condition:
22:00~23:00

Target Data:
연구실 출입 인물

Output:
시간순 목록
```

---

## 9. Command Quality

게임은 명령의 품질을 직접 숫자로 보여주지 않는 것을 기본으로 한다.

대신 결과를 통해 피드백한다.

### Poor Command

> CCTV 좀 확인해줘.

결과:

> 관련 영상 47건을 발견했습니다.

플레이어에게 크게 도움되지 않는다.

---

### Better Command

> 22시 이후 2층 CCTV를 확인해줘.

결과:

더 좁은 범위의 자료 제공.

---

### Good Command

> 22시부터 23시 사이 2층 연구실에 출입한 인물의 이름과 시간을 시간순으로 정리해줘.

결과:

플레이에 즉시 활용 가능한 자료 제공.

---

## 10. Field Agent Interpretation

현장 요원은 플레이어 명령을 그대로 실행하는 로봇이 아니다.

다음 정보를 가진다.

- 현재 위치
- 현재 시야
- 주변 오브젝트
- 플레이어와 공유되지 않은 현장 정보
- 위험도
- 행동 가능 여부

---

## 11. Ambiguous Command

다음과 같은 명령은 불완전한 명령으로 판단한다.

> 오른쪽 문으로 들어가.

오른쪽에 문이 두 개 존재할 경우:

#### Personality A

질문한다.

> “오른쪽에 문이 두 개인데 어느 쪽 말씀입니까?”

#### Personality B

가까운 문을 임의 선택한다.

프로토타입에서는 Personality A를 기본으로 사용한다.

---

## 12. Information Asymmetry

플레이어와 NPC가 보는 정보는 다르다.

### Player

- CCTV
- 전체 지도
- 분석자료
- 시간
- 여러 NPC 상태

### Field Agent

- 현장의 세부 물체
- 글씨
- 소리
- CCTV 사각지대
- 좁은 범위의 직접 시야

따라서 플레이어는 상황에 따라 NPC에게 질문해야 한다.

예:

> “문 옆 패널에 무엇이 보여?”

NPC:

> “숫자 버튼이 9개 있고 위쪽에 빨간 램프가 있습니다.”

---

## 13. AI Literacy Mapping

게임 행동과 실제 AI 활용 능력은 다음과 같이 연결한다.

| Game Behavior | AI Skill |
|---|---|
| 현재 목표 판단 | Problem Definition |
| 여러 업무로 분할 | Task Decomposition |
| 적절한 담당자 선택 | Tool Selection |
| 상황 설명 | Context Providing |
| 조건 설정 | Constraint Definition |
| 행동 순서 지정 | Workflow Design |
| 결과 형태 지정 | Output Formatting |
| 결과 근거 요청 | Verification |
| 추가 요청 | Iteration |
| 다른 시스템과 비교 | Cross Validation |

교육적 메시지를 게임 플레이 도중 직접 설명하지 않는다.

게임 시스템 자체가 위 행동을 요구하도록 설계한다.

---

## 14. Verification System

분석 시스템의 결과는 항상 정답이 아니다.

정보는 다음 중 하나의 상태를 가진다.

```text
CONFIRMED
LIKELY
UNVERIFIED
CONFLICTED
```

플레이어는 필요할 경우 추가 분석을 요청한다.

예:

> “B-05에 USB가 있다고 판단한 근거를 보여줘.”

또는:

> “이 결론과 충돌하는 자료가 있는지 찾아줘.”

검증하지 않고 잘못된 정보에 따라 행동하면 게임상 손실이 발생한다.

---

## 15. Resource System

프로토타입에서는 과도한 자원 시스템을 사용하지 않는다.

핵심 자원은 두 가지.

### Time

Mission 제한시간.

예:

`10:00`

모든 분석 및 현장 행동은 시간을 소모한다.

---

### Risk

현장 발각 위험.

위험한 행동이나 잘못된 명령으로 증가한다.

```text
LOW
MEDIUM
HIGH
COMPROMISED
```

Risk가 최대가 되면 경비 행동 패턴이 변화한다.

---

## 16. Fail State

다음 조건 중 하나가 충족되면 Mission 실패.

1. 제한시간 종료
2. Field Agent 체포
3. 잘못된 증거 파괴
4. 핵심 목표 달성 불가

---

## 17. Success State

USB 확보 후 Field Agent 탈출.

Mission 종료 시 결과를 표시한다.

예:

```text
MISSION COMPLETE

TIME
08:42

COMMANDS
17

REWORKED COMMANDS
6

VERIFICATION
78%

INCIDENTS
1
```

---

## 18. AI Skill Analysis

Mission 종료 후 플레이 패턴을 분석할 수 있다.

평가 항목:

- Goal Definition
- Context
- Constraint
- Task Decomposition
- Delegation
- Verification
- Iteration

단, 이것을 게임 플레이 중 점수처럼 노출하지 않는다.

결과 화면에서 간략히 표시한다.

예:

```text
COMMAND PROFILE

목표 설정       ████████░░
조건 설정       █████░░░░░
업무 분해       ███████░░░
결과 검증       ███░░░░░░░

RECOMMENDATION

결과를 받은 뒤
근거를 확인하는 행동이 부족했습니다.
```

---

## 19. Adaptive Training

향후 버전에서는 플레이 데이터를 바탕으로 다음 Mission의 문제를 변경한다.

예:

검증 능력이 낮은 사용자:

→ 서로 충돌하는 분석자료 증가.

조건 설정 능력이 낮은 사용자:

→ 비슷한 대상이 여러 개 등장.

업무 분해 능력이 낮은 사용자:

→ 한 번에 처리하기 어려운 복합 목표 증가.

이를 통해 사용자마다 서로 다른 AI 활용 훈련 상황을 제공한다.

---

## 20. Patent Concept

본 프로젝트에서 고려하는 핵심 특허 방향은 게임 자체가 아니라 다음 시스템이다.

> **사용자의 자연어 업무 지시를 분석하여 목표, 조건, 맥락, 작업 분해 및 검증 행동을 산출하고, 해당 분석 결과에 따라 게임 내 업무 상황 및 정보 구조를 적응적으로 변경하는 AI 활용 능력 훈련 시스템.**

구조:

```text
Natural Language Input
        ↓
Command Structure Analysis
        ↓
Player AI Skill Profile
        ↓
Weakness Detection
        ↓
Scenario Parameter Selection
        ↓
Adaptive Mission Generation
        ↓
Gameplay
        ↓
Re-evaluation
```

※ 실제 출원 가능성은 별도의 선행기술 조사가 필요하다.

---

## 21. Art Direction

### Concept

**Low Poly Industrial + CCTV Surveillance + Control Room**

주요 키워드:

- CCTV
- Security
- Surveillance
- Industrial
- Low Poly
- Dark
- Analog Monitor
- CRT
- Tactical UI
- Minimal

---

## 22. Visual Priority

아트 우선순위:

#### S

- CCTV 화면
- UI
- 경고 연출
- 명령 결과 피드백

#### A

- 시설 구조
- NPC 실루엣
- 주요 Interactive Object

#### B

- 일반 환경 오브젝트

#### C

- 얼굴
- 세부 캐릭터 모델링
- 고해상도 텍스처

---

## 23. CCTV Presentation

CCTV에는 다음 효과를 사용한다.

- Noise
- Scanline
- Low Resolution
- Timestamp
- Camera ID
- Signal Strength
- Light Chromatic Aberration
- Occasional Glitch

과도한 효과로 플레이 정보를 가리지 않는다.

---

## 24. UI Layout

기본 화면:

```text
┌────────────────────────────────────┐
│ MISSION             08:47    RISK  │
├─────────────┬─────────────┬────────┤
│             │             │        │
│   CAM 01    │   CAM 02    │ TEAM   │
│             │             │        │
├─────────────┼─────────────┤        │
│             │             │ DATA   │
│   CAM 03    │   CAM 04    │        │
│             │             │ LOG    │
├─────────────┴─────────────┴────────┤
│ TARGET: FIELD AGENT                │
│ > ______________________________   │
│                             SEND   │
└────────────────────────────────────┘
```

---

## 25. Technology Stack

| Category | Technology |
|---|---|
| Engine | Unity 6 LTS |
| Rendering | URP |
| Language | C# |
| UI | uGUI |
| CCTV | Camera + RenderTexture |
| Environment | Blender |
| UI Design | Figma |
| Texture | Krita / Photoshop |
| Animation | Existing Animation / Mixamo |
| Navigation | Unity NavMesh |
| Natural Language | External LLM API |
| Version Control | Git / GitHub |

---

## 26. Game Architecture

```text
GameManager
│
├── MissionManager
│
├── TimeManager
│
├── RiskManager
│
├── CCTVManager
│
├── CommandManager
│
├── AgentManager
│
├── AnalysisManager
│
├── EvidenceManager
│
└── UIManager
```

---

## 27. Command Pipeline

```text
Player Input
    ↓
CommandManager
    ↓
LLM / Command Parser
    ↓
Structured Command
    ↓
Target Validation
    ↓
Action Execution
    ↓
Game State Change
    ↓
NPC Response
```

권장 내부 데이터:

```text
Command
{
    target
    action
    object
    location
    conditions[]
    sequence[]
    restrictions[]
    outputFormat
}
```

LLM에게 게임 자체의 모든 권한을 주지 않는다.

LLM은 **자연어 → 구조화된 Command 변환**까지만 담당한다.

실제 행동 성공 여부 및 게임 규칙은 Unity가 결정한다.

---

## 28. Important Technical Rule

### LLM은 Game Master가 아니다.

금지:

```text
LLM:
"요원이 성공적으로 서버실에 도착했습니다."
```

Unity 상태와 관계없이 LLM이 결과를 임의 생성해서는 안 된다.

올바른 방식:

```text
Player
↓
LLM
↓
MOVE / SERVER_ROOM / STEALTH
↓
Unity
↓
NavMesh 이동
↓
경비 Detection 판정
↓
결과
```

즉,

> **LLM = Interpreter**

> **Unity = Authority**

구조를 유지한다.

---

## 29. Prototype Scope

### 반드시 구현

- CCTV 4개
- 방 4~6개
- Field Agent 1명
- Guard 1명
- Analysis System 1개
- 자연어 Command Input
- NPC 이동
- 조사
- 증거 3~5개
- Mission Timer
- Mission Success / Fail
- 1개의 완성된 Mission

---

### 가능하면 구현

- Risk System
- CCTV Recording Search
- 자연어 기반 데이터 분석
- 정보 신뢰도
- Mission Result Analysis

---

### 프로토타입에서 제외

- 캐릭터 커스터마이징
- 여러 Mission
- 스토리 캠페인
- 장비 시스템
- Skill Tree
- NPC 관계
- 복잡한 전투
- 절차적 맵 생성
- Multiplayer

---

## 30. Prototype Content

최소 콘텐츠:

```text
Map
1

Rooms
5

CCTV
4

Field Agent
1

Guard
1

Evidence
5

Documents
5~10

Mission
1

Ending
Success / Failure
```

---

## 31. Core Fun Validation

프로토타입에서 반드시 확인해야 할 질문.

#### F01

플레이어가 직접 움직이지 못하는 것이 답답함보다 전략적 재미를 만드는가?

#### F02

자연어로 명령하는 과정 자체가 재미있는가?

#### F03

명령 결과가 예상과 다르게 나왔을 때 다시 수정하는 과정이 재미있는가?

#### F04

CCTV를 오가며 정보를 찾는 것이 긴장감을 만드는가?

#### F05

정보를 분석 시스템에 위임하는 것이 단순한 메뉴 조작 이상으로 느껴지는가?

#### F06

AI 활용 능력과 게임을 잘하는 능력이 실제로 연결되는가?

F06이 실패하면 교육 요소를 다시 설계해야 한다.

---

## 32. Acceptance Criteria — Prototype

프로토타입 완료 조건.

#### AC-01

플레이어는 최소 4개의 CCTV를 확인할 수 있다.

#### AC-02

플레이어는 현장 요원에게 자연어 명령을 입력할 수 있다.

#### AC-03

자연어 명령은 구조화된 행동으로 변환된다.

#### AC-04

NPC가 실제 Unity 공간에서 명령에 따라 행동한다.

#### AC-05

모호한 명령은 NPC가 추가정보를 요청한다.

#### AC-06

플레이어는 분석 시스템에 정보 분석을 요청할 수 있다.

#### AC-07

분석 결과는 게임 진행에 필요한 정보를 제공한다.

#### AC-08

잘못된 또는 불완전한 분석 정보가 최소 1회 존재한다.

#### AC-09

플레이어가 추가 분석을 통해 해당 정보를 검증할 수 있다.

#### AC-10

시간 제한이 존재한다.

#### AC-11

성공과 실패 상태가 존재한다.

#### AC-12

Mission은 처음부터 끝까지 진행 가능하다.

---

## 33. Development Priority

### Phase 1 — Core

1. Map Blockout
2. Agent Navigation
3. CCTV
4. Basic UI
5. Command Input
6. Structured Command Execution

이 단계에서 먼저

> **“말로 NPC를 움직이는 것이 재미있는가?”**

검증한다.

---

### Phase 2 — Delegation

1. Analysis System
2. Evidence Database
3. Document System
4. Command Context
5. Information Verification

이 단계에서

> **“단순 조종이 아니라 업무 위임이 되는가?”**

검증한다.

---

### Phase 3 — Game

1. Timer
2. Guard
3. Risk
4. Mission Objective
5. Failure
6. Success
7. Sound
8. CCTV Effects

이 단계에서 게임성을 완성한다.

---

### Phase 4 — AI Literacy

1. Command Logging
2. Player Behavior Analysis
3. AI Skill Profile
4. Result Screen
5. Adaptive Scenario Prototype

---

## 34. Main Design Rule

본 프로젝트의 모든 기능은 다음 질문을 통과해야 한다.

> **“이 기능이 플레이어에게 더 좋은 명령과 판단을 요구하는가?”**

YES

→ 유지.

NO

→ 우선순위 감소 또는 제거.

---

## 35. Final Product Identity

본 프로젝트는

**AI 사용법을 설명하는 게임이 아니다.**

또한

**NPC를 자연어로 움직이는 단순한 AI 데모도 아니다.**

본 프로젝트의 핵심은

> **제한된 정보와 시간 속에서 복잡한 문제를 정의하고, 다른 존재에게 업무를 위임하고, 돌아온 결과를 검증하며 상황을 통제하는 게임이다.**

플레이어는 게임을 플레이하면서 자연스럽게 생성형 AI 시대에 필요한

**문제 정의 → 업무 분해 → 지시 → 검증 → 반복**

사고방식을 경험한다.

교육은 플레이 목적이 아니라 플레이 과정에서 발생하는 결과다.

---

## 36. Core Statement

> **직접 행동할 수 없는 상황에서, 얼마나 정확하게 생각하고 얼마나 제대로 일을 맡길 수 있는가?**

이 질문이 프로젝트 전체의 핵심이다.