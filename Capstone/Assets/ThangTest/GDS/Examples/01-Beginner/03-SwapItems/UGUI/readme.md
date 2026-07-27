This example demonstrates how to implement a swap items behavior inside the inventory or between inventories.

To achieve this we create a custom store which updates the source and target bags only on valid swap actions.
On drag, the bag views will show a "ghost" version of the item in its original position (slot).
If a swap action is illegal, the dragged item will return to its original position.
When swapping stackable items (of the same type), they will stack up to the configured maximum (on the item base) and the remainder will return to its original slot.