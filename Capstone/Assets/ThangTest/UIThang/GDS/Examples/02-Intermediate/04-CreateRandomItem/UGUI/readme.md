This example shows how to create random items from a predefined catalog.

You can create a custom catalog of item bases (`Create > SO > Core > ItemBaseCatalog`) or use the existing common item catalog (`GDS/Common/SO/Items/Catalog/Common_ItemBase`).
The controller listens to button click events, picks a random item base from the catalog, creates the associated item and adds it to the inventory. By convention, this should 
happen in a Store, but this example is too small to justify a custom one.