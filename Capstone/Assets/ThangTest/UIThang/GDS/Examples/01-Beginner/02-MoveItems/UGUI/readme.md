This example shows to move items between 2 inventories using ctrl+click.

To achieve this we create a custom store which takes into account the modifier keys (ctrl, alt, shift, etc.) when picking and placing items.
A controller is used to initialize the store with the 2 inventory references. Techincally, these could be part
of the store itself, but that would make it not reusable in other examples.