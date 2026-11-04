# Gameplay HUD R11

The existing `GameplayHudController` builds the view at runtime. It keeps the existing Canvas, live MinimapController, PetCommandHudProvider, QuestTrackerHudController, Inventory and capture coordinator. Open the existing gameplay scene; no screenshot is used as a HUD sprite.

- On GameplayHUD, `Party Picker Mode` selects Toggle (default) or Hold. `Party Key` defaults to P. In Toggle, a short tap toggles; holding P and releasing always closes, including when the radial was already open. In Hold, every release closes. Opening/closing the radial does not summon, change selection, save, or pause the world. Full Companion remains available from Trainer Menu.
- Slot click and existing 1–6 keys use PetCommandInput → PetSummonDirector. Accepted requests close the radial; identity/current is refreshed from the provider after the sequence. Failure feedback comes from the Director. Do not call SetActivePet to update the HUD early.
- `Skill Bar Display Mode` keeps WhilePetSummoned / AlwaysVisible. Four slots bind to Z/X/C/V and retain real cooldowns. R/T keep the existing Bag assignment/save/use flow.
- Tap G to request capture of the camera-locked, explicitly catchable wild target through PetCaptureCoordinator. Hold G for 0.28 seconds to choose a ball; releasing G automatically closes the picker. Releasing after a hold or selecting a ball never throws/consumes. Selection while held stays closed until key-up. Missing key-up/long frames cannot turn a hold into a tap throw. No ball is granted by the HUD.
- G previously switched player visual. With the HUD active, **Shift+G** preserves that configured visual-switch function; G alone belongs to capture. Without the HUD the original configured key remains unchanged. The picker states this binding.
- HubCaptureHud is added to the GameplayHUD object if absent. It can also be added in Edit Mode to serialize its capture key, hold threshold and optional opportunity source. `Opportunity Source` must implement `ICaptureOpportunitySource` and provide a real combat flag. Current combat has no such source: the alert stays hidden. The UI never derives an opportunity from HP or capture odds.
- Empty/missing MP uses an empty track and `— / —`. Portraits, skills and item sprites come from their providers. Empty party slots retain the original round two-paw glyph.

Separated PNG layers are under `Capstone/Assets/Game/HudSystem/Resources/UI/HubR11`. Contour/alpha/hit testing share R11's rounded sector coordinates. Avatar R66, frame R78; HP R126, MP R104 (the JSON/full HUB prompt supersedes the isolated Pet prompt's R114 typo). Six sectors use R201, 96×96, 132° total. No carousel view is instantiated.

QA commands are Editor-only. Use **Game Tools → GameToolThang → HUD → R11 → Verify isolated runtime**. Test saves and evidence go outside Git to `E:/UnityWork/CapstoneProjectData/Artifacts/hub-r11-2026-11-04/`. Reports distinguish engine callbacks from physical keyboard/mouse, Editor measurements from standalone, and unavailable opportunity data.

For the newer hold/release behavior, use **Verify hold release regression** in the same menu. It runs deterministic held/up edges through the actual handlers at both resolutions, with separate saves/reports under `E:/UnityWork/CapstoneProjectData/Artifacts/hud-hold-release-2026-11-05/`. It does not claim a physical held-key test.
