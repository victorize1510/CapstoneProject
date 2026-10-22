This example shows how to implement a "nested inventories" mechanic.

To achieve this we create a custom container item (`ContainerItemBase`) which has a `ListBag` with a predefined size.
The inventory is a regular `ListBag` with some extra behavior defined in `ContainerBagView`. Each inventory slot has an associated `ListBag` of its own (see `ConatinerBag > Content` in the hierarchy). 
When a slot changes, if the item is `ContainerItem`, its associated `ListBag` will also update.
