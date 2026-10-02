# AI 관제 지휘 게임 — Acceptance Driven Prototype

**Version:** 0.1  
**Target:** Prototype Vertical Slice  
**Primary Goal:** SPEC의 `AC-01 ~ AC-12` 전체 통과  
**Engine:** Unity 6 LTS / URP  
**Target Platform:** Windows PC

## 목차
- [0. 개발 완료 정의](#0-개발-완료-정의)
- [MILESTONE 1 — PLAYABLE FOUNDATION](#milestone-1--playable-foundation)
  - [TASK-001 — Unity Project Foundation](#task-001--unity-project-foundation)
  - [TASK-002 — Mission 01 Blockout](#task-002--mission-01-blockout)
  - [TASK-003 — Field Agent Core](#task-003--field-agent-core)
- [MILESTONE 2 — CCTV OBSERVATION](#milestone-2--cctv-observation)
  - [TASK-004 — CCTV Camera System](#task-004--cctv-camera-system)
  - [TASK-005 — CCTV Visual Pass](#task-005--cctv-visual-pass)
- [MILESTONE 3 — COMMAND CORE](#milestone-3--command-core)
  - [TASK-006 — Command Data Model](#task-006--command-data-model)
  - [TASK-007 — Command Input UI](#task-007--command-input-ui)
  - [TASK-008 — Rule-Based Command Parser](#task-008--rule-based-command-parser)
- [MILESTONE 4 — AMBIGUITY](#milestone-4--ambiguity)
  - [TASK-009 — Command Validation](#task-009--command-validation)
  - [TASK-010 — Clarification Conversation](#task-010--clarification-conversation)
- [MILESTONE 5 — LLM INTEGRATION](#milestone-5--llm-integration)
  - [TASK-011 — LLM Command Interpreter](#task-011--llm-command-interpreter)
- [MILESTONE 6 — DELEGATION](#milestone-6--delegation)
  - [TASK-012 — Analysis System](#task-012--analysis-system)
  - [TASK-013 — Analysis Output Formatting](#task-013--analysis-output-formatting)
- [MILESTONE 7 — VERIFICATION](#milestone-7--verification)
  - [TASK-014 — Information Reliability Model](#task-014--information-reliability-model)
  - [TASK-015 — Evidence Verification](#task-015--evidence-verification)
- [MILESTONE 8 — GAME PRESSURE](#milestone-8--game-pressure)
  - [TASK-016 — Mission Timer](#task-016--mission-timer)
  - [TASK-017 — Guard & Detection](#task-017--guard--detection)
  - [TASK-018 — Mission State](#task-018--mission-state)
- [MILESTONE 9 — VERTICAL SLICE](#milestone-9--vertical-slice)
  - [TASK-019 — Mission 01 Full Scenario](#task-019--mission-01-full-scenario)
- [MILESTONE 10 — UI / UX](#milestone-10--ui--ux)
  - [TASK-020 — Control Room HUD](#task-020--control-room-hud)
  - [TASK-021 — Feedback System](#task-021--feedback-system)
- [MILESTONE 11 — AI LITERACY TRACKING](#milestone-11--ai-literacy-tracking)
  - [TASK-022 — Command Logging](#task-022--command-logging)
  - [TASK-023 — Skill Profile](#task-023--skill-profile)
- [MILESTONE 12 — ART PASS](#milestone-12--art-pass)
  - [TASK-024 — Environment Art](#task-024--environment-art)
  - [TASK-025 — Character Art](#task-025--character-art)
- [MILESTONE 13 — QA](#milestone-13--qa)
  - [TASK-026 — Acceptance Test Suite](#task-026--acceptance-test-suite)
- [Implementation Order](#implementation-order)
- [FIRST PLAYABLE GATE](#first-playable-gate)
- [CORE IDENTITY GATE](#core-identity-gate)
- [RELEASE GATE](#release-gate)
- [최종 개발 원칙](#최종-개발-원칙)

---

## 0. 개발 완료 정의

Prototype은 기능 개수가 아니라 아래 조건을 만족했을 때 완료된 것으로 본다.

| Acceptance | 조건 | 필수 |
|---|---|---|
| AC-01 | CCTV 최소 4개 확인 가능 | P0 |
| AC-02 | 현장 요원에게 자연어 명령 입력 가능 | P0 |
| AC-03 | 자연어 → 구조화 Command 변환 | P0 |
| AC-04 | NPC가 Unity 공간에서 실제 행동 | P0 |
| AC-05 | 모호한 명령에 추가정보 요청 | P0 |
| AC-06 | 분석 시스템에 자연어 분석 요청 | P0 |
| AC-07 | 분석 결과가 실제 게임 정보 제공 | P0 |
| AC-08 | 불완전하거나 잘못된 정보 최소 1개 존재 | P0 |
| AC-09 | 추가 분석으로 정보 검증 가능 | P0 |
| AC-10 | 제한시간 존재 | P0 |
| AC-11 | 성공 / 실패 상태 존재 | P0 |
| AC-12 | Mission 처음부터 끝까지 플레이 가능 | P0 |

**P0가 하나라도 실패하면 Prototype 미완성으로 판단한다.**

---

## MILESTONE 1 — PLAYABLE FOUNDATION

목표:

> LLM 없이도 CCTV를 보면서 NPC에게 구조화된 명령을 내려 움직일 수 있다.

---

### TASK-001 — Unity Project Foundation

**Part:** Programming  
**Priority:** P0  
**Dependency:** 없음

#### Purpose

프로토타입 전체가 올라갈 Unity 기반 구조를 구성한다.

#### Implementation

Scene:

```text
Boot
Mission_01
Result
```

Folder:

```text
Assets/
├── Art/
├── Audio/
├── Materials/
├── Prefabs/
├── Scenes/
├── Scripts/
│   ├── Core/
│   ├── Agent/
│   ├── Command/
│   ├── CCTV/
│   ├── Analysis/
│   ├── Evidence/
│   ├── Mission/
│   └── UI/
├── ScriptableObjects/
└── Tests/
```

기본 Manager:

```text
GameManager
MissionManager
TimeManager
CCTVManager
CommandManager
AgentManager
AnalysisManager
EvidenceManager
UIManager
```

#### Output

Unity 프로젝트 기본 구조.

#### Done

- `Mission_01` 실행 가능
- Console Error 0
- 기본 Manager 초기화 성공
- Play Mode 진입/종료 시 오류 없음

---

### TASK-002 — Mission 01 Blockout

**Part:** Game Design + Art  
**Priority:** P0  
**Dependency:** TASK-001

#### Purpose

모든 핵심 시스템을 테스트할 최소 플레이 공간 제작.

#### Map

```text
Entrance
    │
Main Hall ─ Laboratory
    │
Storage ─ Server Room
```

필수 공간:

```text
Entrance
Main Hall
Laboratory
Storage
Server Room
```

#### Interactive Object

- Door
- Cabinet
- Desk
- USB
- Keycard
- Terminal

#### Character

- Field Agent ×1
- Guard ×1

#### Output

Greybox Level.

#### Done

- 모든 공간 NavMesh 연결
- NPC가 각 공간으로 이동 가능
- CCTV 설치 위치 확보
- Mission 시작→목표→탈출 동선 존재

---

### TASK-003 — Field Agent Core

**Part:** Programming  
**Priority:** P0  
**Dependency:** TASK-002

#### Purpose

플레이어의 명령을 실제 게임 행동으로 실행할 현장 요원 구현.

#### Supported Actions

```text
MOVE
WAIT
INSPECT
PICKUP
OPEN
HIDE
REPORT
PHOTO
```

#### State

```text
Idle
Moving
Interacting
Waiting
Reporting
Hidden
Caught
```

#### Example

```json
{
  "target": "FIELD_AGENT",
  "action": "MOVE",
  "location": "LABORATORY"
}
```

→ Agent가 실제 Laboratory로 이동한다.

#### Done

- MOVE 정상 작동
- 목적지 도달 확인
- WAIT 정상 작동
- Object 상호작용 가능
- 잘못된 Action 입력 시 실행하지 않음

#### Acceptance

**AC-04**

---

## MILESTONE 2 — CCTV OBSERVATION

목표:

> 플레이어가 직접 움직이지 않고 상황실에서 현장을 관찰한다.

---

### TASK-004 — CCTV Camera System

**Part:** Programming  
**Priority:** P0  
**Dependency:** TASK-002

#### Camera

```text
CAM-01 Main Hall
CAM-02 Laboratory
CAM-03 Storage
CAM-04 Server Room
```

각 Camera → RenderTexture → UI RawImage.

#### Required Function

- 4분할 화면
- 개별 Camera 선택
- 선택 Camera 확대
- Camera Name 표시
- Timestamp 표시

#### Done

- 4개의 CCTV가 동시에 서로 다른 위치 표시
- Agent 이동이 CCTV에 실시간 반영
- 각 Camera 확대 가능

#### Acceptance

**AC-01**

---

### TASK-005 — CCTV Visual Pass

**Part:** Art + Programming  
**Priority:** P1  
**Dependency:** TASK-004

#### Effect

- Scanline
- Noise
- 낮은 Saturation
- Timestamp
- Camera ID
- Signal UI
- 약한 Glitch

#### Rule

시각효과가 Gameplay 정보 판독을 방해해서는 안 된다.

#### Done

NPC 및 중요 오브젝트 식별 가능.

---

## MILESTONE 3 — COMMAND CORE

이 프로젝트의 가장 중요한 Milestone.

목표:

> 플레이어의 문장을 **게임에서 실행 가능한 데이터**로 변환한다.

---

### TASK-006 — Command Data Model

**Part:** Programming  
**Priority:** P0  
**Dependency:** TASK-001

#### Command Schema

```csharp
Command
{
    CommandTarget target;

    CommandAction action;

    string objectId;
    string locationId;

    List<string> conditions;
    List<CommandStep> sequence;
    List<string> restrictions;

    string outputFormat;

    float confidence;
}
```

#### Target

```text
FIELD_AGENT
ANALYSIS_SYSTEM
```

#### Done

JSON 또는 Structured Data → Command Object 변환 가능.

#### Acceptance

AC-03의 기반.

---

### TASK-007 — Command Input UI

**Part:** Programming + UI  
**Priority:** P0  
**Dependency:** TASK-006

#### UI

```text
TARGET

[FIELD AGENT ▼]

COMMAND
┌────────────────────────────┐
│                            │
└────────────────────────────┘

[SEND]
```

#### Example

```text
연구실로 이동해서 책상 위 물건을 확인해.
```

#### Required

- Text Input
- Enter / Send
- Command History
- NPC Response

#### Done

사용자가 자유문장 입력 가능.

#### Acceptance

**AC-02**

---

### TASK-008 — Rule-Based Command Parser

**Part:** Programming  
**Priority:** P0  
**Dependency:** TASK-006, TASK-007

#### Purpose

LLM 연결 이전에도 전체 Gameplay를 검증할 수 있게 한다.

지원 예:

```text
"연구실로 이동해"

→ MOVE / LABORATORY


"책상을 확인해"

→ INSPECT / DESK


"USB를 가져와"

→ PICKUP / USB
```

#### Important

Parser와 Executor를 분리한다.

```text
Natural Language
       ↓
Command Parser
       ↓
Structured Command
       ↓
Command Validator
       ↓
Action Executor
```

#### Done

최소 15개 테스트 문장 PASS.

#### Acceptance

**AC-03**
**AC-04**

---

## MILESTONE 4 — AMBIGUITY

여기부터 프로젝트 고유성이 생긴다.

---

### TASK-009 — Command Validation

**Part:** Programming + Game Design  
**Priority:** P0  
**Dependency:** TASK-008

#### Purpose

명령이 수행 가능한지 게임 상태 기준으로 검사한다.

예:

```text
"문 열어."
```

현재 시야에 Door 3개.

→ 실행 금지.

#### Result

```text
AMBIGUOUS_OBJECT
```

NPC:

> 어느 문을 말씀하시는 건가요?

---

#### Validation

- Target 존재 여부
- Object 존재 여부
- Location 존재 여부
- 여러 후보 존재 여부
- Agent가 해당 정보를 알고 있는지
- 현재 실행 가능한 Action인지

#### Done

모호한 Command를 임의 수행하지 않는다.

#### Acceptance

**AC-05**

---

### TASK-010 — Clarification Conversation

**Part:** Programming  
**Priority:** P0  
**Dependency:** TASK-009

#### Flow

```text
PLAYER
"오른쪽 문 열어."

AGENT
"오른쪽에 문이 두 개 있습니다.
연구실 문인가요, 창고 문인가요?"

PLAYER
"연구실."

AGENT
→ 기존 Command Context 복원
→ OPEN / LAB_DOOR
```

#### Required

Pending Command 상태 유지.

#### Done

추가 응답으로 기존 Command 보완 후 실행 가능.

#### Acceptance

**AC-05**

---

## MILESTONE 5 — LLM INTEGRATION

Rule Parser가 정상 작동한 다음 진행한다.

---

### TASK-011 — LLM Command Interpreter

**Part:** Programming / AI  
**Priority:** P0  
**Dependency:** TASK-008~010

#### Purpose

자유로운 문장을 Structured Command로 변환한다.

#### LLM Responsibility

허용:

```text
Natural Language
→ Structured Command
```

금지:

```text
게임 결과 결정
NPC 이동 성공 여부 결정
아이템 존재 여부 생성
경비 상태 생성
Mission 성공 선언
```

#### Example

Input:

```text
경비에게 들키지 않도록
연구실로 이동한 다음
책상 위에 저장장치가 있는지만 확인해.
가져오지는 마.
```

Output:

```json
{
    "target": "FIELD_AGENT",
    "sequence": [
        {
            "action": "MOVE",
            "location": "LABORATORY"
        },
        {
            "action": "INSPECT",
            "object": "STORAGE_DEVICE"
        }
    ],
    "restrictions": [
        "DO_NOT_PICKUP",
        "AVOID_GUARD"
    ]
}
```

#### Fallback

API 실패 시:

```text
COMMAND PROCESSING FAILED
```

게임 자체는 정지하지 않는다.

#### Acceptance

**AC-03**

---

## MILESTONE 6 — DELEGATION

여기서 단순 NPC 조종 게임에서 **AI 활용 게임**으로 넘어간다.

---

### TASK-012 — Analysis System

**Part:** Programming  
**Priority:** P0  
**Dependency:** TASK-006

#### Purpose

플레이어가 직접 자료를 전부 읽지 않고 분석 담당에게 업무를 위임한다.

#### Data Source

Prototype에서는 실제 데이터셋을 미리 제작한다.

예:

```text
CCTV_LOG_01
ACCESS_LOG
EMAIL_01
EMAIL_02
SECURITY_REPORT
RESEARCH_MEMO
```

#### Query Example

```text
22시 이후 연구실에 들어간 사람만 찾아줘.
```

#### Output

```text
22:14 — 김민수
22:37 — 박서연
22:52 — 김민수
```

#### Done

자연어 요청 → 데이터 검색 → 결과 반환.

#### Acceptance

**AC-06**
**AC-07**

---

### TASK-013 — Analysis Output Formatting

**Part:** Programming  
**Priority:** P0  
**Dependency:** TASK-012

#### Support

사용자가 다음을 요구할 수 있다.

```text
시간순
인물별
목록
간단 요약
비교
근거 포함
```

#### Example

```text
22시 이후 출입자를
이름과 시간 기준으로 표로 정리해줘.
```

#### Purpose

실제 AI 실무에서 중요한

> Output Format 지정

능력과 연결.

#### Acceptance

**AC-07**

---

## MILESTONE 7 — VERIFICATION

AI 교육 핵심 Milestone.

---

### TASK-014 — Information Reliability Model

**Part:** Game Design + Programming  
**Priority:** P0  
**Dependency:** TASK-012

#### Information State

```text
CONFIRMED
LIKELY
UNVERIFIED
CONFLICTED
```

Prototype에서는 최소 하나의 오해 가능 정보를 의도적으로 배치한다.

예:

분석:

> USB가 B-05 서버실에 있는 것으로 추정됩니다.

하지만 실제 근거:

```text
3일 전 작성된 문서 1건
```

따라서 플레이어가 바로 믿으면 잘못된 판단 가능.

#### Acceptance

**AC-08**

---

### TASK-015 — Evidence Verification

**Part:** Programming + Game Design  
**Priority:** P0  
**Dependency:** TASK-014

#### Query

```text
"USB가 서버실에 있다는 근거 보여줘."
```

→ Source 반환.

또는:

```text
"이 정보와 충돌하는 자료 찾아."
```

→ 반대 Evidence 반환.

#### Required

분석 결과마다:

```text
Result
Source ID
Reliability
Timestamp
```

보유.

#### Done

잘못된 정보가 추가 분석으로 판별 가능.

#### Acceptance

**AC-09**

---

## MILESTONE 8 — GAME PRESSURE

---

### TASK-016 — Mission Timer

**Part:** Programming  
**Priority:** P0

#### Initial

```text
10:00
```

#### Rule

- Gameplay 중 감소
- Pause 상태 제외
- 00:00 → Mission Fail

#### Acceptance

**AC-10**

---

### TASK-017 — Guard & Detection

**Part:** Programming  
**Priority:** P0  
**Dependency:** TASK-002, TASK-003

#### Guard

간단한 Patrol Route 사용.

```text
Point A
→ Point B
→ Point C
→ Point A
```

Agent Detection:

```text
Distance
+
View Angle
+
Raycast
```

#### Result

Agent 발견:

```text
Caught
```

→ Mission Fail.

#### Acceptance

AC-11의 Failure 조건.

---

### TASK-018 — Mission State

**Part:** Programming  
**Priority:** P0  
**Dependency:** TASK-003, 016, 017

#### State

```text
Briefing
Playing
Success
Failure
Result
```

#### Success

```text
USB acquired
+
Agent returned to EXIT
```

#### Failure

```text
Timer == 0

OR

Agent == Caught

OR

Critical Evidence Destroyed
```

#### Acceptance

**AC-11**

---

## MILESTONE 9 — VERTICAL SLICE

---

### TASK-019 — Mission 01 Full Scenario

**Part:** Game Design  
**Priority:** P0  
**Dependency:** 모든 P0 Core System

#### Mission Flow

```text
Mission Start
↓
시설 상황 파악
↓
CCTV 확인
↓
Agent 이동
↓
정보 부족 발견
↓
Analysis 요청
↓
USB 위치 후보 획득
↓
정보 검증
↓
잘못된 정보 발견
↓
추가 분석
↓
실제 위치 파악
↓
Agent 투입
↓
USB 확보
↓
탈출
↓
Mission Complete
```

#### Mandatory Learning Moment

Mission 안에서 반드시 다음 행동이 한 번씩 발생해야 한다.

```text
관찰
명령
모호한 명령 수정
업무 위임
결과 확인
잘못된 정보 발견
근거 검증
추가 지시
최종 판단
```

#### Acceptance

**AC-12**

---

## MILESTONE 10 — UI / UX

---

### TASK-020 — Control Room HUD

**Part:** UI / Art  
**Priority:** P0

#### Layout

```text
┌────────────────────────────────┐
│ MISSION        TIME       RISK │
├──────────┬──────────┬──────────┤
│ CAM 01   │ CAM 02   │ AGENT    │
├──────────┼──────────┤          │
│ CAM 03   │ CAM 04   │ DATA     │
├──────────┴──────────┴──────────┤
│ TARGET                         │
│ COMMAND                        │
│ >                              │
│                         SEND   │
└────────────────────────────────┘
```

#### Priority

1. CCTV
2. Command
3. Response
4. Mission Info
5. Evidence

#### Done

1920×1080 기준 UI 겹침 없음.

---

### TASK-021 — Feedback System

**Part:** UI + Audio  
**Priority:** P1

Command 상태:

```text
RECEIVED
PROCESSING
EXECUTING
COMPLETED
NEEDS_CLARIFICATION
FAILED
```

각 상태를 텍스트와 SFX로 전달.

색상만으로 상태를 표현하지 않는다.

---

## MILESTONE 11 — AI LITERACY TRACKING

Prototype 완성 후 적용.

---

### TASK-022 — Command Logging

**Part:** Programming  
**Priority:** P1

기록:

```text
Original Prompt
Parsed Command
Clarification Count
Execution Result
Verification Request
Retry Count
Timestamp
```

---

### TASK-023 — Skill Profile

**Part:** Game Design + Programming  
**Priority:** P1

측정:

```text
Goal Definition
Context
Constraint
Task Decomposition
Delegation
Verification
Iteration
```

Mission 중 노출하지 않는다.

Result에서만 보여준다.

---

## MILESTONE 12 — ART PASS

---

### TASK-024 — Environment Art

**Part:** Art  
**Priority:** P1

Style:

```text
Low Poly
Industrial
Security Facility
Dark / Desaturated
```

제작:

- Corridor
- Laboratory
- Server
- Desk
- Cabinet
- Door
- CCTV
- Boxes
- Terminal

고해상도 Asset 제작 금지.

---

### TASK-025 — Character Art

**Part:** Art  
**Priority:** P1

Character:

```text
Agent
Guard
```

CCTV에서 실루엣 구분을 최우선으로 한다.

얼굴 표현은 불필요.

Animation:

```text
Idle
Walk
Run
Inspect
Pickup
Open
Hide
Caught
```

---

## MILESTONE 13 — QA

---

### TASK-026 — Acceptance Test Suite

**Part:** QA / Programming  
**Priority:** P0

#### AT-01 CCTV

**Given**

Mission 시작.

**When**

4분할 CCTV 확인.

**Then**

서로 다른 4개 영역이 실시간 표시.

→ AC-01 PASS

---

#### AT-02 Natural Language

입력:

```text
연구실로 이동해.
```

NPC 실행.

→ AC-02 PASS

---

#### AT-03 Parsing

Natural Language 입력 후 Debug Command 확인.

```text
Action = MOVE
Location = LABORATORY
```

→ AC-03 PASS

---

#### AT-04 Execution

Command 수행 후 Agent 실제 좌표 변화.

→ AC-04 PASS

---

#### AT-05 Ambiguity

```text
문 열어.
```

문 2개 존재.

NPC가 추가 질문.

→ AC-05 PASS

---

#### AT-06 Analysis

```text
22시 이후 연구실 출입자 찾아줘.
```

Analysis 결과 반환.

→ AC-06 PASS

---

#### AT-07 Useful Result

반환된 정보로 다음 목표를 결정할 수 있음.

→ AC-07 PASS

---

#### AT-08 Incorrect Information

최소 한 분석 결과가 불확실하거나 잘못된 결론 포함.

→ AC-08 PASS

---

#### AT-09 Verification

플레이어가 근거 또는 반대 Evidence 요청.

잘못된 결론 발견 가능.

→ AC-09 PASS

---

#### AT-10 Timer

10분 → 0분.

0분에서 Failure.

→ AC-10 PASS

---

#### AT-11 Result

USB 확보 + 탈출:

```text
SUCCESS
```

Agent 체포:

```text
FAILURE
```

→ AC-11 PASS

---

#### AT-12 E2E

새 게임 시작.

외부 Debug Command 없이:

```text
Briefing
→ Play
→ Analysis
→ Verification
→ USB
→ Escape
→ Result
```

전 과정 진행.

→ AC-12 PASS

---

## Implementation Order

실제 개발은 아래 순서를 변경하지 않는 것을 권장한다.

```text
TASK-001
↓
TASK-002
↓
TASK-003
↓
TASK-004
↓
TASK-006
↓
TASK-007
↓
TASK-008
↓
TASK-009
↓
TASK-010
↓
━━━━ FIRST PLAYABLE ━━━━

TASK-012
↓
TASK-013
↓
TASK-014
↓
TASK-015
↓
━━━━ CORE IDENTITY ━━━━

TASK-016
↓
TASK-017
↓
TASK-018
↓
TASK-019
↓
TASK-020
↓
TASK-026
↓
━━━━ ACCEPTANCE BUILD ━━━━

TASK-005
TASK-011
TASK-021
TASK-022
TASK-023
TASK-024
TASK-025
↓
━━━━ POLISH ━━━━
```

---

## FIRST PLAYABLE GATE

다음이 작동하기 전에는 아트 작업을 확대하지 않는다.

```text
CCTV 4개
+
NPC 1명
+
자연어 입력
+
Command Parsing
+
실제 NPC 이동
+
모호한 명령 재질문
```

이 단계에서 반드시 플레이 테스트한다.

핵심 질문:

> **말로 사람에게 지시해서 상황을 해결하는 것 자체가 재미있는가?**

NO라면 이후 콘텐츠 개발을 중단하고 Core Interaction부터 수정한다.

---

## CORE IDENTITY GATE

다음 기능이 모두 존재해야 한다.

```text
Delegation
+
Analysis
+
Incomplete Information
+
Verification
+
Iteration
```

핵심 질문:

> **이 게임을 잘하는 행동이 실제 AI를 잘 사용하는 사고과정과 연결되는가?**

NO라면 단순 자연어 NPC 게임이므로 Analysis / Verification 설계를 수정한다.

---

## RELEASE GATE

```text
AC-01 PASS
AC-02 PASS
AC-03 PASS
AC-04 PASS
AC-05 PASS
AC-06 PASS
AC-07 PASS
AC-08 PASS
AC-09 PASS
AC-10 PASS
AC-11 PASS
AC-12 PASS

Console Error = 0
Critical Bug = 0
Mission E2E Completion = PASS
```

위 조건을 모두 만족했을 때만 Prototype v0.1을 완료 처리한다.

---

## 최종 개발 원칙

**1. LLM보다 게임 규칙이 우선한다.**

LLM은 자연어 해석만 담당한다.

**2. 플레이어의 문장을 AI가 알아서 좋게 해석해주지 않는다.**

모호한 지시는 실제로 불리해야 한다.

**3. 모든 것을 구체적으로 작성하는 것이 항상 정답은 아니다.**

필요한 정보만 정확히 전달하는 것이 좋은 명령이어야 한다.

**4. 검증하지 않은 AI 결과에는 리스크가 존재한다.**

**5. 교육용 팝업으로 AI 사용법을 가르치지 않는다.**

게임을 잘하기 위해 필요한 행동 자체가 학습 대상이어야 한다.

**6. Acceptance를 만족하지 않는 기능은 완성된 것으로 보지 않는다.**

**7. P1보다 미통과 P0 Acceptance 수정이 항상 우선이다.**

---

## 구현 및 Acceptance 실행 기록 — 2026-10-02

### SPEC 기준 구현 보완

- TASK-006의 `sequence`는 TASK-011 JSON 예제 및 SPEC §27에 맞춰 `CommandStep` 객체 배열을 표준으로 사용한다. `object`/`location` 별칭은 JSON 입력에서 `objectId`/`locationId`로 정규화한다.
- SPEC §29의 선택 사항 표기와 관계없이 자연어 분석·신뢰도·근거 검증은 AC-06~09에 따라 필수로 구현한다.
- TASK-011은 외부 LLM 해석기 어댑터·스키마 검증·오류 격리를 구현한다. 자격 증명이 없는 실제 외부 API 호출은 검증하지 않은 것으로 기록하며, 규칙 해석기의 AC-03 검증과 구분한다.
- TASK-019 학습 행동 9개는 완주 acceptance 경로에서 모두 시연·검증한다. 명확한 명령을 사용하는 모든 플레이에 의도적인 모호함을 강요하지 않는다.
- TASK-018 증거 파괴 실패는 서버실 증거 보관 터미널의 `OPEN/PURGE`로 재현한다. `INSPECT`는 파괴 경고를 반환하며 `DO_NOT_DESTROY` 제약은 실행기에서도 적용한다.
- TASK-026은 실제 Unity 실행의 NUnit XML, 카메라 픽셀, Transform/NavMesh 이동, uGUI 제출, 활성 경비를 유지한 완주로 검사한다. 독립 C# 검사만으로 AC-01/04/12를 PASS 처리하지 않는다.

### 루프 0 — 초기 상태

프로젝트에는 TASK/SPEC 문서만 존재했다. AC-01~12는 구현 전 상태였다. Unity 6.3.11f1 프로젝트·URP·uGUI 기반을 새로 구현한다.

### 루프 1 — 초기화/통합 검증

Unity batch 실행은 exit 198 (`No valid Unity Editor license found`)로 실패했다. 이 실패는 acceptance 미실행이며 gameplay PASS로 간주하지 않는다. 사용자에게 Unity Hub 라이선스 활성화를 요청하고 구현과 독립 C# 검증을 계속한다.

독립 컴파일에서 명령 `Steps`의 조건식이 구형 Mono 컴파일러에서 CS0173으로 실패했다. SPEC의 순차 행동 의미를 유지하면서 명시적 `IEnumerable<CommandStep>` 변환을 추가했다. 재검증 결과는 `TestResults/`에 기록한다.

당시 완료 판정: **검증 진행 중**. 실행하지 않은 Unity acceptance 및 외부 LLM 실호출은 PASS로 기록하지 않는다.

### 루프 2 — 도메인/통합 보완

SPEC §10/14/15/16/27을 대조하여 다음 실행 조건을 추가했다.

- 이전 NavMesh·EventSystem이 다음 미션에 남지 않도록 재시작 전에 내비게이션을 해제하고 이전 게임 루트를 비활성화한다.
- 이동·대기·연속 명령은 일시정지에서 중단 대신 정지 후 재개한다. 성공 시 마지막 탈출 명령의 완료 기록을 보존한다.
- 현장 조건은 지원하는 `GUARD_CLEAR`만 실행하며, 알 수 없는 조건은 조용히 무시하지 않고 거절한다.
- 증거 사진은 실제 임시 카메라의 렌더 완료 후 PNG로 저장한다. 저장 실패에는 사진 증거를 생성하지 않는다.
- 실제 Unity가 해석한 Unity 6.3 패키지에 맞춰 URP 17.3.0을 사용한다.

도메인 사례와 전체 runtime/editor/test 컴파일은 통과했다. 정확한 최신 사례 수/소스 해시는 `TestResults/domain-results.json`을 기준으로 한다. Unity 재시도는 라이선스 IPC 재연결 시간 초과로 종료했으며, 실제 카메라·NavMesh·미션 완주를 PASS로 판정하지 않았다. 런너는 매 실행의 로그/XML을 `TestResults/runs/`에 보존하고 라이선스 오류/실행 시간 초과를 명확히 반환한다.

### 루프 3 — 최종 요구사항 대조

SPEC §8/15/27의 조건부 실행을 다시 대조했다. `GUARD_CLEAR`가 이동 명령에서만 확인되어 대기·보고·숨기 명령이 조건을 먼저 충족하지 않고 실행되는 누락을 발견했다. 모든 현장 step 실행 전에 경비 시야 조건을 확인하고, 일시정지에서는 조건 대기 시간도 멈추도록 보완한다. 경비가 계속 시야를 확보하면 유한 시간 후 실패를 반환하는 PlayMode 사례를 추가한다.

또한 AC-07 검사에서 제작 데이터의 전체 날짜·시간(`2026-10-02 22:14`)을 시간 문자열(`22:14`)과 직접 비교하는 assertion 오류를 발견했다. SPEC §14의 출처 날짜를 데이터에 유지하고, 검사 쪽에서 시간 부분을 추출하도록 수정한다.

SPEC §7.3의 자료·키워드·인물 범위 지정도 재검토했다. 명시한 `EMAIL_02` 요약이 일반 USB 추정 분기로 빠지고, `연구실 기록`이 미등록 인물 질의로 오인되는 문제를 발견했다. 문서/키워드의 명시 범위를 일반 추정보다 먼저 적용하고, 실제 방 이름을 인물로 추측하지 않도록 수정한다. 인물 지정 문서 비교도 해당 인물의 자료만 비교해야 한다.

SPEC §7.4/10 및 TASK-022에 따라 다음 기록 누락도 수정한다. 증거 패널은 동일 ID 자료의 신뢰도 갱신을 표시해야 한다. 분석 명령이 사이에 실행되어도 원래 추가 질의 기록을 갱신해야 하며, 미션 종료로 취소된 실행은 실패 결과를 보존한다. 주변 보고·책상 조사도 실제 현장 상태에서 작성한 증거로 저장하고, 이미 회수한 키카드를 책상에 남아 있다고 보고하지 않는다.

SPEC §8/27의 예외와 순서 보존을 위해 영어 `don't open/destroy … and report …`에서 금지 절 제거가 후속 보고까지 삼키는 파서 경계를 수정한다. TASK-026 검증 런너는 누락·skip·inconclusive가 있는 suite를 전체 통과로 판정하지 않으며, 경비 판정은 거리 밖·시야 뒤·벽 차폐의 음성 사례도 실제 PlayMode에서 검사한다.

외부 명령 해석 중 아직 파싱 결과가 없을 때도 TASK-022의 담당자 기록은 요청 metadata로 보존한다. 같은 문장을 두 담당자에 동시 제출했을 때 서로의 처리 기록을 덮어쓰지 않아야 한다. TASK-021의 6개 상태에 각각 짧은 음향 신호를 연결한다.

수정 후 전체 C# 어셈블리 컴파일은 오류 없이 통과했다. 독립 도메인 실행은 87개 중 86개 통과, 실패 0, 네이티브 엔진 미실행 1이다. 실제 PlayMode regression은 총 18개로 확장했다. 마지막 `Run-Acceptance.ps1 -Build -TimeoutSeconds 180` 재시도도 `Licensing initialization failed after 74.81s` 및 `LicenseClient IPC connection refused`로 준비 단계에서 중단됐다. 기록: `TestResults/runs/20261002-032525/prepare.log`. 이번 실행은 실제 EditMode/PlayMode 검사와 Windows 빌드까지 진행하지 못했다.

TASK-005 / SPEC §23의 CCTV 표현에 낮은 채도·약한 잡음·subpixel 색상 분리·좁은 간헐적 glitch를 추가한다. TASK-025는 실제 이동 속도에 따른 Walk/Run과 Idle/Inspect/Pickup/Open/Hide/Caught의 구분 가능한 절차적 포즈를 구현한다. 두 항목의 시각 결과는 실제 Unity 실행 후 확인한다.

### 루프 4 — 실제 Unity 검사 및 수정

라이선스 활성화 후 실행 `20261002-222209`의 프로젝트 준비가 성공했고, 실제 Unity EditMode는 **87/87 통과**했다. PlayMode는 **18개 중 16 통과 / 2 실패**다. 활성 경비를 유지한 USB 회수·탈출·결과 전환, 실제 사진, CCTV 픽셀, 분석·추가 질의, 실패 분기는 통과했다.

- AC-04 이동 검사는 문 상호작용으로 요원이 문 앞까지 이미 이동한 뒤에도 고정 8m 이동을 요구했다(실제 6.6978m). SPEC §7.2/AC-04는 지정 목적지까지 실제 이동을 요구하므로 시작 위치와 실제 목적지 사이 거리를 기준으로 검사하고 최종 위치 검사도 유지한다.
- AC-10 일시정지 중 실제 NavMesh 요원 위치가 0.7367m 변했다. SPEC §15 / TASK-016의 일시정지 규칙에 따라 Pause 전환 때 내비게이션을 즉시 정지하고 속도를 0으로 설정하되 진행 중 경로를 보존한다. Resume에서 경로를 재개하며, 포즈·경비도 일시정지와 함께 멈춘다. 이 실패는 검사 기준을 완화하지 않고 런타임에서 수정한다.

batchmode의 `ScreenCapture.CaptureScreenshot`은 실제 HUD PNG를 남기지 않았다. TASK-026의 시각 검토를 위해 검사에서 실제 HUD Canvas를 임시 카메라/RenderTexture로 렌더하고, 프레임 렌더 완료 후 픽셀과 파일을 검사한다. 캡처 후 원래 Canvas 설정을 복구하며 게임 규칙은 바꾸지 않는다.

수정 후 실제 Unity 전체 suite와 Windows 빌드를 다시 실행한다.

### 루프 5 — 실제 화면 셰이더 오류

재실행 `20261002-223110`은 EditMode **87/87 통과**, PlayMode **17/18 통과**다. 루프 4의 실제 이동·일시정지 실패는 통과했다. 새 HUD 카메라 캡처에서 `ControlRoom/CCTVFeed`가 분홍 오류 화면을 표시했고, D3D11 컴파일 로그는 `unexpected token 'point'`를 반환했다. `point`는 HLSL 예약어이므로 잡음 함수 매개변수를 변경한다. SPEC §7.1/23 및 AC-01에 따라 실제 HUD 캡처에 분홍 오류 픽셀이 광범위하게 남아 있으면 실패하도록 검사한다. Console 오류를 허용하지 않는다.

실제 현장 사진은 생성됐지만 프레이밍은 추가 검토한다. 사진의 대상 식별성도 실제 PNG에서 확인한다.

사진 카메라에서만 요원 시각 layer를 제외하고 대상의 실제 활성 bounds 전체에 여백을 주어 촬영하도록 수정했다. CCTV 카메라는 계속 요원을 표시한다. 실제 사진과 HUD PNG는 실행별 결과 폴더에 보존한다.

빌드 뒤에는 실제 Windows 실행 파일을 별도 시작하여 Boot → Mission_01 초기화, CCTV 4개와 NavMesh 준비 및 오류 없는 실행 로그를 확인한다. batch 실행의 초기화 로그로 확인하며, 사람이 평가할 재미와 외부 LLM 실서비스 검증과는 구분한다.

### 루프 6 — 통과 결과 및 사진 안내 표시 정리

실행 `20261002-224213`은 실제 EditMode **87/87 통과**, PlayMode **18/18 통과**, Windows 빌드 및 실제 실행 파일의 Boot/Mission_01 초기화 검사까지 통과했다. HUD PNG에서 분홍 오류 화면이 사라졌고 파란 요원·주황 경비가 구별된다. 사진에 캐비닛 전체가 들어오지만 다른 방의 바닥 안내 글씨가 깊이 차폐 없이 겹쳐 보였다. SPEC §12/23의 현장 정보 판독에 맞게 관제용 방 안내 표시를 별도 layer로 분리하여 PHOTO에서만 제외하고, CCTV에는 유지한다. 이 시각 수정 후 동일 전체 검사·빌드·실행 파일 초기화를 다시 검증한다.

최종 실행 `20261002-224815`도 실제 EditMode **87/87 통과**, PlayMode **18/18 통과**(실패·skip·inconclusive 0), Windows 빌드 및 실제 실행 파일 초기화 **PASS**다. 최종 HUD/사진/결과 PNG를 검토했고 사진의 잘못 겹친 방 안내가 제거됐다. `ACCEPTANCE.md`의 AC-01~12를 실제 결과에 근거해 PASS로 갱신한다.

### 현재 검증 집계

- 설치된 Unity API에 대한 Runtime / Editor / EditMode / PlayMode 전체 소스 컴파일: PASS (오류 0).
- 독립 NUnit: **87개 발견 / 86개 PASS / 실패 0 / 네이티브 엔진 미실행 1**.
- 실제 Unity EditMode: **87/87 PASS**.
- 실제 Unity PlayMode acceptance: **18/18 PASS**, 실패·skip·inconclusive 0.
- Windows 빌드 및 실제 실행 파일 Boot/Mission_01 초기화: **PASS**.
- 실제 외부 LLM 서비스 호출: **NOT RUN** (연결 설정/자격 증명 미제공).
- AC-01~12 자동 acceptance / 검증한 미션 경로의 Console Error 0 / Mission E2E Completion: **PASS**. 프로토타입 자동 Release Gate 통과.
- 외부 서비스 실호출, 실제 Enter/IME·물리 입력·음향 체감, F01~F06의 사람 플레이 평가는 별도 미검증 범위로 유지한다.

실제 구현은 `Assets/Scripts/`, 재현 가능한 검사/빌드는 `scripts/`, 결과의 세부 구분은 `ACCEPTANCE.md`에 있다. 최초 실행 기준 시각은 최신 제작 문서(23:02)가 이미 존재하는 23:05로 설정하여 미래 출처가 먼저 노출되지 않게 했다(SPEC §14). 10분 카운트다운은 그대로 유지한다.
