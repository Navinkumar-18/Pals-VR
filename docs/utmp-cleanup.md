# `.utmp/` Cleanup Notes

## What `.utmp/` is

`.utmp/` is generated **Unity 6 Android build tooling output** (CMake/Gradle
intermediate artifacts and ninja build files produced when Unity compiles
native plug-ins for the Android/Quest target, typically via IL2CPP).

It is **not** part of the project source and **should not be versioned**. As of
the audit (2026-09-25) 49 files under `.utmp/` were tracked in git.

## What has been done (Phase A)

- Added `/.utmp/` to `.gitignore` so no **new** generated files get tracked.
- **No tracked files were deleted and no git commands were run** to avoid any
  destructive operation without explicit instruction.

## Manual cleanup (when you are ready to run it)

The local build files are **not** removed by the following (they remain on
disk, only git stops tracking them):

```bash
git rm -r --cached .utmp
git commit -m "chore: stop tracking generated .utmp build output"
```

After the commit, run one Android build in the editor to confirm the Quest
pipeline still works (it regenerates `.utmp/` automatically).

## Why we did not auto-clean

- The repository is shared (`origin/main`, plus `kanika`/`mithres` branches).
- A `git rm --cached` is a history-changing operation that touches other
  collaborators' working trees.
- The instruction for this phase was to document the cleanup separately and
  avoid destructive git operations.