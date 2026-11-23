# Starlight Chronicles

조성호가 1인 개발한 Windows용 Unity 2D 액션 RPG입니다. 몬스터를 사냥해 얻은 장비로 성장하고, 혼자 또는 다른 플레이어와 보스전에 도전합니다.

| 구분 | 구현 내용 |
| --- | --- |
| 개발 환경 | Unity 6000.4.8f1 · C# · Netcode for GameObjects 2.13.2 |
| 온라인 | Unity Authentication, Lobby, Relay. 참가 취소·실패 시 방 정리 |
| 데이터 | Google Sheets 스냅샷 검증. 원격 로딩 실패 시 로컬 캐시 또는 내장 기본값 사용 |
| 저장 | 임시 파일 기록 후 교체, 손상 시 백업 복원, 보스전 결과 저장 재시도 |
| 개발 도구 | Inspector에서 퀘스트 보상 저장 실패를 재현하고 상태와 저장 파일을 비교 |

## 코드 샘플

- [협동 플레이 입력 반응](01-coop-input.md)
- [보스 투사체 예고](02-boss-telegraph.md)
- [보스전 결과 저장](03-battle-result-save.md)

기존 README의 프로젝트 소개, 핵심 구현과 검증 맥락을 공개용으로 옮겼습니다. 각 문서는 원본 C#에서 기능을 설명하는 부분을 발췌했습니다. 실행에는 원본 프로젝트의 씬과 컴포넌트가 필요합니다.

## Unity에서 플레이

Unity Hub의 **Add**로 [UnityDemo](UnityDemo) 폴더를 등록하고 **Unity 6000.4.8f1**에서 엽니다. Project 창에서 `Assets/Scenes/PortfolioDemo.unity`를 열고 **Play**를 누릅니다.

Start boss attack을 누르고 Lane 버튼으로 이동합니다. 분홍 예고는 현재 위치를 추적하고, 금색으로 바뀌면 경로가 고정됩니다. 고정된 경로에서 벗어나 피할 수 있습니다.

포트폴리오용 기능 데모입니다. 화면 요소는 Unity 기본 도형과 GUI로 그리며, 에셋 구매나 외부 서비스 계정 없이 실행할 수 있습니다.
