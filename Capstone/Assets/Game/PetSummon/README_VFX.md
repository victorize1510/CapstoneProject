# Pet Spawn / Recall 3D VFX

## Configuration

Default asset: `PetVFX_Default.asset`, already assigned to `Player` and `Vefects_Vexa`.
`PetSummonReleaseEffect` selects the profile and Low / Medium / High quality in the Inspector.
Both effect playback and ball motion default to 1x.

| Setting | Default |
| --- | --- |
| Spawn presentation after ball bounce | 0.80 s |
| Recall presentation after Pickup reaches its release threshold | 0.65 s |
| Body white / original colour blend | 75% / 25% |
| Soft body emission | 0.12 |
| Surrounding shell / orbit lines | Hidden for Spawn and Recall |
| Flash padding around model dimensions | 8% |
| Halo padding around fitted posed mesh | 5% per axis |
| Halo contraction time constant / transient excess cap | 0.08 s / 10% |
| Recall maximum twist / near-to-far pull delay | 60 degrees / 0.08 s |
| Quality particle budget | 16 / 32 / 48 |
| Recall funnel strands | 1 / 2 / 2 |
| Point lights | 0 / 1 / 1 |

Profile durations control the visual sequence; they do not shorten the player Throw / Pickup or pet Cast Spell clips.
Spawn waits for Cast Spell after the visible burst ends, then releases gameplay control.

## Presentation

- Spawn has no beam. The real ball becomes hidden at the flash. A small ball-centred pulse, expanding ellipsoid wave and streak particles cover emergence in 0–0.12 seconds. The body grows from 4% size at the ball to its validated landing by 0.46 seconds. The core and wave fade by 0.46 seconds; white light fades back to native colours in 0.46–0.72 seconds, and the overlay finishes at 0.80 seconds. Cast Spell must finish before commands return.
- Recall retains the current animation. Idle pets turn towards the owner; combat pets track their captured opponent from their moving visual centre. A lost/dead opponent preserves the last heading. Movement, commands and pending damage are locked.
- Spawn and Recall do not render the surrounding shell or orbit lines. The fitted ellipsoid remains an invisible calculation envelope for the Recall beam and funnel; existing prefab halo renderers stay disabled during every update and pooled reuse.
- Recall introduces white light in 0–0.18 seconds. Overlay vertices closest to the ball pull first in 0.18–0.56 seconds; farther vertices lag by up to 0.08 seconds. Radial contraction and up to 60 degrees of rotation create the tapered, twisting body. Dissolve starts at 0.40 seconds to preserve recognition early. One/two helical strands and particles lead towards the ball; a small ball flash ends at 0.65 seconds. Close/coincident balls reduce delay/twist, and decorative strands are suppressed when their forward surface would lie beyond the ball.
- Only overlay vertices change size/location. Gameplay roots, bones, colliders and Animator are never scaled by the VFX.
- Cancellation restores only the current form and ball renderers. The existing director owns the queued recall-then-spawn switch.

## Model-relative flash size

The flash is an oriented ellipsoid sized from the current form's animated mesh, with `burstPadding = 0.08` on each axis. It grows/shrinks with the visual body. Renderer transforms include pet and ancestor scales; imported conservative skin bounds are used only for culling, so they cannot enlarge the visible flash. The shockwave reaches these model dimensions plus 8%, independently of halo padding.

The halo uses posed vertex dimensions in the pet's rotation frame. An ellipsoid with just the box's half extents can cut through diagonally placed wing/tail vertices: the implementation first fits the ellipsoid to the actual posed vertices, then adds `haloPadding = 0.05`. This fitted envelope retains independent width, height and depth rather than using a diagonal sphere radius. LateUpdate expands it immediately; contraction is exponential with a 0.08-second time constant and a 10% transient padding cap relative to that frame's fit.

Skin baking compensates renderer scale before the world matrix applies it, avoiding double scaling. Spawn hides the body for one evaluation frame before sampling a freshly summoned Animator; the ball stays visible until the flash begins. During the active visual sequence, LateUpdate samples the animated skin into a reused mesh buffer. This keeps the body centre and flash on the displayed pose instead of retaining the initial Cast Spell pose. Static meshes do not require baking. Baking costs CPU time; it is limited to the active 0.80/0.65-second presentation, with no warmed-update managed allocation in the Editor checks.

The core's vertical projection is clamped above the validated landing surface. The pulse and shockwave remain at their intended centres and clip below that floor in the shader. Reused particle buffers clamp particles to that floor after simulation. This is a landing-plane limit; it does not provide collision with uneven terrain elsewhere along a streak.

Particle size/travel and ribbon widths use the shortest model half-extent, the ground ring is an ellipse using horizontal extents, and the light uses the largest half-extent. Change `burstPadding` for the explosion and `haloPadding` for the decorative envelope separately.

## Resources and lifecycle

`Prefabs/SpawnVFX.prefab` and `RecallVFX.prefab` contain reusable presentation modules. Energy materials, body overlays, pose mesh, property blocks and particle buffers are cached. The noise texture is generated locally, and no external animation, audio or VFX package is required.

The core renders its outside once, with a soft falloff at its silhouette; back faces no longer double its opacity. Spawn keeps its bright initial cover, then fades the core by 0.46 seconds. The ground shockwave fades in rather than appearing at full brightness. Recall's beam and contact flare fade together at capture completion.

Runtime objects belong to the normal scene lifecycle. Editor fixtures are excluded from scene-pet/save discovery and use explicit cleanup when leaving Play Mode. Neither test tool saves a scene or changes party data.

