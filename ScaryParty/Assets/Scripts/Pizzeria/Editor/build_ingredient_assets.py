import os
import re
import shutil

ROOT_DIR = os.path.abspath(os.getcwd())
ASSETS_DIR = os.path.join(ROOT_DIR, "Assets")
PREFABS_DIR = os.path.join(ASSETS_DIR, "Prefabs", "Pizzeria")
VISUALS_DIR = os.path.join(PREFABS_DIR, "Visuals")
MATERIALS_DIR = os.path.join(PREFABS_DIR, "Materials")
PROFILES_DIR = os.path.join(ASSETS_DIR, "ScriptableObjects", "Pizzeria", "Profiles")
RESOURCES_DIR = os.path.join(ASSETS_DIR, "Resources")

os.makedirs(VISUALS_DIR, exist_ok=True)
os.makedirs(MATERIALS_DIR, exist_ok=True)
os.makedirs(RESOURCES_DIR, exist_ok=True)

# 1. Create Materials
MAT_MUSHROOM_GUID = "b8295d950f534549b41ccd7d5f0332fd"
MAT_CONTAINER_GUID = "8f7e5d4204584c7b80b6097038c93fd4"
MAT_DOUGH_GUID = "2bbfa83874c78c647923da3168831094"
MAT_SAUCE_GUID = "0c390cbcdd569d245adc4f2fff8664b9"
MAT_CHEESE_GUID = "d894b96d59de8874fb62f39671fb084b"
MAT_CHEESE_RALADO_GUID = "022cda3f92304c241a81a440ee69e820"
MAT_CALABRESA_GUID = "1b19510320d73b04d9a127f20b4c7a91"

def write_material(name, guid, r, g, b, metallic=0.0, smoothness=0.5):
    mat_path = os.path.join(MATERIALS_DIR, f"{name}.mat")
    meta_path = mat_path + ".meta"
    content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &-6624734433294366383
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion
  version: 10
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap:
    RenderType: Opaque
  disabledShaderPasses:
  - MOTIONVECTORS
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _Metallic: {metallic}
    - _Smoothness: {smoothness}
    m_Colors:
    - _BaseColor: {{r: {r}, g: {g}, b: {b}, a: 1}}
    - _Color: {{r: {r}, g: {g}, b: {b}, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""
    with open(mat_path, "w", encoding="utf-8") as f:
        f.write(content)
    with open(meta_path, "w", encoding="utf-8") as f:
        f.write(f"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

write_material("Mat_Mushroom", MAT_MUSHROOM_GUID, 0.82, 0.76, 0.66)
write_material("Mat_IngredientContainer", MAT_CONTAINER_GUID, 0.85, 0.85, 0.88, metallic=0.6, smoothness=0.7)

print("Materials created.")

# Helper to write visual prefabs
def write_prefab(filename, guid, root_id, yaml_body):
    prefab_path = os.path.join(VISUALS_DIR, filename)
    meta_path = prefab_path + ".meta"
    with open(prefab_path, "w", encoding="utf-8") as f:
        f.write(yaml_body)
    with open(meta_path, "w", encoding="utf-8") as f:
        f.write(f"fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    print(f"Written prefab: {filename}")

# 1. Visual_Ingredient_Dough
write_prefab("Visual_Ingredient_Dough.prefab", "6b2d4dc49c2e46c0822bdfb28876be15", 1000000001, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000000001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000002}}
  m_Layer: 0
  m_Name: Visual_Ingredient_Dough
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000001}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: 1000000004}}
  - {{fileID: 1000000006}}
  - {{fileID: 1000000008}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000003
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000004}}
  m_Layer: 0
  m_Name: GripPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000004
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000003}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000002}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000005
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000006}}
  m_Layer: 0
  m_Name: SupportPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000006
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000005}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -0.075, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000002}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000007
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000008}}
  - component: {{fileID: 1000000009}}
  - component: {{fileID: 1000000010}}
  m_Layer: 0
  m_Name: DoughMesh
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000008
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000007}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0.22, y: 0.15, z: 0.22}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000002}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000009
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000007}}
  m_Mesh: {{fileID: 10207, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000010
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000007}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_DOUGH_GUID}, type: 2}}
""")

# 2. Visual_Ingredient_MassaAberta
write_prefab("Visual_Ingredient_MassaAberta.prefab", "50330bb3be2c46468a973efbce8e1862", 1000000011, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000000011
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000012}}
  m_Layer: 0
  m_Name: Visual_Ingredient_MassaAberta
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000012
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000011}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: 1000000014}}
  - {{fileID: 1000000016}}
  - {{fileID: 1000000018}}
  - {{fileID: 1000000020}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000013
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000014}}
  m_Layer: 0
  m_Name: GripPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000014
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000013}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000012}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000015
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000016}}
  m_Layer: 0
  m_Name: SupportPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000016
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000015}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -0.008, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000012}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000017
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000018}}
  m_Layer: 0
  m_Name: IngredientLayerAnchor
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000018
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000017}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0.01, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000012}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000019
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000020}}
  - component: {{fileID: 1000000021}}
  - component: {{fileID: 1000000022}}
  m_Layer: 0
  m_Name: BaseDisk
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000020
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000019}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0.36, y: 0.008, z: 0.36}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000012}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000021
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000019}}
  m_Mesh: {{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000022
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000019}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_DOUGH_GUID}, type: 2}}
""")

