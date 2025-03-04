![GnF_BannerTop](https://github.com/user-attachments/assets/030bb59a-c8dd-4dd5-80a7-5b848d91b9d7)

A comprehensive solution for rendering grass and fur in Unity Engine.\
Created by Matej Vanco (2024-2025), https://matejvanco.com (MIT License).

**Grass and Fur** is a complete shell-textured rendering solution that enables you to create grass, fur, and partial hair on any mesh - both static and skinned. It includes a variety of features such as support for skinned meshes, motion vectors, a painting editor, styling tool, masking, coloring, wind constraints, radial interactions, explosions, several optimizations, and more.

[![Discord](https://img.shields.io/discord/778036261733531688?label=Discord&logo=discord&color=5865F2&style=for-the-badge)](https://discord.gg/WdcYHBtCfr)
[![My Portfolio](https://img.shields.io/badge/Portfolio-MatejVanco.com-4c1d9f?style=for-the-badge&logo=react&logoColor=white)](https://www.matejvanco.com/)
[![Follow Me on X](https://img.shields.io/badge/Follow_Me_On_X-@matej_vanco-1DA1F2?style=for-the-badge&logo=twitter&logoColor=white)](https://x.com/matej_vanco)

![GnF_Preview-ezgif com-optimize](https://github.com/user-attachments/assets/31633b3b-0d0f-4856-9792-57e9e30162d8)

### Features
- Supports both static and skinned meshes  
- User-friendly painting editor  
- Readable API with useful utilities  
- Deterministic radial interactions (push, pull, explosions)  
- Customizable color, mask, and style textures  
- Advanced shell settings (curvature, gravity, local motion vectors)  
- Realtime point, spot, and directional light support (per-pixel/per-object)  
- Traditional Lambertian shading model with cascaded shadow map sampling  
- Various optimization techniques (distance-based culling, frame downsampling)
- GPU accelerated 
- URP Batcher compatible  
- URP fog compatible  
- Supports both Forward and Deferred rendering paths  
- Production-tested  
- Includes example content  
- Open-source!

### Technical Compatibility
- **Supports Unity URP only (yet!)**
- No WebGL support
- No GPU Instancing
- No depth/shadow writing
- No specular highlights/reflections/SSS
- No physics and rigidbody detections
- Not suitable for hair simulation or individual hair strand rendering  
- Not compatible with Forward+ rendering path
- No advanced frustum culling
- No Gi

### Notice
- Not tested on Vulkan/OpenGL (optimized for DirectX/Metal)
- Not tested in VR (should work, but unverified) 
- Minimum supported Unity version is 2022 (2021 might work but is deprecated)
- Not suitable for large scenes and many meshes
- Meshes using Grass And Fur must be properly unwrapped

# Projects Using Grass And Fur
- Hulda (first person action game)\
![GnF_Game_Hulda-ezgif com-optimize](https://github.com/user-attachments/assets/537e559c-834f-4d44-adc6-d25cef30429d)

- Wanderers (roguelite scifi action game)\
![GnF_Game_Wnd-ezgif com-optimize](https://github.com/user-attachments/assets/80e915bc-10b4-4af6-8fcf-e5fe0b972dba)

---

# Content
1. [Setup](#setup)
2. [How To Use](#how-to-use)
    - [Grass And Fur Master Component](#grass-and-fur-master-component)
    - [Painting](#painting)
    - [Grass and Fur Material](#grass-and-fur-material)
    - [Interactions](#interactions)
3. [Optimizations](#optimizations)
     - [Downsampling](#downsampling)
     - [Distance-Based Fadeout](#distance-based-fadeout)

# Setup  
1. Select a GameObject that has either a **Mesh Filter** or a **Skinned Mesh Renderer** (ensure the mesh source has **Read/Write Enabled**).  
2. Add the `GrassAndFurMaster` component to the GameObject.  
3. In your project, create a new material using the `Grass And Fur Shader`.  
4. Assign this material to the `Target Grass And Fur Material` field in the `GrassAndFurMaster` component.  
5. Done.

# How to Use  
Grass and Fur provides various options to control the final appearance and behavior of your grass/fur.\
You can modify these settings in two places:  

- The **Grass And Fur Master** component  
- The **Grass And Fur** material

## Grass And Fur Master Component  
The **Grass And Fur Master** component is the main controller for grass and fur behavior. It includes two key parameters that influence the overall look:
- `Density` determines how many shell layers (chunks) are generated for the mesh. Higher values create a denser effect but increase performance costs.  
- `Offset` defines the spacing between shell layers. Adjust this based on your mesh’s scale for optimal results.  

Since different meshes have unique scale matrices, you may need to tweak these values accordingly.  

### Global Modifiers  
The component includes a `Global Modifier Settings` container, which controls the tracking feature. These settings adjust how the system interacts with external forces and influences.  

### Downsampling Optimization  
The `Downsample` feature allows you to optimize performance by controlling how the system renders frames. When enabled, you need to assign a compatible **Renderer Feature** from your pipeline asset to ensure proper functionality. More details on optimization techniques are covered below...  

### Skinned Mesh Support  
If your mesh is a **Skinned Mesh**, an additional **Skin Settings** panel becomes available. This panel allows you to:  
- **Rescale the generated shell mesh** to better fit the skinned model.  
- **Enable motion vectors**, allowing the fur or grass layers to react dynamically to the movement of skinned vertex motions.  

## Painting  
The painting system allows you to control where grass or fur appears on your mesh, how it is styled, and how it looks overall. The painting editor is intuitive, so this section won’t cover its usage in detail. Shortcut keys are displayed within the editor.  
There are three painting modes:  
- **Mask Painting**  
- **Add Color (Albedo) Painting**  
- **Style Painting**  

### Mask Painting  
Mask painting defines which areas of the mesh should have grass or fur. It works by painting black/white values.  
- To start, click **Paint Mask Texture**, or load an existing mask texture.  
- Once painted, you can save the mask texture (along with other painted textures) as an asset for future reference.  

### Add Color (Albedo) Painting  
This mode allows you to apply additional colors to the fur/grass.  
- If the painted colors don’t appear as expected, check your material settings and adjust how the shader handles the `Add Albedo` properties.  

### Style Painting  
Style painting shifts the UVs of individual shell layers, creating a more organic look.  
- This feature must be enabled to use a **Style Texture** in your material.  
- The amount of UV shifting and its effect on the mesh can be adjusted within the material settings.  

![GnF_Painting-ezgif com-optimize](https://github.com/user-attachments/assets/90ab5e05-8a0b-48de-84d0-3bad4daf4ddc)

> [!TIP]
> It is **highly recommended** to save painted textures as assets. Saving them within the scene increases scene size and does not create a reusable reference in the project. Once you're satisfied with your painted textures, be sure to **save them as assets**.

## Grass and Fur Material  
The material settings provide various options for fine-tuning the appearance and behavior of grass and fur. Most properties are self-explanatory, but this section covers key settings that may require additional clarification.  

### Add Albedo Texture  
The **Add Albedo Texture** is an additional color texture that can be manually assigned or automatically set by the **GrassAndFurMaster** component when using painted textures.  
- The **Albedo Transition** parameter interpolates color from the lowest shell layer to the highest, creating a smooth gradient effect.  
- To reverse this transition, enable **Invert Shell Offset Add Albedo**.  
- Instead of adding the additional color, you can choose to multiply it by enabling the corresponding option.  

### Styling and Realtime Tracking Feature  
This section includes two features:  
- **Style Texture** – Disabled by default; must be manually enabled to apply a **Style Texture** for more organic variation.  
- **Realtime Tracking Feature** – Allows radial interaction with the fur/grass chunks. More details on this feature are covered in the **Interactions** section.  

### Shell Settings  
The **Shell Settings** panel controls how shell layers (chunks) are generated based on depth. Most parameters should be straightforward, but feel free to reach out if anything is unclear.  

### Planar Explosion Feature  
Enabling the **Planar Explosion Feature** allows grass or fur (typically grass) to dynamically react to explosion events.  

## Interactions  
There are two primary methods for interacting with grass and fur:  
1. **Radial Interactions (Realtime Tracking Feature)** – Requires a world position and a world-space radius to influence the grass/fur dynamically.  
2. **Radial Explosions (Planar Explosion Feature)** – Uses an explosion data container to simulate an explosive force affecting the grass/fur.  

Example scripts demonstrating these interactions are included in `ExampleContent/Scripts/Extras/Modifiers`  

### Enabling Interactions  
To use these interaction features, they must be **manually enabled** in the material settings:  
- For **Realtime Tracking Feature**, enable the `Use Realtime Tracking Feature` property in the material.  
- For **Radial Explosions**, enable the `Use Explosions Feature` property in the material.  

Shader will allocate additional uniforms to properly handle the enabled interaction features.

![GnF_Interactions-ezgif com-optimize](https://github.com/user-attachments/assets/0aa51900-b1f5-47e2-a9b1-01d673661690)
![GnF_Interactions2-ezgif com-optimize](https://github.com/user-attachments/assets/2375f94d-484e-46da-846f-cffff521e194)


# Optimizations  
Grass and Fur includes two key optimization techniques to help improve performance, especially when rendering in large scenes or handling multiple interactions per frame.  

## Downsampling  
The **Downsample Feature** reduces performance costs by rendering the GnF content at a lower resolution. It does this by:  
1. Downsampling the current frame. 
2. Drawing the Grass and Fur content into a lower-resolution texture. 
3. Blitting the texture back onto the screen.

In order to use the downsampling feature, select your universal render asset and add `Grass And Fur RF Downsampler`.
> [!WARNING]
> Since the **downsampled** Grass and Fur shader does not support **depth writing**, some third-party post-processing effects (such as volumetric lighting or dof) may not function correctly. This does not apply to non-donwsampled frame.

![GnF_Opti01-ezgif com-video-to-gif-converter](https://github.com/user-attachments/assets/7ed4bf48-c100-4ced-b616-f59ad0cca2f9)

## Distance-Based Fadeout  
The **Distance-Based Fadeout** feature gradually fades out fur/grass layers based on the camera's distance. This can be enabled in the material settings and helps optimize performance by:  
- Preserving high detail when the camera is **close** to the mesh.  
- Reducing unnecessary rendering when the camera is **far** from the object.

This option can be found on your GnF material under **Shell Settings** header.

![GnF_Opti02-ezgif com-optimize](https://github.com/user-attachments/assets/d5a2e976-9744-4ea5-894e-d212df52bb80)

