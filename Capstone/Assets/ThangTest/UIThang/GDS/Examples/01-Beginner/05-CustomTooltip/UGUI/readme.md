This example shows how to create a custom tooltip view.

Create a prefab with the required structure.
Add a script that extends the `BaseTooltipView` or one if its superclasses. Use the `Render` method to update the tooltip view.
Update the `TooltipPrefab` reference in `TooltipSystem` in the hierarchy with the new prefab.