# Test fixtures

Files here are inputs to tests that have to be **real**, not reconstructed.

## `save-v1-windows-player.sav`

A save file in save format **version 1**, written by the Windows Development player
on 2026-09-25 during TASK 039's smoke-test run, before TASK 041 added
`SaveData.SceneStates` and bumped the format to version 2. Nothing in it was edited.

`SaveMigrationTests` loads this file through the ordinary reader and asserts what the
version 1 → 2 step makes of it.

It matters that this is the real thing. A migration tested against a `SaveData` built
in code is tested against the current build's idea of the old shape, and the current
build's idea of the old shape is precisely what is in doubt — a field that was
renamed, or one whose default changed, looks correct from inside the build that
changed it. This file was produced by the earlier build and has a real envelope
checksum over a real payload, so a mistake about version 1's field names shows up as a
failure rather than as agreement.

Do not regenerate it, reformat it, or fix its whitespace. It is evidence.
