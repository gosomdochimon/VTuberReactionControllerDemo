# Unity VTuber Reaction Controller Demo

Unity 기반 VTuber 리액션 컨트롤러 데모입니다.

외부 이벤트를 `ReactionEvent`로 변환하고, `ReactionController`가 큐 기반으로 VFX, VRM 표정, 투척 연출, UI 로그를 순차 처리합니다.

상세한 포트폴리오 원본은 [PORTFOLIO.md](PORTFOLIO.md)에서 확인할 수 있습니다.

## 기능 영상

아래 MP4는 `QA`에서 검증한 실제 기능 영상입니다.

<p align="center">
  <video controls width="960" src="QA/VTuber_Demo_OBSTest.mp4">
    <a href="QA/VTuber_Demo_OBSTest.mp4">QA 기능 영상 열기</a>
  </video>
</p>

GitHub 등 일부 Markdown 뷰어에서 MP4가 바로 재생되지 않으면 [QA 기능 영상 파일](QA/VTuber_Demo_OBSTest.mp4)을 직접 열 수 있습니다.

영상 구성: `cheer` → `shock` → `gift` → `throw` → `spark` → `bonk`

## 프로젝트 목적

VTuber 방송에서 발생하는 후원·채팅·리액션 이벤트를 Unity 안의 아바타 표정과 VFX 반응으로 변환하는 구조를 검증하기 위한 데모입니다.

핵심 목표는 입력 방식과 캐릭터 반응 실행을 분리하는 것입니다.

```text
Keyboard / UDP JSON / WebSocket JSON / OSC
  -> ReactionEvent
  -> ReactionController.Enqueue(...)
  -> Queue 기반 순차 처리
  -> ReactionDefinition
  -> VFX / VRM 표정 / 모션 / UI 로그
```

실제 방송 플랫폼 API를 직접 연결한 제품이 아니라, 로컬 입력 어댑터와 외부 broadcast payload 매핑을 통해 Unity 쪽 반응 실행 경계를 검증한 프로토타입입니다.

## 주요 기능

- Windows Standalone 앱 실행
- UDP JSON 이벤트를 `ReactionEvent`로 변환
- 로컬 WebSocket JSON 입력 지원
- OSC `/reaction/*` 입력 지원
- `cheer`, `shock`, `gift`, `throw`, `spark`, `bonk` 리액션 처리
- Queue 기반 순차 반응 처리
- VRM 아바타 표정 전환
- Cartoon FX VFX 재생
- `throw` 투척 연출과 hit VFX
- `bonk` 머리·목·상체 모션 반응
- 왼쪽 리액션 목록 및 오른쪽 후원 로그 UI
- 키보드 `1`~`6` 테스트 입력

## 대표 기술 GIF

`QA/VTuber_Demo_OBSTest.mp4`에서 대표 기술 장면을 추출한 GIF입니다.

### 일반 VFX (`spark`)

<p align="center">
  <img src="QA/PortfolioClips/VTuber_Demo_NormalVFX.gif" alt="일반 VFX - Spark reaction" width="640">
</p>

### Throw (`throw`)

<p align="center">
  <img src="QA/PortfolioClips/VTuber_Demo_Throw.gif" alt="Throw projectile and hit VFX" width="640">
</p>

### Bonk (`bonk`)

<p align="center">
  <img src="QA/PortfolioClips/VTuber_Demo_Bonk.gif" alt="Bonk expression and body motion" width="640">
</p>

GIF 원본과 다른 포트폴리오에서 재사용할 수 있는 Markdown 예시는 [QA/PortfolioClips/README.md](QA/PortfolioClips/README.md)에 정리했습니다.

## 주요 구현 기술

### 입력 어댑터와 `ReactionEvent`

각 입력 경로는 `ReactionController`를 직접 제어하지 않고, 공통 이벤트 모델인 `ReactionEvent`를 만들어 동일한 입구로 전달합니다.

