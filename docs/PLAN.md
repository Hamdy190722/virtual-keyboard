# Virtual Keyboard for Windows — Project Plan

An always-on-top on-screen keyboard that types into whatever app or browser currently has focus.
Built with C# / .NET 8 + WPF. Portable single-file .exe.

---

## 1. Decisions (locked by interview)

| Area | Decision |
|---|---|
| Platform | Windows 10/11, x64 |
| Stack | C# / .NET 8 + WPF (`net8.0-windows`) |
| Input injection | `SendInput` only (no driver). Normal apps + browsers. |
| Elevation | Runs as the user. Cannot type into elevated windows or anticheat games — explicitly out of scope. |
| Layouts | US QWERTY + Arabic (true RTL, Arabic-Indic digits, shift variants) |
| Prediction | Offline embedded wordlists (EN + AR) + per-user learned words |
| Suggestion tap | Replace the in-progress word (backspace then insert) |
| Physical keyboard | Independent — no hooking, no mirroring |
| Key behaviour | Auto-repeat, long-press alternates, click sound, press animation |
| Window | Bottom-docked by default, draggable, width-resizable, tray icon, `Ctrl+Alt+K` show/hide |
| Packaging | Self-contained single .exe (~100–150 MB), no runtime install |
| Settings | `settings.json` + `learned-words.json` next to the exe (portable) |
| Tests | xUnit for core logic; GUI verified by running |
| Autostart | Optional tray toggle → `HKCU\...\Run` |
| Style | Modern dark by default, light + high-contrast themes |

## 2. Goals

1. Click keys with the mouse and have characters appear in the focused app (browser, Notepad, Office, chat apps).
2. Never steal focus from the app being typed into.
3. Sticky modifiers: Shift / Ctrl / Alt / Win can be tapped once (one-shot) or locked (double-tap).
4. Switch between English and Arabic at runtime; Arabic renders right-to-left with Arabic-Indic digits.
5. Word suggestions above the keys, offline, improved by learning the user's vocabulary.
6. Comfort features: auto-repeat, long-press accented/Arabic alternate forms, click sound, animations.
7. Lives in the tray, toggled with a global hotkey, remembers position and settings.

## 3. Non-goals (v1)

- Typing into elevated/admin windows (Task Manager, UAC prompts).
- Games with anti-cheat / raw-input-only apps (would require the Interception kernel driver).
- Cloud prediction, telemetry, accounts.
- Full OS keyboard-layout parity (dead keys, AltGr, IME composition).
- Mobile / Linux / macOS.

## 4. Architecture

```
VirtualKeyboard.sln
├─ src/
│  ├─ VirtualKeyboard.Core/          net8.0, no UI dependencies
│  │  ├─ Layouts/                    KeyDef, KeyboardLayout, LayoutCatalog (EN + AR)
│  │  ├─ Input/                      KeyStroke, InputEvent, InjectionPlanner, SendInputInjector
│  │  ├─ Sticky/                     StickyModifiers state machine
│  │  ├─ Prediction/                 WordPredictor, WordBuffer
│  │  ├─ Settings/                   AppSettings, SettingsStore
│  │  └─ Assets/                     english-words.txt, arabic-words.txt (embedded)
│  └─ VirtualKeyboard.App/           net8.0-windows, WPF + WinForms (tray)
│     ├─ MainWindow                  borderless keyboard, no-activate window
│     ├─ Controls/KeyButton          press animation, repeat, long-press
│     ├─ Services/                   tray, hotkey, theme, click sound, autostart, foreground watcher
│     ├─ SettingsWindow              theme/layout/behaviour settings
│     └─ Themes/                     Dark, Light, HighContrast resource dictionaries
└─ tests/
   └─ VirtualKeyboard.Core.Tests/    xUnit
```

Dependency direction: **App → Core**. Core is pure logic and fully unit-tested.

## 5. Key technical designs

### 5.1 No focus steal (the make-or-break requirement)

