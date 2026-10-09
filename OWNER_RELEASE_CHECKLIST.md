# Your Windows release checklist

This is the hands-on and approval work needed for the first Windows Act I
release. The engineering task list stays in `STANDALONE_RELEASE_ROADMAP.md`;
`TEST_PLAN.md` contains the full test steps. This checklist does not ask you to
run automated tests or build the game.

## Play the candidate

- [ ] Extract the latest **Release** ZIP into a folder outside the project and
  launch it by double-clicking the executable. Confirm the Main Menu, default
  resolution, text and controls are usable. Do this on a Windows account or PC
  without Unity installed if available.
- [ ] Complete Act I and Agniya from a fresh save without debug commands.
  Talk to Amara after Q001, follow Q003, solve the puzzles, beat both bosses,
  acquire Ember Step and reach the Act I chapter close. Record any dead end,
  missing beat, confusing direction or unreadable prompt.
- [ ] Save before, during and after Agniya. Quit the process, relaunch and
  Continue. Confirm scene, position, quest, memory, ability and checkpoint.
  Die and respawn once in each scene.
- [ ] Play with a physical controller as well as keyboard and mouse. Check
  left-stick movement and sprint, D-pad Up interaction, D-pad Down map,
  combat, dialogue, menus, remapping and controller disconnect/reconnect.
- [ ] Judge combat on Story, Normal, Warrior and Mythic. Check parry and
  unblockable tells, enemy differences, Ember Step's cost, and whether Mythic
  can be finished without grinding. Note what feels unfair or unclear.
- [ ] Check text scaling, subtitles, colour-independent cues, screen effects,
  camera shake, resolution/quality changes, Alt-Tab and window focus. Use
  `TEST_PLAN.md` M0–M5 for the exact steps.

For every failure, send the build commit from `build-info.txt`, the steps to
repeat it, expected and actual behavior, and a screenshot or short recording
when the problem is visual.

## Sign off on the release

- [ ] Review the final Act I chapter close, story wording, art, animation,
  audio, UI and any remaining optional placeholders. Approve what is ready or
  list changes needed. The first release is an Act I slice; the other six
  temples and Acts II–V are later work.
- [ ] Review the measured performance report on the reference PC and accept
  the 1080p/60 target and 1080p/30 fallback results. This is a measurement and
  approval step, not a request for you to build profiling tools.
- [ ] Approve the final Release ZIP, version, store description, screenshots,
  credits, asset-rights ledger and the unsigned-build notice. All tools and
  assets for this release should remain free or already covered by your Unity
  Personal license.
- [ ] Provide or approve the itch.io publishing account and the player support
  path. Publishing happens only after the candidate and store page are ready
  for this final approval.

There is no paid license, signing service or asset purchase in this checklist.