| 입력 경로 | 담당 코드 | 역할 |
| --- | --- | --- |
| 키보드 | [`KeyboardReactionSimulator.cs`](Assets/Scripts/ReactionSystem/Input/KeyboardReactionSimulator.cs) | 1~6 키 입력을 테스트 이벤트로 변환 |
| UDP JSON | [`UdpJsonEventReceiver.cs`](Assets/Scripts/ReactionSystem/Input/UdpJsonEventReceiver.cs) | UDP 9000 포트의 JSON 수신 |
| WebSocket JSON | [`WebSocketJsonEventReceiver.cs`](Assets/Scripts/ReactionSystem/Input/WebSocketJsonEventReceiver.cs) | `ws://127.0.0.1:9001/reaction/` 입력 수신 |
| OSC | [`OscReactionReceiver.cs`](Assets/Scripts/ReactionSystem/Input/OscReactionReceiver.cs) | `/reaction/cheer` 같은 주소를 반응 타입으로 변환 |

```text
ReactionEvent
  - Type
  - User
  - Amount
  - Message
```

### 외부 JSON 매핑

방송 payload의 필드명과 구조가 달라도 실행 계층에 원본 JSON을 직접 전달하지 않습니다.

```text
외부 JSON
  -> ReactionEventJsonMapper
  -> ExternalBroadcastEvent
  -> BroadcastReactionMapper
  -> ReactionEvent
```

`selectedReaction`이 있으면 후원 금액보다 우선하고, 선택 값이 없을 때만 이벤트 종류와 금액으로 `gift` 또는 `spark` fallback을 판단합니다.

관련 코드:

- [`ReactionEventJsonMapper.cs`](Assets/Scripts/ReactionSystem/Mapping/ReactionEventJsonMapper.cs)
- [`BroadcastReactionMapper.cs`](Assets/Scripts/ReactionSystem/Mapping/BroadcastReactionMapper.cs)
- [`ExternalBroadcastEvent.cs`](Assets/Scripts/ReactionSystem/Models/ExternalBroadcastEvent.cs)

### Queue 기반 순차 처리

`ReactionController`는 이벤트를 `Queue<ReactionEvent>`에 저장하고 하나씩 처리합니다.

```text
Enqueue(A) -> Enqueue(B) -> Enqueue(C)
        ↓
       A 실행
        ↓
    간격 대기
        ↓
       B 실행
        ↓
    간격 대기
        ↓
       C 실행
```

반응 하나의 실행 순서는 다음과 같습니다.

```text
ReactionDefinition 조회
  -> ReactionStarted 이벤트 발행
  -> VFX 생성
  -> UI/Unity 로그 기록
  -> 표정 적용 시점 판단
  -> throw 연출 대기
  -> bonk 모션 연출 대기
  -> 표정 유지 후 복귀
```

`throw` Coroutine 연출이 끝나기 전에 다음 이벤트를 실행하지 않으므로, 연속 입력에서도 VFX와 표정이 서로 덮어쓰는 문제를 줄였습니다.

관련 코드: [`ReactionController.cs`](Assets/Scripts/ReactionSystem/Controller/ReactionController.cs)

### `ReactionDefinition` ScriptableObject 데이터화

반응별 VFX·표정·특수 연출 설정을 `ReactionDefinition` ScriptableObject로 분리했습니다.

```text
ReactionDefinition
  - type / displayName / description
  - vfxPrefab / offset / lifetime
  - preset 또는 custom expression
  - expression timing
  - playThrowReaction
  - playMotionReaction
```

현재 반응 asset:

```text
Assets/Data/Reactions/
  CheerReactionDefinition.asset
  ShockReactionDefinition.asset
  GiftReactionDefinition.asset
  ThrowReactionDefinition.asset
  SparkReactionDefinition.asset
  BonkReactionDefinition.asset
```

`spark`는 `ReactionController` 코드를 수정하지 않고 Definition asset과 Catalog 데이터만 연결해 추가한 확장 검증 사례입니다. `throw`도 문자열 분기보다 `playThrowReaction` 데이터가 실행 여부를 소유하도록 정리했습니다.

관련 코드: [`ReactionDefinition.cs`](Assets/Scripts/ReactionSystem/Definitions/ReactionDefinition.cs), [`ReactionCatalog.cs`](Assets/Scripts/ReactionSystem/Definitions/ReactionCatalog.cs)

