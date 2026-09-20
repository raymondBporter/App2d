● Blender file:
Animations are split by actions. Animation frame ranges are stored in custom properties. More information here: https://www.keviniglesias.com/animationBlenderFiles.html

Deforming bones are stored in the first bone layer (enabled by default).
Controller and helper bones used for animation are placed in the third and fourth layers.

The B-root bone is a deforming bone, even though it has no vertex weights assigned.
This is necessary for B-root to be included when using the "export only deforming bones" option.

For Unity, exporting the armature node ('Rig') is recommended.
For Unreal, Godot, and other game engines, skipping the armature node ('Rig') is recommended.

B-spineProxy is a deforming bone that shares the same animation data as B-spine, but is not its sibling in the hierarchy. This setup is useful for mixing animations in game engines that use transform inheritance. It can be ignored, disabling its deformation will not affect the animation. More information:
https://www.keviniglesias.com/spine-proxy.html

Thank you for downloading and using my assets!

● License:
Standard Asset Store EULA (Unity Asset Store, Gumroad, itch.io)
    - Royalty-free and allowed for commercial use.
    - Resale not allowed.
    - Optional attribution (credits).
    
More license details:
https://www.keviniglesias.com/#license

● Support & Feedback:
support@keviniglesias.com

www.keviniglesias.com