# Dynamic-Spatial-Anchors-for-Meta-SDK-Unity
Solution for quickly destroying and re-creating Meta spatial anchors associated with Unity GameObjects positions in space

Once a Meta spatial anchor is created, it can't be moved. This solution deletes and re-adds a GameObject's associated SpatialAnchor, as well as re-serializes the list of anchors uuids. 

For the most relevant code, skip to on OnHandGrab(), and OnHandRelease() methods.

A common use case would be for when a user grabs and releases an object, respectivelly.

**Note** I used Odin Serializer for the serialization, if you prefer to use Unity serialization, simply modify the LoadSavedAnchorsUUIDs() and SaveAllActiveAnchorUUIDs() methods. 