- Window: `WindowStyle=None`, `ShowActivated=False`, `Topmost=True`, `ShowInTaskbar=True`.
- On `SourceInitialized`: add `WS_EX_NOACTIVATE` (0x08000000) and `WS_EX_APPWINDOW` (0x00040000) while clearing `WS_EX_TOOLWINDOW` (0x00000080) so the taskbar button is always visible.
- Hook `WM_MOUSEACTIVATE` (0x0021) → `MA_NOACTIVATE` (3), and hook `WM_SYSCOMMAND` (`SC_RESTORE` = 0xF120) → `ShowWindow(SW_SHOWNOACTIVATE)` + `SetWindowPos(..., SWP_NOACTIVATE)` so clicking the taskbar button restores without stealing focus.
- All clickable controls are `Focusable=False`; no text boxes on the keyboard surface.
- Consequence: the foreground window never changes when a key is clicked or the window is restored from the taskbar, so `SendInput` lands in the right app.

### 5.2 Injection

- Printable characters → `KEYEVENTF_UNICODE` (layout-independent; Arabic works even if the OS layout is English).
- Control keys and shortcuts → virtual-key codes.
- When Ctrl/Alt/Win is sticky, letters/digits are sent as VK combos (`Ctrl+S` actually works).
- Shift is resolved in the layout data (key has `Text` + `ShiftText`), so no synthetic Shift is needed for characters.
- All events for one key press are batched into a single `SendInput` call.
- `InjectionPlanner` is pure: `(stroke, modifiers) → [InputEvent]`, unit-tested without touching the OS.

### 5.3 Sticky modifiers

State machine per modifier: `Off → Armed → Locked → Off` (tap cycles).
- `Armed` (one-shot) is consumed after the next non-modifier key.
- `Locked` persists until tapped again.
- `Caps` key toggles Shift lock directly.
- Visual: armed = accent border, locked = solid accent fill.

### 5.4 Prediction

- Embedded wordlists: English (~1.5k) and Arabic (~600), frequency-ordered.
- Optional user wordlists: `wordlists/*.txt` next to the exe (one word per line).
- Learned words: completed words (length ≥ 2) are counted and persisted to `learned-words.json`.
- Ranking: prefix match, score = base frequency + 2000 × learned count; language-filtered.
- In-progress word tracked by `WordBuffer`; tapping a suggestion sends `N` backspaces then the word.
- Buffer resets on space/punctuation/Enter/Tab/navigation keys and when the foreground app changes.

### 5.5 RTL Arabic

- Layout declares `IsRightToLeft`; the key host gets `FlowDirection=RightToLeft`, so rows mirror automatically.
- Digits are Arabic-Indic (٠١٢٣٤٥٦٧٨٩); shift gives `! @ # $ % ^ & * ) ( _ +`.
- Hamza/alef variants are on Shift and also on long-press (`ا → أ إ آ ٱ`).
- Injection is per-character Unicode, so the target app's own bidi/shaping engine renders correctly.

### 5.6 Key interaction

- Text keys with no alternates: fire on mouse-down (feels like a real keyboard).
- Keys with alternates: 450 ms hold opens an in-window alternate strip (no popup window, so focus is never stolen).
- Repeatable keys (Backspace, arrows, Space): fire immediately, then repeat after 400 ms at ~45 ms intervals.
- Click sound: synthesized 20 ms WAV click played through `SoundPlayer` (no asset files).
- Press animation: 60 ms scale to 0.94 and back.

## 6. UI layout

