# VTuberReactionControllerDemo Agent Notes

This is the local working root for the VTuber reaction controller demo.

## Start Here

Before continuing feature work, read:

```text
Docs/VTuberControllerDemo_CheckPoint.md
Docs/README.md
```

For the keyboard-input design explanation, read:

```text
Docs/Controller파악.md
```

For the full 3-day demo direction, read:

```text
Docs/SCON_VTuber_3일_데모_따라하기.md
```

## Project Rules

- Treat this folder as the Unity project root.
- Work on user scripts under `Assets/`.
- Do not edit generated or cache folders such as `Library/`, `Temp/`, `obj/`, and `Logs/` unless explicitly requested.
- Use Unity-friendly C# style: namespaces, 4-space Allman braces, private serialized fields where practical, and small lifecycle methods.
- For each future prompt in this project, overwrite `Docs/현재프롬프트_질문_및_답변.md` with the latest user prompt, assistant answer or final briefing, changed files, validation evidence, and next-step criteria. Do not create a new prompt Q&A markdown file per turn.
- Verify C# changes with:

```powershell
dotnet build VTuberReactionControllerDemo.sln --nologo
```

## Current Next Step

Refactor the current `ReactionController` toward:

```text
KeyboardReactionSimulator -> ReactionEvent -> ReactionController.Enqueue(...)
```

Keep `ReactionController` responsible for processing reactions, not directly polling every keyboard key.