# 3. Visual_Ingredient_Sauce
write_prefab("Visual_Ingredient_Sauce.prefab", "6f00d9b5be2f47818c86b953a27da0d4", 1000000023, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000000023
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000024}}
  m_Layer: 0
  m_Name: Visual_Ingredient_Sauce
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000024
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000023}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: 1000000026}}
  - {{fileID: 1000000028}}
  - {{fileID: 1000000030}}
  - {{fileID: 1000000034}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000025
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000026}}
  m_Layer: 0
  m_Name: GripPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000026
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000025}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0.05, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000024}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000027
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000028}}
  m_Layer: 0
  m_Name: SupportPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000028
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000027}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -0.12, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000024}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000029
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000030}}
  - component: {{fileID: 1000000031}}
  - component: {{fileID: 1000000032}}
  m_Layer: 0
  m_Name: JarBody
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000030
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000029}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0.12, y: 0.12, z: 0.12}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000024}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000031
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000029}}
  m_Mesh: {{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000032
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000029}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_SAUCE_GUID}, type: 2}}
--- !u!1 &1000000033
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000034}}
  - component: {{fileID: 1000000035}}
  - component: {{fileID: 1000000036}}
  m_Layer: 0
  m_Name: JarLid
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000034
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000033}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0.13, z: 0}}
  m_LocalScale: {{x: 0.06, y: 0.03, z: 0.06}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000024}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000035
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000033}}
  m_Mesh: {{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000036
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000033}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_CONTAINER_GUID}, type: 2}}
""")

# 4. Visual_Ingredient_Cheese
write_prefab("Visual_Ingredient_Cheese.prefab", "f06d4e9f6b854869ad28f749c0b28d84", 1000000037, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000000037
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000038}}
  m_Layer: 0
  m_Name: Visual_Ingredient_Cheese
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000038
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000037}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: 1000000040}}
  - {{fileID: 1000000042}}
  - {{fileID: 1000000044}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000039
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000040}}
  m_Layer: 0
  m_Name: GripPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000040
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000039}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000038}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000041
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000042}}
  m_Layer: 0
  m_Name: SupportPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000042
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000041}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -0.05, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000038}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000043
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000044}}
  - component: {{fileID: 1000000045}}
  - component: {{fileID: 1000000046}}
  m_Layer: 0
  m_Name: CheeseBlock
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000044
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000043}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0.18, y: 0.10, z: 0.12}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000038}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000045
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000043}}
  m_Mesh: {{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000046
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000043}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_CHEESE_GUID}, type: 2}}
""")

