# BAG / PET material fields — 23 October 2026

Source: `E:/AIWork/Capstone/BAG_PET_Backgrounds_2026-10-23/Assets/Backgrounds/`.
The two PNGs are unmodified copies of the supplied 1600 × 900 materials.

- Strategy 1: opaque material × vertex alpha **0.5**, over the existing panel/shell palette. No second 50% multiplier.
- BAG shell, browser and inspector sample one 1552 × 748 cover field. Origins: (0,0), (288,24), (1088,24). No material is added to item cells.
- PET inspector uses **xMaxYMid cover** on its existing 864 × 620 contour. The existing R3 frame remains above the material.
- `PanelMaterialField` adds cached mesh gradients behind text. Only decoration is tinted; host/content opacity and picking are untouched.
- Import: native NPOT size, clamp, bilinear, no mipmaps, no read/write, no sprite slicing.
- No preview screenshot, AI source export, overlay variant or precomposed panel is loaded at runtime.

Runtime A/B verification is opt-in through `BackgroundVerification` in the Editor. Its original-material mode and save isolation use only the task folder under `QaArtifactPaths.Root`; they do not change game data or scene assets.
