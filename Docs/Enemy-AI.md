# Enemy AI: retaliation and shared skills

## Dùng nhanh trong Unity

Mở **DemoScene 1** và Play. Gọi pet rồi ra lệnh đánh trúng quái: quái chỉ phản công sau khi mất HP, không tự gây chiến khi bạn đi ngang. Đổi pet thì quái chuyển sang pet mới của cùng đội sau khi gọi thành công; thu hết pet thì quái về nhà. Quái đang quay về vẫn nhận damage và có thể bị bắt.

Chọn quái → **Basic Creature Enemy** để chỉnh `patrolRadius`, `leashRadius`, `useSkills`, `skillChance`. Skill, icon, VFX và sound vẫn lấy từ **Pet Hud Runtime Stats → Species Skill Set** và **Creature Visual Binding → Animation Profile / Sockets**. AI không có một danh sách skill riêng cần nhập lại.

Đã gắn cấu hình cho encounter hiện có trong DemoScene 1. Menu **Tools → Capstone → PvE → 8** chỉ cần dùng lại khi authoring thêm; các lệnh 9–11 là kiểm thử tách biệt trên ổ E. Mở menu đầy đủ sẽ dừng gameplay, còn bảng P/G trên HUB tiếp tục dùng hành vi riêng hiện có.

The authored example is `Capstone/Assets/Scenes/DemoScene 1.unity`. The original DemoScene is preserved. NPCs and bosses are outside this implementation.

## Behaviour

- Enemies stay neutral until an owned pet actually removes HP. Walking close, aiming or missing an attack does not provoke them.
- Retaliation belongs to the attacking pet's owner/team. A newly summoned pet from that team becomes the target after its summon completes. Withdrawing every pet makes the enemy return home.
- A pending switch is given up to 12 gameplay seconds. The enemy waits and cannot hit an unrevealed pet.
- Enemies attack pets only. There is no player-damage route here.
- Basic attacks and equipped skills share the existing stat, mitigation, crit, cooldown, animation, projectile, VFX and audio systems. Enemy AI chooses actions; it never invents damage or skill data.
- Returning enemies can still take damage and be captured. Another real hit can provoke them again if a summoned target is inside their territory.
- After reaching home and spending 8 safe seconds, enemies recover 10% maximum HP per second. The existing downed/capture, Die, smoke and fresh-respawn lifecycle remains in `EncounterSpawnPoint`.

## Authoring an enemy

Use a runtime prefab under `Assets/Game/CreatureSystem/Runtime`, keeping original imported models/controllers separate.

Required components:

1. `DummyEnemy`: health, defeat/downed/capture compatibility. Keep `autoRevive` off for encounter-owned actors.
2. `PetHudRuntimeStats` and `PetCollectionMetadata`: real level, form, stats and equipped skill IDs.
3. `CreatureCombatant`: unified damage and a fitted capsule. Owned pet hurtboxes may be triggers; projectiles include actor triggers but ignore interaction triggers.
4. `CreatureAnimationDriver` and the visual's `CreatureVisualBinding`: map `Idle`, `Move`, `Attack`, `Hit`, `Death` and skill IDs to real Animator states. Missing Stun uses Idle. Missing Attack cancels safely; it does not create timer-based damage.
5. `BasicCreatureEnemy` and `NavMeshAgent`. The brain creates/reuses its shared action context and driver once at startup.
6. `EncounterSpawnPoint` on a separate home object for death/capture respawn. Assign the runtime encounter prefab and smoke/stun effects. The owner copies its AI settings to replacements.

For catchable wildlife, use `WildPetCaptureTarget` and a PetController marked as a wild encounter. Ordinary enemies are not made capturable by the AI.

## Inspector settings

| Field | Default | Purpose |
| --- | ---: | --- |
| `patrolRadius` | 3.5 m | Neutral patrol; 0 means stand still |
| `leashRadius` | 18 m | Home territory when no collider is assigned |
| `territory` | optional | Trigger volume defining the permitted territory |
| `decisionInterval` | 0.3 s | Target and action decisions |
| `pathInterval` | 0.25 s | Minimum path-request interval when destination/path needs updating |
| `searchSeconds` | 2 s | Last-seen search before disengaging |
| `useSkills` | true | Use actual equipped skills in addition to basic attacks |
| `skillChance` | 0.35 | Chance to try an eligible skill when ready for a new action |

The AI alternates eligible skills and respects unlocks, cooldown, distance and line of sight. It skips an active offense buff. Failed or incomplete paths do not use transform movement or long-distance warps. A nearby NavMesh recovery is limited to 0.75 m horizontally and 0.5 m vertically.

The legacy `aggroRadius`, `hitDelay` and `attackDuration` fields remain serialized for compatibility. `aggroRadius` no longer causes proximity aggression. A basic hit releases once at 36% of its actual animation and recovers at 85%.

## Full-menu pause

`GameMenuController` owns `GameplayPause` while a full menu is open. Nested owners are supported. Gameplay time, actors, casts, cooldown, projectiles, regeneration and lifecycle timers pause; UI and 2D music continue. P/G transient HUD selectors are not pause owners.

Runtime sequences that previously needed unscaled time use `GameplayPause.PlayTime`, `PlayDelta` and `WaitForSeconds` so opening a menu does not finish or time out a summon/capture presentation in the background. Resume restores the previous time scale. Destroyed/disabled menu owners release their pause token.

## Verification commands

With DemoScene 1 stopped, use `Tools → Capstone → PvE`:

- **8. Apply retaliation and skill AI (DemoScene 1)**: explicit authoring, backup on E, scene-original hash check, and runtime owned-pet hurtboxes. No import-time authoring hook.
- **9. Verify retaliation skills and menu pause**: actual basic/ranged damage, menu transitions, team targeting and withdrawal.
- **10. Verify navigation lifecycle and 20-enemy budget**: temporary NavMesh/wall/actors, territory, missing paths and a short ProfilerRecorder sample.
- **11. Verify wild buff capture respawn and owned skills**: wild buff, return damage/capture, downed/menu/Die/smoke/respawn and the four owned dragon skills.

These checks use a temporary additive arena and isolated save stores on E. Nothing is baked into the scene. Each run has a 150-second limit and exits Play. They invoke real runtime APIs; they do not certify manual keyboard/mouse feel or standalone performance.

Reports and useful final images: `E:/UnityWork/CapstoneProjectData/Artifacts/enemy-ai-retaliation-skills-2026-11-16/`.
