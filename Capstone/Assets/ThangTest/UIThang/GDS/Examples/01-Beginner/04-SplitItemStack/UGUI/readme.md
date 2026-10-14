This example shows how to add a split stack behavior using shift+click.

To achieve this we create a custom store which takes into account the modifier keys (ctrl, alt, shift, etc.).
Picking a stackable item while holding shift, will split it, half of it (rounded down) remaining in place, and the remainder transferred to the "ghost" item (dragged item).
Placing a dragged stackable item while holding shift will transfer one from ghost to target slot.