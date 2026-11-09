# Procedural Graphic System for Unity

A powerful, node-free procedural UI graphic generator for Unity. This tool allows you to create complex, point-based shapes (like paper cut borders, dynamic geometric forms, and animated UI elements) directly inside the Unity UI Canvas, replacing static textures and reducing memory overhead.

## Features
- **Procedural Graphic**: A custom UI Graphic component that draws polygons based on defined points.
- **Image to Shape Wizard**: Right-click any Sprite or Texture to extract its contour and convert it into a Shape Preset.
- **Shape Presets**: Save and load polygon configurations. Supports multiple keyframes for shape morphing.
- **Procedural Graphic Animator**: Animate smoothly between different Shape Preset keyframes using DOTween.
- **Paper Cut Border Generator**: Automatically proceduralize paper-tear style borders on your UI.

## Dependencies
- [Odin Inspector](https://odininspector.com/): Required for advanced inspector UI and editor tools.
- [DOTween (Pro/Free)](http://dotween.demigiant.com/): Required for the Procedural Graphic Animator to morph shapes smoothly.

## How to use
1. **Create a Shape Preset**: Right click in the project window `Create > UI > Shape Preset`.
2. **Auto-Extract from Image**: Go to `Tools > Procedural Graphic > Image to Shape Preset`, select an image and generate a shape based on its alpha contour.
3. **Use in UI**: Add a `Procedural Graphic` component to any Canvas GameObject. Assign your Shape Preset to it.
4. **Animate**: Add a `Procedural Graphic Animator` and call `PlayAnimation()` to morph between shape keyframes!