### Throw 투사체 연출

[`ThrowReactionPlayer.cs`](Assets/Scripts/ReactionSystem/Effects/ThrowReactionPlayer.cs)는 투사체 연출만 담당합니다.

```text
시작 위치 계산
  -> projectile 생성
  -> Lerp로 hit point까지 이동
  -> sin(t * PI) * arcHeight로 곡선 추가
  -> hit VFX 생성
  -> avatarRoot 짧은 흔들림
  -> projectile 정리
```

Rigidbody 물리 대신 스크립트 기반 궤적을 선택해 시작점·도착점·타이밍을 안정적으로 제어했습니다.

```csharp
basePosition = Vector3.Lerp(start, hit, t);
height = Mathf.Sin(t * Mathf.PI) * arcHeight;
position = basePosition + Vector3.up * height;
```

`Random Start Area`를 연결하면 Sphere 또는 Box 영역 안에서 시작점을 랜덤으로 정할 수 있습니다.

### Bonk 절차형 타격 모션

[`ReactionMotionPlayer.cs`](Assets/Scripts/ReactionSystem/Effects/ReactionMotionPlayer.cs)는 Humanoid Animator의 head·neck·chest bone에 짧은 회전 offset을 적용합니다.

Rigidbody나 ragdoll 대신 `AnimationCurve + spring-damper`를 선택했습니다.

- Animator와 VRM 표정 제어의 충돌을 줄임
- 타격 시간·각도·복귀 속도를 Inspector에서 조절 가능
- 짧고 예측 가능한 방송 연출에 적합
- 현재 자세를 base로 저장해 타격 직전 정자세로 튀는 현상 방지

```text
현재 bone 회전 저장
  -> AnimationCurve로 targetAmount 생성
  -> LateUpdate에서 spring-damper 적용
  -> targetAmount를 0으로 변경
  -> 잔여 속도가 안정화될 때까지 복귀
```

### UI와 Scene 조립 분리

```text
ReactionCatalog
  -> ReactionListPresenter -> 왼쪽 리액션 목록

ReactionController.ReactionStarted
  -> ReactionLogPresenter -> 최신 반응/히스토리 로그

ReactionSystemRoot
  -> Controller·Input·Catalog·Presenter 참조 조립
```

`ReactionCatalog`가 목록 데이터의 원본을 소유하고, `ReactionListPresenter`는 목록을 렌더링합니다. `ReactionLogPresenter`는 `ReactionStarted` 이벤트를 구독해 최대 6개의 로그를 유지합니다.

관련 코드:

- [`ReactionSystemRoot.cs`](Assets/Scripts/ReactionSystem/Core/ReactionSystemRoot.cs)
- [`ReactionListPresenter.cs`](Assets/Scripts/ReactionSystem/UI/ReactionListPresenter.cs)
- [`ReactionLogPresenter.cs`](Assets/Scripts/ReactionSystem/UI/ReactionLogPresenter.cs)

## 구현 결과

| 입력 | Type | Demo behavior |
| --- | --- | --- |
| `1` | `cheer` | Joy 표정 + 별 VFX |
| `2` | `shock` | Surprised 표정 + 전기 VFX |
| `3` | `gift` | Fun 표정 + 불꽃 VFX |
| `4` | `throw` | 큐브 투척 + hit VFX + 피격 표정 |
| `5` | `spark` | Joy 표정 + firework VFX |
| `6` | `bonk` | 즉시 표정 + head/neck/chest 타격 모션 |

## 실행 방법

### Unity Editor

1. Unity로 프로젝트 루트를 엽니다.
2. [`Assets/Scenes/SampleScene.unity`](Assets/Scenes/SampleScene.unity)를 엽니다.
3. Play Mode를 시작합니다.
4. 키보드 `1`~`6`을 눌러 반응을 확인합니다.

### 외부 입력 테스트

Unity Play Mode 또는 Windows Standalone 실행 중 프로젝트 루트에서 실행합니다.

