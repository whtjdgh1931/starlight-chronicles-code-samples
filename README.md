# Starlight Chronicles

[C# 소스 (300개)](Source/Assets) · [게임 실행](#게임-실행) · [Unity 데모 실행](#unity-데모-실행)

공개 소스는 원본의 게임 C# 파일을 폴더 구조와 함께 담았습니다. 운영 시트 식별자가 들어간 Editor 업로더는 제외했습니다. 공개 C# 소스에서 구현을 살펴보고, 게임 빌드와 Unity 데모를 각각 실행할 수 있습니다.

**조성호 · Unity / C# 기반 Windows 2D 액션 RPG · 1인 개발**

몬스터를 사냥해 얻은 장비로 캐릭터를 성장시키고, 혼자 또는 다른 플레이어와 보스전에 도전하는 게임입니다. 캐릭터와 전투, 외부 서비스 연동, 저장 복구, 개발용 검증 도구를 구현했습니다. 메이플스토리의 이동과 성장 구조를 참고해 시작한 학습 프로젝트이며, 저장소 이름은 `copyMaplestory`입니다.

[핵심 구현](#핵심-구현) · [AI 활용과 검증 도구](#ai-활용과-검증-도구) · [검증 기록](#검증-기록)


| 항목 | 내용 |
| --- | --- |
| 개발 기간 | 2026.05.22 ~ 진행 중 (저장소 최초 커밋 기준) |
| 개발 환경 | Unity 6000.4.8f1 · C# · Windows PC |
| 문서 기준 | 2026.11.22 · `network` 브랜치 · `0a9fe0f` |

## 게임 실행

[Windows ZIP 다운로드](https://github.com/whtjdgh1931/starlight-chronicles-code-samples/releases/download/portfolio-demo-v1/Starlight-Chronicles-Windows.zip) · [릴리스 portfolio-demo-v1](https://github.com/whtjdgh1931/starlight-chronicles-code-samples/releases/tag/portfolio-demo-v1)

1. ZIP 파일 전체를 압축 해제합니다.
2. 압축을 푼 폴더에서 `Starlight Chronicles.exe`를 실행합니다.

외부 리소스와 글꼴 라이선스 고지는 ZIP의 `THIRD-PARTY-NOTICES.txt`를 참고하세요.

[AI 활용 포트폴리오](https://github.com/whtjdgh1931/starlight-chronicles-ai-portfolio)

## 핵심 구현

### 인증·Lobby·Relay 연결과 참가 취소

Unity Authentication으로 익명 인증한 뒤 Lobby에서 방에 참가하고 Relay 연결 정보를 받습니다. Relay 응답이 돌아오면 취소 여부를 확인한 후 현재 세션에 방을 등록합니다. 기다리는 동안 취소하거나 Relay 참가가 실패하면 이미 참가한 Lobby에서 나가는 정리를 시도합니다. [참가와 취소 처리](Source/Assets/01_Scripts/Network/InternetRoomService.cs)

### Google Sheets 실패 복구와 사냥터 데이터 이관

외부 시트에서 읽은 데이터는 스냅샷으로 검증합니다. 원격 로딩에 실패하면 로컬 캐시를 확인하고, 사용할 수 있는 캐시가 없으면 내장 기본 데이터를 사용합니다. 존재하지 않는 문서 ID로 실제 HTTP 404를 재현한 검사에서는 캐시 유무에 따라 각 복구 경로가 실행됐고, 로딩 완료 이벤트가 각각 한 번 발생했습니다. [데이터 로더](Source/Assets/01_Scripts/Manager/DataManager.cs)
사냥터 입장 레벨과 난이도·구역별 몬스터 생성 규칙을 `HuntDifficulty`, `HuntSpawn` 테이블로 옮겼습니다. 난이도·구역 조합의 누락·중복, 몬스터 ID, 생성 수량과 주기 범위를 검사합니다. 구형 캐시에 빠진 사냥터 테이블은 내장 기본값으로 보완합니다. [스냅샷 검증](Source/Assets/01_Scripts/Data/GameDataSnapshotValidator.cs) · [구형 캐시 변환](Source/Assets/01_Scripts/Data/GameDataSnapshotSerializer.cs)

### 협동 보스전의 서버 판정과 참가자 화면 반응

서버가 보스의 체력과 전투 결과를 결정하고 클라이언트에 전달합니다. 공동 씬 이동은 참가자 전원의 로딩과 준비 응답을 확인한 뒤 전투를 시작합니다. [보스 상태 동기화](Source/Assets/01_Scripts/Network/NetworkBossController.cs) · [공동 씬 이동](Source/Assets/01_Scripts/Network/NetworkBossSceneFlow.cs)

참가자가 기본 공격 키를 누르면 자신의 화면에 준비 자세를 바로 표시합니다. 서버가 승인하면 공격 모션을 재생하고, 피해는 서버에서 판정합니다. 검기 스킬 연출은 서버가 확정한 발사 위치와 방향을 따릅니다. 이동·점프는 참가자 본인의 **최대 위치 보간 설정을 `0.10초 → 0.04초`**로 조정했습니다. 실제 2인 플레이에서 입력부터 화면 반응까지 걸리는 시간은 측정 대상으로 남아 있습니다. [참가자 입력 처리](Source/Assets/01_Scripts/Network/NetworkPlayerController.cs) · [공격 준비 자세](Source/Assets/01_Scripts/Player/PlayerVisual.cs) · [투사체 발사](Source/Assets/01_Scripts/Player/Skill/PlayerProjectileSkillExecutor.cs)

### 캐릭터 저장과 백업 복원

저장할 때 임시 파일에 먼저 기록한 뒤 본 파일을 교체합니다. 본 파일이 손상되면 백업을 읽어 상태를 복원합니다. 백업으로 복원한 직후 쓰기가 실패해도 정상 백업이 보존되는지 확인했습니다. [저장소 코드](Source/Assets/01_Scripts/Save/SaveFileStorage.cs)
## AI 활용과 검증 도구

### 전투음이 겹치는 문제를 재생 정책으로 풀기

여러 참가자가 동시에 공격할 때 다른 캐릭터의 효과음에 본인의 타격음이 묻힐 수 있다고 판단했습니다. 본인의 타격음을 구분할 수 있어야 한다는 기준을 세우고, Codex를 활용해 재생 코드를 검토하고 구현했습니다. 본인과 다른 참가자의 재생 채널을 나누고, 원격 효과음에 볼륨 감쇠와 타격음 재생 간격을 적용했습니다. 채널이 가득 차면 해당 효과음만 생략합니다. 다인 플레이에서의 음량과 간격은 청취 검증 항목입니다. [전투음 재생 코드](Source/Assets/01_Scripts/Audio/CombatSoundPlayer.cs)

### 플레이를 반복하지 않고 퀘스트 저장 실패 재현

보상 수령과 저장 실패를 확인할 때마다 사냥과 수집을 되풀이하는 시간을 줄이고자 Unity Inspector 도구를 만들었습니다. 캐릭터 슬롯과 퀘스트를 선택하면 기존 게임 API로 수락·처치·보상 수령을 요청할 수 있습니다. 저장 쓰기를 한 번 실패시키고 저장만 재시도한 뒤, 전후 스냅샷과 저장 파일을 비교해 퀘스트 상태와 보상 중복 여부를 확인하도록 구성했습니다. `Tools > Debug > Create Quest Tester`에서 추가할 수 있습니다. [Quest Tester 코드](Source/Assets/01_Scripts/Debug/QuestValidation)

개발자는 문제와 목표 동작, 검증 기준을 정했습니다. Codex는 코드 조사, 구현과 수정, 검사 작성에 활용했습니다. 타이틀·보스맵 배경과 일부 몬스터·NPC·투사체·전투 효과 이미지에도 이미지 생성을 사용했습니다. 일부 전투 효과음은 AI의 도움으로 작성한 코드로 로컬에서 합성했습니다.

## 검증 기록

| 기록 | 확인한 범위 |
| --- | --- |
| 11월 19일 · 저장·복구 | 임시 Unity Editor 검사 48건 통과. 본 파일 손상 후 백업 복원, 쓰기 실패 후 정상 백업 보존 등 확인 |
| 11월 20일 · 보스·드롭 | 임시 Unity 검사 65건 통과. 드롭 ID·확률 검증, 실제 프리팹의 예고 방향과 발사 방향 일치, 투사체 풀 재사용 확인 |
| 11월 20일 · HTTP·네트워크 | 실제 HTTP 404 후 캐시·기본 데이터 복구. 로컬 UDP에 지연 120±20ms와 손실률 5%를 설정한 뒤 최종 공격 예고를 147.4ms 후 수신하고 발사점·방향·숨김 확인 |
| 11월 22일 · 참가자 입력 반응 | 격리 프리팹에서 기본 공격 준비 자세의 즉시 표시와 연타 중복 방지, 참가자 본인에 한정한 보간 적용 및 객체 제거 시 기존 값 복원 확인 |

11월 19–20일 기록은 각 문서에 표시된 작업본과 환경에서 실행했습니다. Unity Test Framework 패키지는 설치되어 있으며, 위 기록은 격리 스크립트와 개발 중 실행 결과입니다. 최신 코드의 외부망 2인 보스전과 이탈·재접속, 실제 입력 반응 비교는 후속 검증 범위입니다. 플레이 체크리스트

## Unity 데모 실행

1. 저장소를 내려받은 뒤 Unity Hub에서 [`UnityDemo`](UnityDemo) 폴더를 추가합니다.
2. Unity **6000.4.8f1**에서 `Assets/Scenes/PortfolioDemo.unity`를 열고 **Play**를 누릅니다.
3. **Start boss attack**을 누릅니다. 분홍 예고가 위치를 추적하다가 금색으로 바뀌면 공격 경로가 확정됩니다. **Lane** 버튼으로 경로를 벗어나 피할 수 있습니다.

데모 화면은 Unity 기본 GUI로 구성했습니다. 외부 에셋 구매나 서비스 계정 없이 실행할 수 있습니다.

코드 문서는 원본 C# 발췌입니다. 데모는 보스 예고·경로 확정·회피 흐름을 독립적으로 확인할 수 있게 구성했습니다.

## 외부 리소스 및 라이브러리

외부 에셋의 원본 그래픽·음원·라이브러리는 직접 제작한 부분에 포함하지 않습니다.

| 리소스 | 제작자 | 적용 범위 |
| --- | --- | --- |
| [Hero Editor 4D](https://github.com/hippogamesunity/HeroEditor4DHub/wiki) | Hippo Games | 캐릭터 외형·애니메이션·장비 이미지 |
| [Sunny Land](https://assetstore.unity.com/packages/2d/characters/sunny-land-103349) | Ansimuz | 맵 환경과 배경 소품 |
| [Fantasy Wooden GUI: Free](https://assetstore.unity.com/packages/2d/gui/fantasy-wooden-gui-free-103811) | Black Hammer | UI 패널과 버튼 |
| [Free - Casual & Relaxing Game Music Pack](https://assetstore.unity.com/packages/audio/music/free-casual-relaxing-game-music-pack-262740) | SLD Audio | Happy·Forest·Mystery·Darkness 배경음악 |
| [RPG Essentials SFX – Free!](https://leohpaz.itch.io/rpg-essentials-sfx-free) | Leohpaz | 베기·부활 효과음 설정 |
| [배민 연성체](https://www.woowahan.com/fonts) | 우아한형제들 | 화면 텍스트의 원본 글꼴 |
| [DOTween](https://dotween.demigiant.com/) | Demigiant | UI와 게임 오브젝트 트윈 연출 |
| [Google Sheets To Unity](https://assetstore.unity.com/packages/tools/utilities/google-sheets-to-unity-73410) | Greener Games | Google Sheets 연동 |

`Casual Game Sounds`도 프로젝트에 포함돼 있습니다. 제작자는 Dustyroom이며 동봉된 문서에는 CC0로 명시돼 있습니다. 현재 확인한 게임 씬·프리팹·설정에서는 직접 참조를 찾지 못해 사용 목록과 구분했습니다.

각 리소스의 이용 조건은 원본 배포처와 동봉된 라이선스를 따릅니다. 배민 연성체의 이용 조건은 [공식 폰트 라이선스](https://www.woowahan.com/fonts/license)에서 확인할 수 있습니다.

## 주요 패키지

| 패키지 | 버전 |
| --- | --- |
| Universal Render Pipeline | 17.4.0 |
| Input System | 1.19.0 |
| Cinemachine | 2.10.7 |
| Netcode for GameObjects | 2.13.2 |
| Multiplayer Services | 2.1.3 |
| Unity Transport | 2.7.2 |
| Unity Test Framework | 1.6.0 |

패키지 전체 목록은 manifest.json을 참고하세요.

## 프로젝트 구조

| 경로 | 내용 |
| --- | --- |
| `Assets/00_Scenes` | 타이틀·캐릭터 선택·마을·사냥터·보스·대기방 씬 |
| `Assets/01_Scripts/Player`, `Monster` | 캐릭터와 몬스터 행동·전투 |
| `Assets/01_Scripts/Network` | 접속·씬 이동·전투 동기화 |
| `Assets/01_Scripts/Data`, `Save` | 게임 데이터와 저장·복구 |
| `Assets/01_Scripts/UI`, `Audio` | 화면 표시와 사운드 |
| `Assets/01_Scripts/ObjectPool` | 반복 생성 객체 재사용 |
| `Assets/Config`, `Assets/05_ScriptableObject` | 설정 에셋 |
| `Assets/Art`, `Assets/Effects` | 게임 이미지와 효과 |
| `Assets/99_Assets` | 외부 에셋 |