# 5. Visual_Ingredient_GratedCheese
write_prefab("Visual_Ingredient_GratedCheese.prefab", "75911d39ab9042b9b90366e89878b9c8", 1000000047, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000000047
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000048}}
  m_Layer: 0
  m_Name: Visual_Ingredient_GratedCheese
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000048
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000047}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: 1000000050}}
  - {{fileID: 1000000052}}
  - {{fileID: 1000000054}}
  - {{fileID: 1000000058}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000049
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000050}}
  m_Layer: 0
  m_Name: GripPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000050
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000049}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0.02, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000048}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000051
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000052}}
  m_Layer: 0
  m_Name: SupportPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000052
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000051}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -0.035, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000048}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000053
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000054}}
  - component: {{fileID: 1000000055}}
  - component: {{fileID: 1000000056}}
  m_Layer: 0
  m_Name: Bowl
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000054
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000053}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0.18, y: 0.035, z: 0.18}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000048}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000055
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000053}}
  m_Mesh: {{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000056
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000053}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_CONTAINER_GUID}, type: 2}}
--- !u!1 &1000000057
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000058}}
  - component: {{fileID: 1000000059}}
  - component: {{fileID: 1000000060}}
  m_Layer: 0
  m_Name: CheesePile
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000058
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000057}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0.02, z: 0}}
  m_LocalScale: {{x: 0.16, y: 0.05, z: 0.16}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000048}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000059
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000057}}
  m_Mesh: {{fileID: 10207, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000060
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000057}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_CHEESE_RALADO_GUID}, type: 2}}
""")

# 6. Visual_Ingredient_Pepperoni
write_prefab("Visual_Ingredient_Pepperoni.prefab", "054a2469cdd147bbbd240ad3d27af854", 1000000061, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000000061
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000062}}
  m_Layer: 0
  m_Name: Visual_Ingredient_Pepperoni
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000062
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000061}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: 1000000064}}
  - {{fileID: 1000000066}}
  - {{fileID: 1000000068}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000063
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000064}}
  m_Layer: 0
  m_Name: GripPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000064
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000063}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000062}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000065
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000066}}
  m_Layer: 0
  m_Name: SupportPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000066
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000065}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -0.03, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000062}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000067
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000068}}
  - component: {{fileID: 1000000069}}
  - component: {{fileID: 1000000070}}
  m_Layer: 0
  m_Name: PepperoniStick
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000068
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000067}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0.7071068, w: 0.7071068}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0.06, y: 0.10, z: 0.06}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000062}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 90}}
--- !u!33 &1000000069
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000067}}
  m_Mesh: {{fileID: 10208, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000070
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000067}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_CALABRESA_GUID}, type: 2}}
""")

