# Generated AI data and verification storage

- Keep gameplay source, scenes, prefabs, runtime textures, fonts and their `.meta` files in the Unity project.
- Put AI screenshots, frame sequences, videos, overlays, diffs, test saves, profiler recordings, logs and standalone builds under `E:/UnityWork/CapstoneProjectData/Artifacts/<task>/`.
- Use `Capstone.Game.UISystem.QaArtifactPaths.Root` in Unity verification code. Never write bulky QA output into this repository or `Assets/`.
- Do not create a junction/symlink from the repository to the external artifact directory: Git/Desktop can still collect those files.
- Keep at most 10 rolling manual-capture frames. At handoff retain only useful final images, aiming for <=50 MB per task; summarize counters into small CSV/JSON/text reports.
- Raw frame/video/binary-profiler recording must have explicit duration and size limits. Do not leave a recorder running after verification ends.
- Remove failed, duplicate and intermediate AI-owned captures after recording the relevant finding. Delete only paths recorded as created by that task. Preserve user references, source assets, real saves and backups.
- Keep caches already relocated to E in their current location. Do not move active app binaries or edit Git history/stashes as routine cleanup.
- Before committing, check staged files for generated output and files >=100 MB. `.gitignore` does not remove files already tracked or stored in a stash.
- UI implementation/QA remains paused until the user requests it; storage maintenance does not authorize starting Play Mode, a capture session or a standalone build.
