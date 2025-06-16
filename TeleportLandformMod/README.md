# Landform Teleport Mod

This mod adds a simple chat command `/tpl <landform>` that teleports the player to the nearest chunk containing the specified landform.

The implementation scans nearby chunks using the server's landform map and moves the calling player if a match is found.

> **Note**: The search code uses API classes available in the game. Adjust the `FindNearestLandform` logic if the underlying API changes.
