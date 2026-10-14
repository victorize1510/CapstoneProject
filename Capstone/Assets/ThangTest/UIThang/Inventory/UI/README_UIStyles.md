# UI markup and stylesheet conventions

## Ownership

| Asset | Responsibility |
| --- | --- |
| `MenuTheme.uss` | Inherited LiberationSans font and shared header/back values, with fallbacks in consumers |
| `MenuCommon.uss` | Shared controls: keycaps, actions, generic panel surfaces, dropdowns, health/EXP fills |
| `MenuScreenHeader.uss` | Shared screen title and Back geometry/states |
| `<Panel>.uss` | That panel's layout, typography and interaction states |
| `MonsterInventory.uss` | Composed document routing, template stacking and BAG evolution confirmation |
| `InventoryQuantityPopup.uss` | Quantity dialog attached to the document root at runtime |
| `<Panel>.uxml` | Semantic hierarchy, static text, bindings and stylesheet/template references |

Trainer Menu, Profile and MAP use uGUI. Their Canvas/RectTransform layout and
C# presentation code do not consume these stylesheets. BAG and INVENTORY are
the same screen, not two implementations.

uGUI font references are shared through `MenuTypography` and its Resources asset;
Map TMP uses the same LiberationSans source. Full-screen Back captions are
`←  TRỞ LẠI`, 15px, 146×42 design units, with a gold keyboard focus border.
Update both presentation systems together and validate the composed runtime
document, not only individual templates.

## Cascade and naming

- Load theme, common controls, panel styles, then the shared screen header.
- Apply `menu-theme` to each standalone document root, including templates that
  can also be previewed independently.
- Keep existing `name` attributes stable: runtime controllers query them.
- Use panel-prefixed classes for screen-specific rules. Add a shared class only
  when the control actually has the same contract across panels.
- Keep one property declaration per line and four-space indentation.
- Pseudo states and intentional descendant overrides may repeat a selector.
  Avoid appending a second copy of an entire panel to fix a single property.
- Decorative marks ignore picking; interactive controls retain picking/focus.
- Put static presentation defaults in USS. C# still owns data-dependent display,
  selected states, responsive classes, cooldowns and transactional button locks.
- Never use `!important` or browser `@media` syntax. UI Toolkit has its own
  supported USS properties and specificity rules.

Unity gives inline/C# styles higher precedence than stylesheet declarations;
this is why runtime visibility still overrides the BAG default hidden class.
See [USS selector precedence](https://docs.unity3d.com/cn/6000.0/Manual/UIE-uss-selector-precedence.html).

Shared header values use custom properties and explicit fallback values. See
[USS custom properties](https://docs.unity3d.com/es/2020.2/Manual/UIE-USS-CustomProperties.html).

## Verification

1. Import the changed UXML/USS assets in Unity and check parser diagnostics.
2. Preview the standalone panel and `MonsterInventory.uxml`; composition can
   change the cascade and visibility.
3. `Game Tools > GameToolThang > HUD > Validate UI Style Review` compares resolved
   styles/layout with the archived audit baseline at 1920x1080 and 1366x768.
   Review every difference; zero differences proves only the captured default
   properties, not every runtime state.
4. In TestChar Play Mode run `Validate Panel Lifecycle in Play Mode`, then inspect
   the populated screens. Do not use item consumption as a layout test.
5. For BOX viewport checks use `Preview BOX Viewport`; do not validate layout by
   matching exact whitespace in the USS source.

The initial migration backup, report and validation helper are under
`artifacts/ui-style-audit` at the repository root. The historical migration
recipe must not overwrite later reviewed theme/state edits.
