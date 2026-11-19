# Red screen (full-screen shader)

While Conseraph is enraged the whole view turns bright red. It is a full-screen shader, not a Volume effect, so it can grow into the later look where every object gets a red outline and everything else goes black.

**Files:** `Assets/Shaders/RageScreen.shader` (`Custom/Rage Screen`); `Assets/Scripts/Conseraph/Conseraph.cs` (sets the amount).

**Set up (two things, by hand):**
1. **Create > Material**, name it `rage_screen`, and set its **Shader** to **Custom > Rage Screen**.
2. Select `Assets/Settings/PC_Renderer`, choose **Add Renderer Feature > Full Screen Pass Renderer Feature**, set **Pass Material** to `rage_screen`, and leave **Injection Point** on After Rendering Post Processing.

If either is missing nothing breaks, the screen just doesn't go red.

## How it works
- The renderer feature draws one full-screen triangle with the material after post-processing, reading the finished camera picture as `_BlitTexture` (from URP's `Blit.hlsl`).
- For each pixel: `luma = dot(color.rgb, (0.299, 0.587, 0.114))`, `red = _Tint.rgb * saturate(luma * _Gain)`, and the result is `lerp(color, red, _RageAmount)`. So the picture turns red with light staying light and dark staying dark. **Red** (`_Tint`) and **Brightness** (`_Gain`) are on the material.
- **Where to change the look:** the numbers in the shader's `Properties` (Brightness 1.8, Red 1, 0.02, 0.02) are only the defaults for a *new* material. The material in the project, `Assets/Materials/Rage.mat`, has its own saved values (Brightness 0.71, Red 1, 0, 0), and those are what the game uses, so editing the shader file changes nothing for it. Edit the material in the Inspector instead, or use the slider below.
- **Rage Intensity (on Conseraph):** a slider from 0 to 1 (default 1) in his Enraged section: how red the screen gets at the most. `_RageAmount` is sent as `rage * rageIntensity`, so at 1 the whole picture turns red, at 0.5 the final picture is half the real picture and half red, and at 0 there is no red at all. The fade in and out still takes `redFadeSeconds`, it just ends at that lower level. It is sent every frame, so you can drag it during Play mode with the screen red to tune it live. (It is a blend, not a darkening: for a dimmer red change **Brightness** or **Red** on `Rage.mat`.)
- `_RageAmount` is a **global** shader float (0 to 1). `Conseraph` moves it toward 1 while enraged and back to 0 afterwards, over `redFadeSeconds`, with `Shader.SetGlobalFloat`. No material is changed at runtime, so nothing in the project is edited by playing. At 0 the picture passes through unchanged.
- It runs after post-processing, so it also tints Conseraph himself (he is part of the picture). The HUD, the timer and the burn overlay are Screen Space - Overlay canvases, drawn after the camera, so they stay normal.
- Cost: one full-screen blit every frame, even at `_RageAmount` 0.

## Growing it into the outline look later
The renderer feature has a **Requirements** setting. Adding **Depth** (and **Normal**) makes the depth and normal textures available to the shader (`PC_RPAsset` already has the depth texture turned on). Edge detection compares neighbouring depth or normal values, paints the edges red, and multiplies everything else toward black. That is a change to this same shader and renderer feature, not a new system. A Volume effect like Color Adjustments can only tint colours, which is why this was built as a shader.