## Verification tools

Under **Game Tools > Pets**:

1. **Validate Summon Effects**: Edit Mode checks all forms and qualities, posed vertex containment, ellipsoid contact including a ball inside the halo, warp convergence, near-side lead, close-ball safety, halo smoothing/cap, shader errors, texture/UV preservation, scales/parent scale, cancellation, facing, zero managed allocation across 100 warmed presentation updates, and 20 pooled recall/cancel cycles per form.
2. **Run Play Mode VFX Validation**: run in TestChar. Temporary dragons test the complete Cast Spell, spawn and recall for all nine form/quality combinations, plus Medium at scales 0.25 and 2.5 for each form (15 cases). The ball moves during Recall; fixtures clean up afterwards.
3. **Preview 3D VFX > Dusk / Nightfall / Darkness**: temporary Medium fixture pauses once during Spawn and once during Recall. Darkness also has Ctrl+Shift+F8. Press Unity's Pause button to continue. Select the pet and use **F** in Scene view to inspect it from other angles.
4. **Pause Next Summon Effect**: pauses the next real player-driven sequence at a middle frame.
5. **Clear Temporary VFX Previews**: Edit Mode cleanup for stale temporary test objects from an interrupted older preview.
6. **Validate Director in Play Mode**: creates a temporary flat-floor fixture with the authored player and three forms. Exercises physical Throw/flight/bounce, Pickup, a switch requested during Spawn, and latest-request wins during Recall. It asserts that at most one pet is summoned. Its pets are excluded from save discovery; the fixture is removed afterwards.
7. **Validate Recall Combat Facing** (Ctrl+Shift+F9): uses three temporary forms and a disabled enemy fixture outside the enemy registry. The target moves, then is marked lost without damage/rewards. Checks tracking, last-heading preservation and command lock during Recall.

**Install 3D Summon VFX** explicitly upgrades missing modules/streak settings in existing VFX prefabs, creates missing resources and fills missing profile references. It preserves profile settings and does not save scenes. Existing player prefabs are saved only when a profile reference is missing.

## Verified in this change

- Unity C# compilation; runtime and Editor C# builds passed (existing warnings remain).
- Editor checks passed all three forms and qualities, 20 pooled cycles per form, and 0 managed bytes in the warmed update measurement.
- TestChar Play Mode passed 15 form/quality/scale cases using the actual evolved models and their own Animators. The checks run after Animator/LateUpdate: live posed centre, vertex containment in the halo, warped vertex culling bounds, flash floor, moving-ball beam attachment, complete Cast Spell and 1x Recall timing.
- Darkness Spawn/Recall middle frames were inspected in Game/Scene view, including a top view of Recall. Preview completed and temporary resources were removed on exit.
- Director integration passed with the authored player: Throw/Pickup, physical flight/bounce, all three Cast Spell clips, queued switch during Spawn and latest-request wins during Recall.
- The dedicated combat Recall test passed for all three forms: moving opponent, target loss, last heading and command lock.
- SampleScene and TestChar scene file hashes remain identical to their pre-change values.

## Optimization review

- Sequence coroutines advance their own local clocks; visual updates run in LateUpdate after animation. Only initialization/finalization render immediately. This removes the repeated body/material/orbit/particle work previously done by both coroutine and LateUpdate every frame.
- A superseded coroutine checks its version immediately after yielding. It cannot advance or clean up the replacement's presentation.
- Orbit unit-circle points are cached by quality. Rings and funnel strands share one reusable point buffer. The straight recall beam uses two points and analytic ellipsoid contact.
- Posed vertex lists reuse capacity. Mesh fitting runs only while a presentation is active; no readable-mesh copies, material instances or particle arrays are created per frame. Warp culling bounds cover the original body-to-ball corridor, including delayed vertices.
- Frequently used shader property IDs and per-frame root transform/profile values are cached before the material loop.
- Recall cleanup restores culling and renderer state, but never rewrites Animator speed that it does not own. A destroyed source/overlay is skipped during cleanup so the ball and VFX still return correctly.
- A blocked queued landing is probed at most every 0.2 s, and physics waits share a cached WaitForFixedUpdate instead of creating one every flight tick.

Editor regression checks passed all forms/qualities, zero warmed-update managed allocation, replacement-coroutine isolation, external Animator-speed preservation, and cancellation after pet destruction. Runtime/Editor builds, Play Mode VFX, Director integration and combat Recall tests passed. Existing MCP connection/authorization errors remain outside this VFX change, so the overall Console is not error-free.

No scene/save-system source was changed. The first TestChar Play Mode runs used the game's existing autosave and therefore advanced save timestamps/playtime; temporary VFX pets never joined the saved party. The later combat check was backed up and its timestamp/playtime changes restored after confirming all gameplay fields were identical (pet-state records are compared by pet ID, independently of serialization order). Both the primary file and its backup matched their pre-combat copies after restoration.

## Ball visibility (2026-10-10)
Idle no longer instantiates a hand ball. Landing and trajectory checks use the active hand socket position and configured ball radius before visibility begins. An accepted Spawn shows the ball in the active hand and keeps the existing Throw/release timing. Recall shows it at the existing Pickup effect cue, then removes it immediately after the pet is withdrawn. Completion, rejection and cancellation cannot restore an idle hand ball. Director integration validation covers all three forms, queued switches, rejected landing, idle/accepted hand visibility, final Recall and cancellation cleanup.

