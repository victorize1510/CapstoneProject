This example demonstrates a way to implement tabs of inventories.

It shows the "player inventory" on the right and "player stash" on the left.
It uses the `MoveItems` store to allow moving items between stash and inventory via ctrl+click.

Unity does not provide a built in Tabs component. TabsView prefab and script (`GDS/Core.UGUI/Views/TabsView`) offer a sample implementation of such a component.
The "stash" game object has a TabsView script attached. It uses a `ListBagCollection` to keep a reference to the currently selected bag and its tab.

The tabs are implemented using Unity's `Toggle` and `Toggle Group` components.