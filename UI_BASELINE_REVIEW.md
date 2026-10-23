# UI BASELINE REVIEW

A first pass over the TASK 054 UI, done on 24 October 2026 by rendering and probing every
screen rather than by playing the game.

**What this is for.** `TEST_PLAN.md`'s M0 pass needs a human with a keyboard, a gamepad and
an opinion. This is the pass that can be done without one — the parts that are mechanical,
exhaustive and boring, which is exactly where a human tester's attention runs out first.
Each finding below is either already fixed, or written down for the owner to accept,
reject, or correct.

**What it cannot tell you.** Nothing in here was played. No finding below is about feel,
pacing, how a menu sounds, whether the wording lands, or whether a control responds the way
a hand expects. Those need M0 and they are not in scope for this document. Where a reading
is the editor's rather than a built player's, it says so.

## How each finding was reached

| Method | What it can see |
|---|---|
| `UiCapture` renders a screen to a PNG in the Editor | Layout, overlap, clipping, contrast, placeholder text |
| The same, at text scale 1.5 | Whether a screen survives the largest accessibility setting |
| The same, in the pseudo-locale (`qps`) | Whether a translation longer than English breaks the layout |
| Walking `Selectable.navigation` in a test | Whether a gamepad can reach every control |
| Reading `CanvasScaler` state before and after a setting is applied | Whether a setting does anything at all |

---

## Findings

### F1 — Text scaling did nothing, in either scene — **FIXED**

**Severity: high.** SPEC.md section 43 requires text scaling; TASK 017 recorded it as
delivered; the slider existed, moved, and saved its value.

`UIScaleApplier` set `CanvasScaler.scaleFactor`, which Unity reads **only** in
`ConstantPixelSize` mode. Both canvases in this game are `ScaleWithScreenSize`, where the
scaler recomputes the factor from the reference resolution on every enable and overwrites
whatever was assigned.

Measured, not deduced — asking for 1.5× left the canvas where it was:

```
MainMenu / UI_Canvas  mode=ScaleWithScreenSize
   before:                scaler.scaleFactor=1    canvas.scaleFactor=0.4000813
   after asking for 1.5x: scaler.scaleFactor=1.5  canvas.scaleFactor=0.4000813
Avarsha  / UI_Canvas  mode=ScaleWithScreenSize
   after asking for 1.5x: scaler.scaleFactor=1.5  canvas.scaleFactor=0.4000812
```

Fixed by scaling the reference resolution instead, which is how the mode is meant to be
driven. `AskingForBiggerTextMakesTheCanvasScaleBigger` now asserts the effect rather than
the stored value.

### F2 — The Main Menu had no `UIScaleApplier` at all — **FIXED**

**Severity: high**, and the other half of F1. Both gameplay canvases carried the component;
the Main Menu did not — and until TASK 054 the Main Menu was the *only* place the text-size
slider existed. So the setting lived on the one canvas that could not apply it, and the two
canvases that could were being handed a value their scaler ignored. Broken at both ends,
present-looking at both.

### F3 — At the largest text size, Settings grew past the screen and took Back with it — **FIXED**

**Severity: high.** Visible only once F1 was fixed, which is the usual way one defect hides
another. At 1.5× the panel was taller than 1080, the title was cut off at the top and the
**Back button was off the bottom** — a player who turned the text up could not leave the
screen they turned it up on.

Fixed by pinning the tall panels to the screen's height and letting the middle scroll, so
the title and Back are always on screen at any scale. Same treatment for How to Play.

### F4 — Seven controls were unreachable with a gamepad — **FIXED**

**Severity: high.** Found by walking the navigation graph from the Back button and
comparing what it reaches against what exists. Unity's automatic navigation did not bridge
the Settings screen's two columns, leaving the whole right-hand column stranded:

```
Row_menu.label.subtitle_background/Control/Toggle
Row_menu.label.camera_shake/Control/Toggle
Row_menu.label.screen_effects/Control/Toggle
Row_menu.label.aim_assist/Control/Toggle
Row_menu.label.resolution/Control/Button_menu.label.change
Row_menu.label.quality/Control/Button_menu.label.change
Row_menu.label.fullscreen/Control/Toggle
```

