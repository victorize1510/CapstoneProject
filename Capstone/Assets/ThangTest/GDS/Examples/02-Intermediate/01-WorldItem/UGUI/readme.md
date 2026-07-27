This example shows how to spawn items in the world when dropping them from the inventory.

It uses a custom store (`WorldItem_Store`) which listens to "drop-outside-UI" events as well as "pick-world-item" events.
The actual spawning and despawning is handled in `SpawnWorldItem` script. It is attached to the `DragAndDropSystem` game object.
You can specify the interactible layer in `DragAndDropSystem` script. The blocking layer will prevent the item spawning event from trigerring (see "Basic Sandbox" demo).

The item spawns inside a wrapper prefab, with some default behavior, like spin around Y axis and highlight on hover.
If the item has a prefab associated (see `PrefabItem`), it'll spawn the prefab, otherwise it'll spawn the item icon as a "billboard".

Picking an item spawned like this from the world will add it back to the inventory.