```powershell
python Tools/send_reaction.py cheer
python Tools/send_reaction.py gift --amount 5000
python Tools/send_reaction.py throw
python Tools/send_reaction.py bonk --payload-style broadcast
python Tools/send_websocket_reaction.py bonk --payload-style broadcast
python Tools/send_osc_reaction.py bonk
```

### Windows Standalone

기본 빌드 경로는 `Builds/Windows/VTuberReactionControllerDemo.exe`입니다.

```powershell
& "Builds/Windows/VTuberReactionControllerDemo.exe" -screen-fullscreen 0 -screen-width 960 -screen-height 540
```

빌드 방법은 [StandaloneBuild_빌드방법.md](Docs/StandaloneBuild_빌드방법.md)를 참고합니다.

## OBS 캡처와 Overlay Mode

OBS는 Unity Standalone 반응을 방송 화면에 합성하고 검증하는 출력 환경으로 사용했습니다.

```powershell
& "Builds/Windows/VTuberReactionControllerDemo.exe" --obs-overlay -screen-fullscreen 0 -screen-width 960 -screen-height 540
```

Overlay Mode에서는 HUD를 숨기고 초록색 배경을 사용합니다. OBS에서 Unity 캡처 소스에 Chroma Key 필터를 적용하면 캐릭터와 VFX 중심으로 합성할 수 있습니다.

현재 방식은 크로마키 기반 오버레이이며, 진짜 알파 투명 윈도우 출력과는 구분됩니다.

관련 문서: [OBS 설치 및 Unity 빌드 앱 캡처 설명서](Docs/OBS_설치_및_Unity빌드앱_캡처_설명서.md), [OBS Overlay Mode 사용설명서](Docs/OBS_OverlayMode_사용설명서.md)

## 검증 결과

### 빌드 및 자동 테스트

```text
dotnet build VTuberReactionControllerDemo.sln --nologo
  -> 오류 0개

Unity EditMode Test Runner
  -> 5개 실행 / 5개 통과 / 실패 0개

Python syntax check
  -> UDP·WebSocket·OSC 테스트 송신 스크립트 통과
```

테스트로 고정한 규칙:

- 표준 JSON alias가 `ReactionEvent`로 변환되는지 확인
- `selectedReaction`이 donation amount보다 우선하는지 확인
- `bonk` broadcast 매핑이 유지되는지 확인
- 명시적 반응이 없을 때 금액 fallback이 동작하는지 확인
- 빈 payload가 실패 사유와 함께 거절되는지 확인

테스트 코드: [`ReactionEventJsonMapperTests.cs`](Assets/Tests/Editor/ReactionEventJsonMapperTests.cs)

### 수동 QA 및 Windows Standalone smoke test

- `cheer`, `shock`, `gift`, `throw`, `spark`, `bonk` Play Mode 반응 확인
- 연속 UDP 입력의 Queue 순차 처리 확인
- `throw` Random Start Area 기반 시작 위치 확인
- `ReactionDefinition` 기반 VFX·표정·특수 연출 확인
- `spark` 코드 수정 없는 데이터 확장 확인
- Windows Standalone 빌드 생성 및 실행 확인
- UDP receiver `127.0.0.1:9000` 시작 확인 (로컬 전용)
- WebSocket receiver `ws://127.0.0.1:9001/reaction/` 시작 확인
- OSC receiver `127.0.0.1:9002` 시작 확인 (로컬 전용)
- Standalone에서 6개 반응의 수신·enqueue 확인
- OBS Window Capture 확인
- Overlay Mode 및 Chroma Key 적용 확인

### 성능 검증 기준

Profiler 측정 기준과 시나리오는 문서화했습니다.

- Idle 상태 CPU·GC Alloc
- 단일 반응의 Instantiate·표정·Coroutine 비용
- 연속 입력 시 Queue 누적과 frame time
- VFX 반복 생성·삭제에 따른 메모리 누적
- OBS 캡처 조건에서의 렌더링 비용

현재 Profiler 자동 캡처 중 일부는 Play Mode가 아닌 상태에서 실행되어 실제 성능 수치가 아닌 도구 호출 확인값입니다. 따라서 확정적인 FPS 개선 수치는 제시하지 않고, 측정 기준과 후속 검증 항목으로 구분했습니다.