Every accessibility option is in that list. A player on a gamepad could not change any of
them. Navigation is now explicit, and `EveryControlOnTheSettingsPanelCanBeReachedByNavigatingToIt`
fails if a control is ever stranded again. This is the concrete cost of the entry
KNOWN_ISSUES.md has carried since TASK 010.

### F5 — Focus could move to a row scrolled out of sight — **FIXED**

**Severity: medium**, and a consequence of F3's scrolling. A `ScrollRect` does not follow
the selection, so pressing down past the bottom of the visible area moved focus to
something the player could not see — indistinguishable from the list having stopped
working. `ScrollToSelection` now scrolls the minimum distance to bring the focused control
into view; it is on the remap list too, which has the same problem and more rows.

### F6 — Muted text on a focused button was 3.8:1 — **FIXED**

**Severity: low.** Caught by the contrast test, not by eye. Below roughly 4:1 text stops
being readable for players with low vision and on a dim laptop panel. The focus colour is
darker than first chosen; focus still has to be findable, which is why it was bright in the
first place.

---

## Findings left open, for the owner

### F7 — A preview shows every checkbox ticked

**Severity: none in game — a property of the tool.** `UiCapture` renders a toggle with its
checkmark visible whatever the value, because `Toggle.UpdateVisuals` runs from `OnEnable`
and the Editor does not call it outside play mode. The word beside each toggle is correct.
Read the words, not the green squares. **Needs M0 to confirm** the checkmarks are right in
a running game.

### F8 — Stepping to a resolution can take many presses

**Severity: low. Owner's call.** Resolution and quality step forward only. On a machine
offering twenty resolutions the last one is nineteen presses away. The cheap fix is a
left/right pair rather than a single Change button; the thorough one is a dropdown with
explicit gamepad navigation. Deliberately not guessed at.

### F9 — The Display section is missing from the in-game Settings

**A decision, not a defect, but a player cannot tell.** Resolution, quality and fullscreen
are Main-Menu-only, because changing resolution under a live game re-creates the screen
(SPEC.md section 54, edge case 24). Someone who looks for them mid-run will not find them
and nothing says why. A line of text would fix it; whether it is worth the clutter is a
judgement call.

### F10 — Save slots render blank in the Editor

**Severity: none in game.** Slot rows are written at runtime from the files on disk, so a
captured screenshot shows three empty bars. Correct behaviour, unhelpful screenshot.
**Needs M0** to confirm the rows read properly with and without saves present.

### F11 — The whole UI is flat tints

**Severity: expected.** No sprites, frames, transitions or button sounds. That is TASK 053,
and doing it before the art direction exists means doing it twice.

### F12 — Everything above is the Editor's render, not the player's

**The standing caveat.** These are canvases rendered at one size by the Editor. They catch
layout, contrast and text; they cannot catch anything that only exists once the game runs —
a real gamepad's feel, a panel opening over live gameplay, frame cost, or focus behaviour
under an actual `EventSystem`. **M0 owns all of it.**

---

## What M0 should check first

Ordered by how likely a problem is to be found there and nowhere else.

1. **A gamepad, in hand, through every menu.** F4 proves navigation was broken in a way
   nothing but a graph walk would find; the fix is asserted but has never been felt.
2. **Text size at 0.75 and 1.5, in a running game**, including subtitles during dialogue
   and the HUD — F1 means this has never once worked, so no one has seen it.
3. **Hide the HUD mid-fight and bring it back.** New, and the only feature here that
   changes what is on screen during combat.
4. **Pause mid-run, change difficulty, quit without saving.** The unsaved-run warning has
   never been shown to anyone.
5. **Open every screen from Pause and from the Main Menu** and confirm they are the same
   screen — they are the same component, so a difference would mean a real bug.
6. **Read How to Play as though you had never played**, then try to play from it. That is
   TASK 054's gate, stated as an instruction.
