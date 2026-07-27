To create an inventory, right click on a Canvas in hierarchy and select `GDS > ListBag`.
Alternatively, you can drag the `ListBag` prefab from `GDS/Core.UGUI/Views` into the hierarchy.
All "bag" views require a Store in the scene. You can add the default Store to the hierarchy by selecting `GDS > DefaultStore` in the hierarchy context menu.
The default store contains the functionality which updates the inventory on drag and drop.
Add the drag and drop system from the context menu (`GDS > DragAndDropSystem`)

Select the ListBag in the hierarchy, expand the ListBagView script and set the preferred inventory size (`Data.Slots`) 
To update the view in edit mode, click `Generate slot views` button.

With `ListBagView script` selected, populate the slots with test items from `GDS/Common/SO/Items`.
At this point you should be able to move items inside the inventory

You can add the default tooltip and sfx from the same context menu (`GDS > TooltipSystem` and `GDS > DefaultSfx`)