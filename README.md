# LiquidSurface
![preview-mesh](preview-mesh.gif)
<br>

## Description
Small experiment that generates an optimal triangle surface for vertex animation. <br>
It tries to optimize calculations by using a shader to handle the spring physics (for paralell gpu processing). <br>
Outputs a RenderTexture that can be plugged into custom materials as height map. <br>
<br>

## Installation
- Copy the two shaders and the csharp script somewhere into your **Unity** project hierachy
- Add liquidsurface.cs as a component to an empty game object; it will generated the mesh for you (values adjustable)
- Create a custom shader (or graph); use _LiquidSurface texture to get height (r) and velocity (g); attatch shader material to the mesh
- The script has a testing loop attatched by default. Click on the surface to trigger an impact. Remove from update loop if testing environment is not needed
<br>

## Tips for Shader
![preview-mesh](preview-shader.gif) <br>
The spring height map only handles up and down. <br>
To also include vertex offset into other directions (and calcualte the normal map) you will need to look up it's pixel neighbours. <br>
Below is an example on how a shadergraph like that could look like. <br>
![preview-mesh](graph.png)
<br>

## Notes
- Validate warnings can be ignored
- Might be slightly optimizable with compute shaders or tesselation
- Too high values make the shader freak out (especially spread)
- Could also be used for snow by turning spring forces way down
- Keep in mind each surface has it's own rendertarget and calculations so use sparingly
- This setup only works on newer Unity versions since it uses the newer Mesh API which was introduced in Unity 2020
