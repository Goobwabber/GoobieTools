# GoobieTools
A random assortment of tools I use for creating avatars with NDMF/VRCFury.

These tools shouldn't cause import errors if you are missing either NDMF or VRCFury so don't worry about it!


## Tools
- AAOPatch: This is an NDMF plugin that fixes bone translation animations when AAO changes the parent of bones. It ensures that animations will play with the user's desired offsets.
- VrcfNdmfResolver: This is both an NDMF plugin and VRCSDK hook that will fix animation bindings for animators in VRCFury 'Full Controller' components. This means that NDMF tools won't break their animations.
- BoneFixer: This is a very simple tool which will fix a mesh disappearing when the bone order from the fbx doesn't match the mesh renderer's previous bone order. This tool is very simple and wont reparent things/do anything fancy. Its intended for times when the mesh has changed but the armature *should* be identical.
