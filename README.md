# VTuber Reaction Controller Demo

Unity 기반 VTuber 리액션 컨트롤러 데모입니다.

외부 이벤트를 `ReactionEvent`로 변환하고, `ReactionController`가 큐 기반으로 VFX, VRM 표정, 투척 연출, UI 로그를 순차 처리합니다.

## Demo Video

**v1 - Standalone UDP Reaction Demo**

[![VTuber Reaction Controller Demo v1](https://img.youtube.com/vi/OjYZkSU9oJ8/0.jpg)](https://youtu.be/OjYZkSU9oJ8)

https://youtu.be/OjYZkSU9oJ8

## What v1 Shows

- Windows Standalone 앱 실행
- PowerShell에서 UDP JSON 이벤트 전송
- `cheer`, `shock`, `gift`, `throw`, `spark` 리액션 처리
- 큐 기반 순차 반응 처리
- VRM 아바타 표정 전환
- VFX 재생
- `throw` 입력의 투척 연출과 hit VFX
- 왼쪽 리액션 목록 UI
- 오른쪽 후원 로그 UI
- 키보드 1~5 테스트 입력

## Runtime Flow

```text
Keyboard input or UDP JSON
  -> ReactionEvent
  -> ReactionController.Enqueue(...)
  -> queued reaction processing
  -> ReactionDefinition
  -> VFX / VRM expression / throw effect / UI log
```

## Current Architecture

```text
ReactionCatalog
  -> owns reaction menu data used by keyboard input and list UI

KeyboardReactionSimulator
  -> converts number-key input into ReactionEvent test events

UdpJsonEventReceiver
  -> receives UDP JSON and converts it into ReactionEvent

ReactionController
  -> queues and executes reactions

ReactionDefinition
  -> data-driven mapping from reaction type to VFX, expression, and throw settings

ReactionListPresenter
  -> renders the left reaction list UI

ReactionLogPresenter
  -> renders latest donation and history log UI

ReactionHudPresenter
  -> groups HUD presenters for refresh/initialization
```

`ReactionSystemRoot` is intentionally left as a future refactor. It will be introduced when OBS or a real donation API input adapter is added.

## Implemented Reactions

| Input | Type | Demo behavior |
| --- | --- | --- |
| `1` | `cheer` | Joy expression + VFX |
| `2` | `shock` | Surprised expression + electric VFX |
| `3` | `gift` | Fun expression + firework VFX |
| `4` | `throw` | Projectile throw + hit VFX + avatar hit reaction |
| `5` | `spark` | Data-only added reaction using ReactionDefinition |

## How To Run In Unity

1. Open this folder as a Unity project.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Enter Play Mode.
4. Press `1`, `2`, `3`, `4`, `5` to trigger reactions.

## UDP Smoke Test

Run these commands while Play Mode or the Standalone build is running:

```powershell
python Tools/send_reaction.py cheer
python Tools/send_reaction.py shock --user ViewerB --amount 0 --message "shock from python"
python Tools/send_reaction.py gift --user ViewerC --amount 5000 --message "gift from python"
python Tools/send_reaction.py throw --message "throw from python"
python Tools/send_reaction.py spark --amount 1500 --message "spark from python"
```

Queue stress test:

```powershell
python Tools/send_reaction.py spark --amount 1500 --message "spark queue test" --count 10 --delay 0.2
```

## Windows Standalone Build

Unity Editor menu:

```text
VTuber Demo -> Build Windows Standalone
```

PowerShell:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" `
  -batchmode `
  -quit `
  -projectPath "C:\MyPrograms\Unity\VTuberReactionControllerDemo" `
  -executeMethod VTuberReactionControllerDemo.Editor.StandaloneBuild.BuildWindowsStandalone `
  -logFile "Logs/StandaloneBuild.log"
```

Default output:

```text
Builds/Windows/VTuberReactionControllerDemo.exe
```

The Standalone build defaults to a `960x540` windowed, resizable screen for recording.

Smaller recording window:

```powershell
& "Builds/Windows/VTuberReactionControllerDemo.exe" -screen-fullscreen 0 -screen-width 800 -screen-height 450
```

## Verified v1 Scope

- `dotnet build VTuberReactionControllerDemo.sln --nologo`
  - Errors: `0`
  - Warnings: Unity/NuGet/external asset reference warnings only
- Windows Standalone build succeeded
- Standalone UDP smoke test confirmed:
  - `cheer`
  - `shock`
  - `gift`
  - `throw`
  - `spark`

## Current Limitations

- Real donation platform API is not connected yet.
- OBS automation is not connected yet.
- Current external input is simulated through UDP JSON.
- Transparent background overlay is not implemented yet.
- Large generated build output and local working docs are intentionally excluded from Git.

## Next Direction

When OBS or a real donation API input adapter is added, the next architecture refactor is:

```text
ReactionSystemRoot
  -> scene-level wiring and initialization root
  -> keeps ReactionController, input adapters, catalog, and HUD presenters separated
```