# 7. Visual_Ingredient_SlicedPepperoni
write_prefab("Visual_Ingredient_SlicedPepperoni.prefab", "5d50dbc038eb4994a8e567f09ba47a7a", 1000000071, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000000071
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000072}}
  m_Layer: 0
  m_Name: Visual_Ingredient_SlicedPepperoni
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000072
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000071}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: 1000000074}}
  - {{fileID: 1000000076}}
  - {{fileID: 1000000078}}
  - {{fileID: 1000000082}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000073
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000074}}
  m_Layer: 0
  m_Name: GripPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000074
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000073}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0.015, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000072}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000075
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000076}}
  m_Layer: 0
  m_Name: SupportPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000076
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000075}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -0.015, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000072}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000077
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000078}}
  - component: {{fileID: 1000000079}}
  - component: {{fileID: 1000000080}}
  m_Layer: 0
  m_Name: Dish
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000078
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000077}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0.18, y: 0.015, z: 0.18}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000072}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000079
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000077}}
  m_Mesh: {{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000080
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000077}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_CONTAINER_GUID}, type: 2}}
--- !u!1 &1000000081
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000082}}
  - component: {{fileID: 1000000083}}
  - component: {{fileID: 1000000084}}
  m_Layer: 0
  m_Name: Slices
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000082
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000081}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0.015, z: 0}}
  m_LocalScale: {{x: 0.14, y: 0.006, z: 0.14}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000072}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000083
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000081}}
  m_Mesh: {{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000084
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000081}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_CALABRESA_GUID}, type: 2}}
""")

# 8. Visual_Ingredient_Mushroom
write_prefab("Visual_Ingredient_Mushroom.prefab", "406842cc83ea4b9aa2fc8ecaf099da4c", 1000000085, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000000085
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000086}}
  m_Layer: 0
  m_Name: Visual_Ingredient_Mushroom
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000086
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000085}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: 1000000088}}
  - {{fileID: 1000000090}}
  - {{fileID: 1000000092}}
  - {{fileID: 1000000096}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000087
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000088}}
  m_Layer: 0
  m_Name: GripPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000088
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000087}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000086}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000089
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000090}}
  m_Layer: 0
  m_Name: SupportPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000090
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000089}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -0.06, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000086}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000091
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000092}}
  - component: {{fileID: 1000000093}}
  - component: {{fileID: 1000000094}}
  m_Layer: 0
  m_Name: Cap
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000092
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000091}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0.03, z: 0}}
  m_LocalScale: {{x: 0.13, y: 0.07, z: 0.13}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000086}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000093
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000091}}
  m_Mesh: {{fileID: 10207, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000094
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000091}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_MUSHROOM_GUID}, type: 2}}
--- !u!1 &1000000095
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000096}}
  - component: {{fileID: 1000000097}}
  - component: {{fileID: 1000000098}}
  m_Layer: 0
  m_Name: Stem
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000096
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000095}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -0.02, z: 0}}
  m_LocalScale: {{x: 0.05, y: 0.04, z: 0.05}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000086}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000097
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000095}}
  m_Mesh: {{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000098
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000095}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_MUSHROOM_GUID}, type: 2}}
""")

