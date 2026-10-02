# AI 관제 지휘 / DATA LEAK

Unity 6.3 LTS · URP · Windows 및 데스크톱 브라우저용 자연어 관제 게임 프로토타입입니다. CCTV를 관찰하고 요원과 분석 시스템에 지시하여 10분 안에 기밀 USB를 회수하고 탈출합니다.

- [GitHub Pages / 웹에서 플레이](https://cheonyeon0316.github.io/ai-control-room-game/)
- [Windows 다운로드](https://github.com/cheonyeon0316/ai-control-room-game/releases/latest)
- [공개 저장소](https://github.com/cheonyeon0316/ai-control-room-game)

## 실행

1. 저장소를 clone한 폴더를 Unity Hub에 프로젝트로 추가합니다. Editor `6000.3.11f1`과 활성화된 Unity 라이선스가 필요합니다.
2. 패키지 가져오기가 끝나면 초기 씬·URP 설정이 자동 생성됩니다. 수동 재설정은 **Control Room → Prepare Prototype Scenes**입니다.
3. `Assets/Scenes/Mission_01.unity`를 열고 Play를 누른 뒤 **작전 시작**을 선택합니다. `Boot`도 동일 미션으로 진입합니다.

CCTV 클릭은 확대, **4분할 보기**는 복귀입니다. 하단 담당자에서 `FIELD AGENT` 또는 `ANALYSIS SYSTEM`을 선택하고 한국어/영어 문장을 입력하여 Send 또는 Enter로 보냅니다. 요원은 직접 조작할 수 없습니다. 분석 중에도 시간과 현장은 진행되며, 우측에서 작전 기록·증거·지도를 확인할 수 있습니다.

예시:

```text
현장 요원
메인 복도로 이동해
주변 상황 보고해
문 열어
연구실
연구실로 이동해서 책상을 확인해
5초 동안 대기해
캐비닛 사진 찍어
경비에게 들키지 않게 출입구로 이동해

분석 시스템
22시 이후 연구실 출입자 찾아줘
22시 30분부터 22시 45분까지 연구실 출입자를 표로 정리해줘
USB 위치 알려줘
USB가 서버실에 있다는 근거 보여줘
이 결론과 충돌하는 최신 자료를 찾아 검증해줘
김민수의 출입 기록을 시간순으로 정리해줘
```

USB만 회수하거나 USB 없이 출입구에 도착하면 미션은 끝나지 않습니다. USB 회수 후 탈출해야 성공합니다. 시간 초과, 체포, 핵심 증거 파괴, 목표 달성 불가는 실패입니다. 터미널은 조사 응답의 경고를 확인한 뒤 조작하십시오. 사진은 실제 현장 카메라 이미지로 저장됩니다.

## 검증 / 빌드

PowerShell에서 실행합니다. Unity를 열고 있다면 먼저 닫아 프로젝트 잠금을 해제합니다.

```powershell
# 실제 Unity EditMode + PlayMode acceptance
& .\scripts\Run-Acceptance.ps1

# acceptance 이후 Windows 실행 파일 생성
& .\scripts\Run-Acceptance.ps1 -Build

# 이미 만든 Windows 실행 파일의 실제 Boot / Mission_01 초기화 검사
& .\scripts\Verify-PlayerStartup.ps1

# 라이선스 없이 설치된 Unity API 참조로 전체 C# 소스 컴파일
& .\scripts\Verify-Compilation.ps1

# 실제 순수 도메인 NUnit 사례 실행 (Unity runtime acceptance와 구분)
& .\scripts\Verify-Domain.ps1

# Web Build Support 설치 후 웹 빌드 및 web/play/ 갱신
& .\scripts\Build-Web.ps1
```

Editor 설치 경로가 다르면 Unity 검증 스크립트에 `-EditorPath '경로\Unity.exe'`를 전달합니다. 실제 테스트 XML·로그·CCTV/HUD/현장 사진·보고서는 `TestResults/`에 생성되며 실행별 사본은 `TestResults/runs/`에 보존됩니다. 빌드는 `Builds/ControlRoom/ControlRoom.exe`이며 같은 폴더의 데이터 파일들과 함께 사용합니다. `-Build`는 실제 Windows 실행 파일의 초기화 검사도 수행합니다. 통과 상태는 `ACCEPTANCE.md`와 TASK의 실행 기록을 확인하십시오.

## GitHub Pages 배포

`web/`에 소개 페이지와 `web/play/`의 Unity WebGL 빌드를 함께 게시합니다. `main`에 웹 파일을 push하면 `.github/workflows/pages.yml`이 Pages를 배포합니다. Unity Web Build Support가 필요한 로컬 빌드는 `Build-Web.ps1`로 수행합니다. 압축을 비활성화하여 별도 응답 헤더 설정 없이 GitHub Pages에서 로드합니다.

웹 버전은 데스크톱의 WebGL 2 지원 브라우저를 권장하며 기본 규칙 해석기로 플레이합니다. 외부 LLM 키는 웹 파일에 포함하지 않습니다. 현장 사진은 브라우저 내부 파일 시스템에 저장되고 게임의 **현장 사진** 버튼으로 봅니다. Windows ZIP은 전체 압축을 풀고 `ControlRoom.exe`를 실행합니다.

웹의 상단 **작전 시작**으로 시작하고 게임 아래의 브라우저 입력창에서 담당자와 한국어 지시를 보냅니다. 한글 조합 중 Enter는 전송하지 않습니다. 게임의 기존 명령 입력창도 사용할 수 있습니다. **전체 화면**에서도 브라우저 입력창을 유지합니다.

공개 검사 결과는 경로·로그·라이선스 정보를 제외한 `web/assets/acceptance.json`에 보존합니다. 원본 `TestResults/`와 로컬 빌드·캐시는 Git에 포함하지 않습니다.

## 외부 LLM 해석기

기본 모드는 네트워크 없이 동작하는 규칙 해석기입니다. 외부 서비스는 OpenAI 호환 Chat Completions JSON 프로토콜을 사용하며 자연어를 Command로 변환하는 역할만 맡습니다. 이동·물체·경비·미션 결과는 Unity가 판정합니다.

Editor/실행 파일을 시작하는 프로세스에 다음 환경 변수를 설정하면 **MODE** 버튼으로 연결된 해석기를 선택할 수 있습니다.

```powershell
$env:CONTROLROOM_LLM_ENDPOINT = 'https://your-service.example/v1/chat/completions'
$env:CONTROLROOM_LLM_MODEL = 'your-model'
# API 키는 로컬 환경 변수로 설정하며 파일이나 저장소에 저장하지 않습니다.
# CONTROLROOM_LLM_API_KEY
```

API 시간 초과·연결 실패·잘못된 JSON·허용되지 않은 action·선택된 담당자 변경은 `COMMAND PROCESSING FAILED` 응답으로 끝납니다. 게임 타이머와 현장 실행은 계속됩니다. 외부 서비스의 실제 호출은 해당 환경 설정이 필요하며 규칙 해석기 통과와 별도로 검증합니다.

## 구조

- `Assets/Scripts/Command`: 파서 → 검증 → 추가 질의 → Unity 실행 연결, JSON/LLM 해석기.
- `Assets/Scripts/Agent`, `Mission`, `CCTV`: 실제 시설·NavMesh·경비·촬영·미션 규칙.
- `Assets/Scripts/Analysis`, `Evidence`: 출처가 있는 제작 자료와 실제 필터·교차 검증.
- `Assets/Scripts/UI`: 1920×1080 기준 관제 uGUI와 결과 화면.
- `Assets/Tests`: 순수 로직 EditMode 사례 및 실제 입력·영상·이동·실패·완주의 PlayMode acceptance.
- `ARCHITECTURE.md`: 구현 계약. `TASK.md`/SPEC: 제품 및 검증 기준.

씬은 가벼운 진입점이며 시설·캐릭터·UI·내비게이션을 런타임에 생성합니다. 저폴리 오브젝트와 애니메이션·음향을 코드로 제작합니다. 한글 표시는 SIL Open Font License 1.1의 Noto Sans CJK KR을 포함합니다. 출처와 라이선스는 `Assets/Resources/Fonts/`에 있습니다.
