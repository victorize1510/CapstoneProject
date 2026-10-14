This example shows to create a basic equipment view with restricted slots (slots that accept a specific type of item).

The actual restriction happens by setting a tag both on the item and the slot. As long as the slot has a tag, it will only accept that have that particular tag.
To highlight the slot with valid/invalid colors, we add the `HighlightSlotBehavior` script to `DragAndDropSystem` game object. You can change the colors on individual slots in the hierarchy.

This example uses the same default setup inlcuding `DragAndDropSystem, TooltipSystem and DefaulSfx`. However, instead of the `DefaultStore` we use a custom Store from "SwapItems" example
which allows swapping items between slots. The inventory will show a "ghost" version of the slot, indicating its original position. If you drop the item on something other than a slot, 
it will return to its original position.