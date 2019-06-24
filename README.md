# RIMMSAssemble

This mod allows other assembly mods to load referenced assemblies even if their version has changed.

When rimworld releases an update no mod has to be recompiled to work with the new assembly version. This is due to a custom AssemblyResolver, however that resolver does not work for anything other than the main rimworld assembly. This mod corrects the assembly resolving thus increasing stability for mod lists and allows truly independant development between mods.

In order for an assembly to be correctly loaded it must avoid being loaded by the base game. Thus the normal file extension of ".dll" must be changed to ".ass". Now the mod will load the assembly and take over the creation of the mod classes to ensure the load order is kept.

C# solutions can achieve the automatic renaming by adding post build instructions. These (windows) instructions first remove old assemblies and then rename the new assemblies file extension.
- del *.ass
- rename "MyAssemblyName.dll" *.ass

For the most part this mod will only be necessary if another mod adds it as dependancy, however it can also be used by manually renaming a mods assembly if the game log reports a ReflectionTypeLoadException error. If that error doesn't happen before any mod is loaded it might be enough to just have this mod present.

## Load Order
As early as possible, since only mods below it can be handled correctly.

## Disclaimer
Any resources based on or referncing rimworld resources are also subject to rimworlds eula agreement. This includes images, audio but also decompiled sections repeated in this mod. If the origin is unclear it must be assumed that the rimworld eula applies.
