# Acceptance 검증 기록

기준 문서: `AI_관제_지휘_게임_SPEC_v0.1.md` §32 / `TASK.md` TASK-026.

현재 상태: **AC-01~AC-12 자동 acceptance PASS**. 실제 Unity 검사, Windows 빌드, 실행 파일 초기화 검증을 완료했다.

2026-10-02 실행 결과:

- 최종 실행: `TestResults/runs/20261002-233403/` (웹 공통 폰트와 타이머 렌더링 수정 후 재검증).
- 실제 Unity **EditMode 87/87 PASS**, **PlayMode 18/18 PASS**. 실패·skip·inconclusive 0.
- 전체 Runtime(25파일) / Editor / EditMode / PlayMode C# 어셈블리 컴파일: PASS, 오류 0.
- 실제 D3D11 렌더에서 CCTV 셰이더·카메라·HUD·사진을 확인했다. 최종 테스트 경로에서 예기치 않은 Console 오류 0.
- Windows 빌드 PASS: `Builds/ControlRoom/ControlRoom.exe`.
- 실제 Windows 실행 파일의 Boot → Mission_01, 4개 영상, 요원·경비 NavMesh, Briefing 초기화와 오류 없는 초기 실행 PASS. 로그: `TestResults/player-startup.log` 및 최종 실행 폴더의 사본.
- 이전 독립 도메인 검사는 86 PASS / native 미실행 1이었다. 해당 미실행 사례도 이번 실제 EditMode 실행에서 통과했다.

| 기준 | 구현 및 실제 검사 | Unity 결과 |
|---|---|---|
| AC-01 | 4 Camera/RT/RawImage, 고유 픽셀·영역, 확대, 이동 후 영상 변화, 실제 HUD 오류 색 검사 | PASS |
| AC-02 | 한국어 자연어를 InputField + Dropdown + Send의 실제 uGUI 이벤트로 제출 | PASS |
| AC-03 | 한국어/영어 파서, CommandStep 순서, 엄격한 JSON·제약·LLM 응답 schema | PASS |
| AC-04 | 실제 NavMesh·Transform 이동, 8행동, 물체·증거 상태 변경, 실제 촬영·사진 열기 | PASS |
| AC-05 | 두 문 후보 → 재질문 → 기존 OPEN 문맥 복원, 분석 교차 실행에도 기록 보존 | PASS |
| AC-06 | 담당자 선택 → 자연어 분석 → 실제 제작 자료 필터 | PASS |
| AC-07 | 시간·인물·방·카메라 조건, 형식, 출처, 목표 단서 | PASS |
| AC-08 | 3일 전 서버실 위치 추정과 실제 연구실 USB의 불일치 | PASS |
| AC-09 | 근거 날짜 → 최신 반대 자료 → 캐비닛/암호 교차 검증 | PASS |
| AC-10 | 600초, Briefing/Pause/Result 정지, 실제 이동 정지·재개, 시간 초과 실패 | PASS |
| AC-11 | USB+Exit 성공, 체포·시간·실제 터미널 파괴 실패, 경비 거리·시야각·벽 차폐 | PASS |
| AC-12 | 관찰·명령·재질문·위임·검증·회수·탈출·결과, 경비 활성, 재시작 후 실제 입력 | PASS |

### 실패 → 수정 → 재검증

| 실행 | 실제 결과 | 수정 |
|---|---|---|
| `20261002-222209` | EditMode 87 PASS, PlayMode 16 PASS / 2 FAIL | 문 상호작용 뒤의 실제 이동 거리 검사, 즉시 NavMesh Pause |
| `20261002-223110` | EditMode 87 PASS, PlayMode 17 PASS / 1 FAIL | HUD 캡처로 발견한 HLSL 예약어 `point` 오류 |
| `20261002-224213` | EditMode 87 PASS, PlayMode 18 PASS, 빌드·실행 초기화 PASS | 사진 구도 확인 후 관제 안내 글씨 겹침 제거 |
| `20261002-224815` | EditMode 87 PASS, PlayMode 18 PASS, 빌드·실행 초기화 PASS | 최종 동일 suite 재검증 완료 |
| `20261002-232031` | EditMode 87 PASS, PlayMode 18 PASS, 빌드·실행 초기화 PASS | Noto Sans CJK KR 포함 후 재검증 |
| `20261002-233403` | EditMode 87 PASS, PlayMode 18 PASS, 빌드·실행 초기화 PASS | 타이머 실제 mesh 생성·일시정지 표시 검사 추가 후 통과 |

SPEC 기준의 각 수정 요구는 TASK 루프 0~6에 기록되어 있다. 최종 실제 `control-room.png`, `field-photo.png`, `mission-result.png`를 검토하여 CCTV 구분·요원/경비 실루엣·캐비닛 사진·결과 표시를 확인했다.

검증 명령: `scripts/Run-Acceptance.ps1`. 실제 NUnit 결과는 `TestResults/EditMode.xml`과 `PlayMode.xml`입니다. 미실행 결과를 PASS로 표기하지 않습니다.

독립 검사: `scripts/Verify-Compilation.ps1`은 설치된 Unity 어셈블리를 참조하여 runtime/editor/test 코드를 컴파일합니다. `Verify-Domain.ps1`은 실제 도메인 NUnit 테스트를 Mono에서 실행하고 사례별 결과와 소스 해시를 `TestResults/domain-results.json`에 기록합니다. 네이티브 객체가 필요한 사례는 미실행으로 명시합니다.

외부 LLM 실호출: 설정/자격 증명 미제공. 어댑터·스키마 검증과 실제 외부 서비스 성공 호출을 구분합니다.

### 검증 범위 및 재실행

재실행은 `scripts/Run-Acceptance.ps1 -Build`. 각 실행의 XML·로그·실제 PNG를 보존하고 필수 사례 누락·skip·inconclusive가 있으면 실패한다. 이번 통과는 오프라인 규칙 해석기와 실제 Unity 상태/화면을 기준으로 한다.

실제 키보드 Enter·한글 IME 조합, 물리적 마우스 조작, 창 크기 변경과 음향 체감은 별도 사용자 플레이 확인 항목이다. 자동 UI 제출은 실제 uGUI 이벤트를 사용했다. Windows 실행 파일의 전체 수동 완주는 아직 수행하지 않았으며, 전체 미션 자동 완주는 실제 Unity PlayMode에서 검증했다.

F01~F06 재미·학습 체감은 사람의 플레이 평가가 필요합니다. 자동 성공 조건의 통과가 이 평가를 대신하지 않습니다.