# 9. Visual_Ingredient_SlicedMushroom
write_prefab("Visual_Ingredient_SlicedMushroom.prefab", "7743f135c45f481c911d313178a41eca", 1000000099, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000000099
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000100}}
  m_Layer: 0
  m_Name: Visual_Ingredient_SlicedMushroom
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000100
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000099}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: 1000000102}}
  - {{fileID: 1000000104}}
  - {{fileID: 1000000106}}
  - {{fileID: 1000000110}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000101
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000102}}
  m_Layer: 0
  m_Name: GripPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000102
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000101}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0.015, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000100}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000103
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000104}}
  m_Layer: 0
  m_Name: SupportPoint
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000104
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000103}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -0.015, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000100}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &1000000105
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000106}}
  - component: {{fileID: 1000000107}}
  - component: {{fileID: 1000000108}}
  m_Layer: 0
  m_Name: Dish
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000106
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000105}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0.18, y: 0.015, z: 0.18}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000100}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000107
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000105}}
  m_Mesh: {{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000108
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000105}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_CONTAINER_GUID}, type: 2}}
--- !u!1 &1000000109
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000000110}}
  - component: {{fileID: 1000000111}}
  - component: {{fileID: 1000000112}}
  m_Layer: 0
  m_Name: Slices
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000000110
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000109}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0.015, z: 0}}
  m_LocalScale: {{x: 0.14, y: 0.006, z: 0.14}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1000000100}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &1000000111
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000109}}
  m_Mesh: {{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &1000000112
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000000109}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_MUSHROOM_GUID}, type: 2}}
""")

# 2. Update Profiles
PROFILES = [
    ("Profile_Ingredient_Dough.asset", 1, 1, 1000000001, "6b2d4dc49c2e46c0822bdfb28876be15"),
    ("Profile_Ingredient_MassaAberta.asset", 6, 2, 1000000011, "50330bb3be2c46468a973efbce8e1862"),
    ("Profile_Ingredient_Sauce.asset", 2, 3, 1000000023, "6f00d9b5be2f47818c86b953a27da0d4"),
    ("Profile_Ingredient_Cheese.asset", 3, 4, 1000000037, "f06d4e9f6b854869ad28f749c0b28d84"),
    ("Profile_Ingredient_GratedCheese.asset", 3, 5, 1000000047, "75911d39ab9042b9b90366e89878b9c8"),
    ("Profile_Ingredient_Pepperoni.asset", 4, 6, 1000000061, "054a2469cdd147bbbd240ad3d27af854"),
    ("Profile_Ingredient_SlicedPepperoni.asset", 4, 7, 1000000071, "5d50dbc038eb4994a8e567f09ba47a7a"),
    ("Profile_Ingredient_Mushroom.asset", 4, 8, 1000000085, "406842cc83ea4b9aa2fc8ecaf099da4c"),
    ("Profile_Ingredient_SlicedMushroom.asset", 4, 9, 1000000099, "7743f135c45f481c911d313178a41eca")
]

for prof_name, cat, def_id, prefab_file_id, prefab_guid in PROFILES:
    prof_path = os.path.join(PROFILES_DIR, prof_name)
    content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 215def019522bcf46a8e57c683f8027e, type: 3}}
  m_Name: {prof_name.replace('.asset', '')}
  m_EditorClassIdentifier: Assembly-CSharp::ScaryParty.Pizzeria.Presentation.ItemPresentationProfile
  presentationKey: {prof_name.replace('Profile_', '').replace('.asset', '')}
  category: {cat}
  definitionId: {def_id}
  isTool: 0
  visualPrefab: {{fileID: {prefab_file_id}, guid: {prefab_guid}, type: 3}}
  gripPointName: GripPoint
  supportPointName: SupportPoint
  fpLeftHandPose:
    position: {{x: -0.04, y: -0.04, z: 0.22}}
    rotationEuler: {{x: 0, y: 0, z: 0}}
    scale: {{x: 1, y: 1, z: 1}}
  fpRightHandPose:
    position: {{x: 0.04, y: -0.04, z: 0.22}}
    rotationEuler: {{x: 0, y: 0, z: 0}}
    scale: {{x: 1, y: 1, z: 1}}
  remoteLeftHandPose:
    position: {{x: 0, y: 0, z: 0.1}}
    rotationEuler: {{x: 0, y: 0, z: 0}}
    scale: {{x: 1, y: 1, z: 1}}
  remoteRightHandPose:
    position: {{x: 0, y: 0, z: 0.1}}
    rotationEuler: {{x: 0, y: 0, z: 0}}
    scale: {{x: 1, y: 1, z: 1}}
  stationSlotPose:
    position: {{x: 0, y: 0, z: 0}}
    rotationEuler: {{x: 0, y: 0, z: 0}}
    scale: {{x: 1, y: 1, z: 1}}
  isPlaceholder: 0
  notes: 
"""
    with open(prof_path, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Updated profile: {prof_name}")

# 3. Ensure ItemPresentationCatalog.asset in Resources
CATALOG_PATH = os.path.join(ASSETS_DIR, "ScriptableObjects", "Pizzeria", "ItemPresentationCatalog.asset")
RES_CATALOG_PATH = os.path.join(RESOURCES_DIR, "ItemPresentationCatalog.asset")
RES_CATALOG_META = RES_CATALOG_PATH + ".meta"

shutil.copyfile(CATALOG_PATH, RES_CATALOG_PATH)
with open(RES_CATALOG_META, "w", encoding="utf-8") as f:
    f.write("fileFormatVersion: 2\nguid: 076277eb3d9a463088963cccb32e5542\nNativeFormatImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

print("Resources ItemPresentationCatalog copied.")

# 4. Fix PizzeriaPrefab.prefab catalog & slot anchors
PREFAB_PATH = os.path.join(PREFABS_DIR, "PizzeriaPrefab.prefab")
with open(PREFAB_PATH, "r", encoding="utf-8") as f:
    prefab_text = f.read()

# Assign catalog to PizzeriaItemVisualizer in PizzeriaPrefab
prefab_text = re.sub(
    r'(m_EditorClassIdentifier:\s*Assembly-CSharp::ScaryParty\.Pizzeria\.Presentation\.PizzeriaItemVisualizer\s+ShowTopMostFoldoutHeaderGroup:\s*1\s+catalog:\s*)\{fileID:\s*0\}',
    r'\g<1>{fileID: 11400000, guid: 8ab051e2454dde5419dc47aa8365e66d, type: 2}',
    prefab_text
)

# Fix SlotAnchor_0 and SlotAnchor_1 positions:
# Station 5: SlotAnchor_0 (fileID 8524384185848102207) -> (-0.35, 0.38, 0)
prefab_text = re.sub(
    r'(--- !u!4 &8524384185848102207[\s\S]*?m_LocalPosition:\s*\{)x:\s*-[0-9\.]+, y:\s*[0-9\.]+, z:\s*[0-9\.]+(\})',
    r'\g<1>x: -0.35, y: 0.38, z: 0\g<2>',
    prefab_text
)
# Station 5: SlotAnchor_1 (fileID 3366809497685710987) -> (0.35, 0.38, 0)
prefab_text = re.sub(
    r'(--- !u!4 &3366809497685710987[\s\S]*?m_LocalPosition:\s*\{)x:\s*-[0-9\.]+, y:\s*[0-9\.]+, z:\s*[0-9\.]+(\})',
    r'\g<1>x: 0.35, y: 0.38, z: 0\g<2>',
    prefab_text
)

with open(PREFAB_PATH, "w", encoding="utf-8") as f:
    f.write(prefab_text)
print("PizzeriaPrefab.prefab updated.")

# 5. Fix GameScene.unity catalog assignment
GAME_SCENE_PATH = os.path.join(ASSETS_DIR, "StarterAssets", "ThirdPersonController", "Scenes", "GameScene.unity")
with open(GAME_SCENE_PATH, "r", encoding="utf-8") as f:
    scene_text = f.read()

scene_text = re.sub(
    r'(m_EditorClassIdentifier:\s*Assembly-CSharp::ScaryParty\.Pizzeria\.Presentation\.PizzeriaItemVisualizer\s+ShowTopMostFoldoutHeaderGroup:\s*1\s+catalog:\s*)\{fileID:\s*0\}',
    r'\g<1>{fileID: 11400000, guid: 8ab051e2454dde5419dc47aa8365e66d, type: 2}',
    scene_text
)

with open(GAME_SCENE_PATH, "w", encoding="utf-8") as f:
    f.write(scene_text)
print("GameScene.unity updated.")

# 6. Fix PizzeriaFbxImportProfile.asset slotOffsets
IMPORT_PROFILE_PATH = os.path.join(ASSETS_DIR, "ScriptableObjects", "Pizzeria", "Import", "PizzeriaFbxImportProfile.asset")
with open(IMPORT_PROFILE_PATH, "r", encoding="utf-8") as f:
    prof_text = f.read()

# Update Prep A and B slotOffsets
prof_text = prof_text.replace("- {x: -1.5, y: 0.82, z: 0}\n    - {x: -0.5, y: 0.82, z: 0}", "- {x: -0.35, y: 0.38, z: 0}\n    - {x: 0.35, y: 0.38, z: 0}")
prof_text = prof_text.replace("- {x: 0.5, y: 0.82, z: 0}\n    - {x: 1.5, y: 0.82, z: 0}", "- {x: -0.35, y: 0.38, z: 0}\n    - {x: 0.35, y: 0.38, z: 0}")
prof_text = prof_text.replace("- {x: -0.6, y: 0.82, z: 0}\n    - {x: 0.6, y: 0.82, z: 0}", "- {x: -0.35, y: 0.38, z: 0}\n    - {x: 0.35, y: 0.38, z: 0}")
prof_text = prof_text.replace("- {x: 0, y: 0.85, z: -0.4}\n    - {x: 0, y: 0.85, z: 0.4}", "- {x: 0, y: 0.38, z: -0.4}\n    - {x: 0, y: 0.38, z: 0.4}")

with open(IMPORT_PROFILE_PATH, "w", encoding="utf-8") as f:
    f.write(prof_text)
print("PizzeriaFbxImportProfile.asset updated.")

print("All ingredient assets, profiles, prefabs, catalog, and scene bindings completed successfully!")