```
┌──────────────────────────────────────────────────────────────┐
│ ⠿ drag area            Virtual Keyboard       ⌨ EN   ⚙  —   │  header
├──────────────────────────────────────────────────────────────┤
│ [hello] [help] [held] [he]                                   │  suggestions
├──────────────────────────────────────────────────────────────┤
│ ` 1 2 3 4 5 6 7 8 9 0 - =        ⌫                           │
│ Tab  q w e r t y u i o p [ ] \                               │
│ Caps  a s d f g h j k l ; '      Enter                       │
│ Shift  z x c v b n m , . /      Shift                        │
│ Ctrl Win Alt      Space      Alt Win Ctrl  ع                 │
│ Esc Home End Del PgUp PgDn ← ↑ ↓ →                           │
├──────────────────────────────────────────────────────────────┤
│ English · Shift armed                        Ctrl+Alt+K      │  status
└──────────────────────────────────────────────────────────────┘
```

Arabic mirrors the rows and swaps the layout key to `EN`.

## 7. Milestones

- [x] **M0 — Scaffold**: solution, three projects, build green.
- [x] **M1 — Core domain**: layouts, sticky state machine, word buffer, predictor, injection planner + unit tests.
- [x] **M2 — Shell**: borderless no-activate window, key rendering, themes, drag/resize, position memory.
- [x] **M3 — Typing**: SendInput backend, sticky modifiers, repeat, long-press alternates, click sound, animations.
- [x] **M4 — Prediction**: suggestion bar, learning, word replacement.
- [x] **M5 — Desktop integration**: tray icon/menu, `Ctrl+Alt+K`, settings window, autostart, single instance.
- [x] **M6 — Ship**: self-contained single-file publish, README, smoke test.

### Acceptance criteria

1. With Notepad focused, clicking `h e l l o` types `hello` and Notepad keeps focus the whole time.
2. In a browser address bar, `Shift` then `1` produces `!`; Shift clears itself afterwards.
3. `Ctrl` then `s` triggers Save in Notepad (or the app's shortcut), then Ctrl clears.
4. Double-tap `Shift` locks it; typing stays uppercase until Shift is tapped again.
5. Arabic mode types Arabic letters and Arabic-Indic digits into Notepad and Chrome.
6. Typing `hel` shows `hello`, `help`, …; clicking one replaces `hel` with `hello`.
7. Holding Backspace deletes repeatedly; holding `e` shows `é è ê ë`.
8. `Ctrl+Alt+K` hides/shows the keyboard without changing the foreground app.
9. Settings survive restart; window returns to the same position.
10. `publish.ps1` produces one .exe that runs on a machine without .NET installed.

## 8. Risks & mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Clicking the keyboard activates it and typing goes nowhere | Fatal | `WS_EX_NOACTIVATE` + `MA_NOACTIVATE` + no focusable controls; verified first in M2 |
| Some apps ignore `KEYEVENTF_UNICODE` | Rare | Documented limitation; VK path used for control keys. Most browsers/Office/Notepad accept Unicode. |
| Prediction deletes the wrong text after the user clicks elsewhere | Data loss | Foreground-window watcher clears the word buffer; replacement only runs on the tracked buffer |
| Arabic wordlist coverage is thin | Weak suggestions | Learning + user-supplied `wordlists/*.txt`; bundled list is a starter, not a corpus |
| Single-file publish + WinForms tray icon | Packaging issue | `IncludeNativeLibrariesForSelfExtract`; tested in M6 |
| Antivirus flags synthetic input | False positive | Signed builds out of scope; document. `SendInput` is standard. |

## 9. Testing

- **Unit (xUnit)**: sticky transitions, layout integrity (row widths, shift text, RTL flag), injection planning (Unicode vs VK vs Ctrl combos), predictor ranking/learning, word buffer boundaries, settings round-trip.
- **Manual smoke**: the 10 acceptance criteria above on this PC.

## 10. Deliverables

- `publish/VirtualKeyboard.exe` — portable, self-contained.
- `settings.json`, `learned-words.json`, `wordlists/` beside the exe.
- `README.md` — usage, hotkey, limitations, how to add wordlists.

## 11. Status

Implemented and verified on this PC (Windows 11, .NET 8):

- `dotnet build VirtualKeyboard.sln` — clean, 0 warnings.
- `dotnet test` — **42/42 passing**.
- End-to-end smoke test against a browser text field (window title mirrors the field):
  - typing `hello`, backspace, sticky `Shift`+`1` → `!`
  - long-press `s` → alternate strip → `ß`
  - Arabic layout: `كتاب` injected with the keyboard mirrored RTL
  - `Ctrl`+`A` then `Ctrl`+`X` clears the field
  - `hel` shows suggestions; tapping one replaces the word
  - `Ctrl+Alt+K` hides/shows; header drag moves the window; focus never leaves the target app
- Dark, light and high-contrast themes render correctly.
- Single-file publish produces `publish/VirtualKeyboard.exe`.
