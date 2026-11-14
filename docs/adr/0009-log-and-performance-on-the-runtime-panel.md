# Log and Performance live in OperatorConsole

ADR 0003 widened OC to on-site operator tools. The same technician also needs to see what went
wrong and how the app is running on the installed machine, so we widen the scope again to on-site
diagnostics: the Runtime Panel gains a Log tab and a Performance tab. We rejected a separate
package plugged in through a public tab seam: nobody else needs that seam yet, and it would undo
ADR 0008's choice to keep the panel's elements internal. We modelled the approach on SRDebugger
but rejected its service locator and `Stopwatch` timing in favour of `FrameTimingManager` and
`ProfilerRecorder`.

## Consequences

- Both tabs work in release builds, because on-site machines run them. Metrics a release build
  cannot measure (some render counters) show as unavailable.
- Log capture starts at app start, so errors from before the panel opened are kept. It runs only
  when an enabled Panel Definition shows the Log or Performance tab; otherwise there is no cost.
- The word "Profiler" is avoided in the domain so the tab is not mistaken for the Unity Profiler.
- The Save File still holds only Overrides; nothing about the Log or Performance is persisted.
