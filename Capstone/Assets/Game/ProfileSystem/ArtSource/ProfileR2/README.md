# Profile R2 — original layout, 23 October 2026

Design source: `E:/AIWork/Capstone/Profile_OriginalLayout_R2_2026-10-23`.

Keep runtime layout at **1920×1080**. MainProfilePanel (32,104,1856,436), PlayerInfoPanel (32,560,780,488) and StatsOverviewPanel (832,560,1056,488) retain their original anchors/rects. Avatar stays circular; team and metrics stay 3×2.

`Components` and `Icons` preserve editable SVGs from R2. Art references point to the one runtime material under `Resources/UI/ProfileR2/Art`. The original manifest records provenance; original design screenshots and fixture numbers remain external.

Runtime component fills already compose the original material at 50% plus their reading surface; draw fills at opacity 1. Root/header raw material draws at 0.5 once. No whole-tree alpha. Original player/pet sprites and BOX two-paws artwork are loaded from existing providers/resources.

`ProfileR2Skin` is called by RebuildPanel. `ProfileR2Graphic` uses the shared polygon geometry, local circle contour, native fill/border textures and crop/cover. Non-native sized frames use a contour instead of stretching the border. Button picking and state use the same contour. No shared palette or provider API was modified.

Name fitting remains one line in the 28–48 range using the current font's measured preferred width; state changes never alter text geometry. Rename continues through TrySetDisplayName, avatar selection through TrySelectAvatar, and pet clicks only update the profile's local selected index.

Editor capture/Profiler output and isolated saves are opt-in under `QaArtifactPaths.Root/profile-original-r2-unity-2026-10-23/`, outside Git. No screenshot is loaded as a UI sprite.
