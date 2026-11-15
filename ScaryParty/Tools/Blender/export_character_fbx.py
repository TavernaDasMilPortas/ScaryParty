# -*- coding: utf-8 -*-
"""
Blender CLI Export Script for ScaryParty Character & Animations.
Normalizes Armature and Mesh to 1.0 scale, puts pivot at feet (Z=0),
sets semantic materials (M_Apron, M_EyePupil, M_EyeWhite, M_Body),
and exports canonical Character.fbx and clean animation clips for Unity.
"""

import bpy
import json
import math
import mathutils
import os
import sys


def get_arg_value(flag):
    if "--" not in sys.argv:
        return None
    idx = sys.argv.index("--")
    args = sys.argv[idx + 1:]
    for i in range(len(args) - 1):
        if args[i] == flag:
            return args[i + 1]
    return None


def clean_scene_keep_armature_and_mesh(include_mesh=True):
    if bpy.context.active_object and bpy.context.active_object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    to_remove = []
    for obj in bpy.data.objects:
        if obj.type == 'ARMATURE':
            continue
        if include_mesh and obj.type == 'MESH':
            continue
        to_remove.append(obj)

    for obj in to_remove:
        bpy.data.objects.remove(obj, do_unlink=True)


def normalize_rig_and_mesh(arm_obj, mesh_obj=None):
    """
    Normalizes Armature and Mesh:
    - Armature location = (0, 0, 0), rotation = (0, 0, 0), scale = (1, 1, 1)
    - Mesh location = (0, 0, 0), rotation = (0, 0, 0), scale = (1, 1, 1)
    - Mesh lowest vertex Z is set to 0.0 (feet on floor)
    - Mesh reparented to Armature cleanly with inverse identity
    """
    if bpy.context.active_object and bpy.context.active_object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    # Deselect all
    bpy.ops.object.select_all(action='DESELECT')

    # If mesh exists, unparent keeping world transform and apply transforms
    if mesh_obj is not None:
        bpy.context.view_layer.objects.active = mesh_obj
        mesh_obj.select_set(True)
        bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        mesh_obj.select_set(False)

    # Apply Armature transform
    if arm_obj is not None:
        bpy.context.view_layer.objects.active = arm_obj
        arm_obj.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        arm_obj.select_set(False)

    # Check Z min on mesh to ensure feet touch Z=0
    if mesh_obj is not None and len(mesh_obj.data.vertices) > 0:
        z_min = min(v.co.z for v in mesh_obj.data.vertices)
        if abs(z_min) > 0.001:
            # Shift mesh vertices by -z_min so feet are exactly at Z=0
            bpy.context.view_layer.objects.active = mesh_obj
            mesh_obj.select_set(True)
            mesh_obj.location.z -= z_min
            bpy.ops.object.transform_apply(location=True)
            mesh_obj.select_set(False)

            # Also shift bone rest positions in armature edit mode
            bpy.context.view_layer.objects.active = arm_obj
            arm_obj.select_set(True)
            bpy.ops.object.mode_set(mode='EDIT')
            for b in arm_obj.data.edit_bones:
                b.head.z -= z_min
                b.tail.z -= z_min
            bpy.ops.object.mode_set(mode='OBJECT')
            arm_obj.select_set(False)

        # Reparent mesh to armature
        mesh_obj.parent = arm_obj
        mesh_obj.matrix_parent_inverse = arm_obj.matrix_world.inverted()

        # Verify final mesh height
        z_min = min(v.co.z for v in mesh_obj.data.vertices)
        z_max = max(v.co.z for v in mesh_obj.data.vertices)
        height = z_max - z_min
        print(f"[SCALE_OK] Mesh bounds Z: min={z_min:.4f}, max={z_max:.4f}, height={height:.4f}m")


def setup_semantic_materials(mesh_obj):
    """
    Configures the 4 material slots with semantic names and clean colors:
    - Slot 0: M_Apron (Apron / avental - 370 polys)
    - Slot 1: M_EyePupil (Dark pupils - 200 polys)
    - Slot 2: M_EyeWhite (White sclera - 200 polys)
    - Slot 3: M_Body (Body mesh / corpo principal - 1252 polys)
    """
    if not mesh_obj or mesh_obj.type != 'MESH':
        return

    slot_defs = [
        ("M_Apron", (0.95, 0.95, 0.95, 1.0)),     # Slot 0: Crisp clean white/cream apron
        ("M_EyePupil", (0.05, 0.05, 0.05, 1.0)),  # Slot 1: Dark black pupils
        ("M_EyeWhite", (0.96, 0.96, 0.96, 1.0)),  # Slot 2: White eye sclera
        ("M_Body", (0.85, 0.55, 0.25, 1.0))       # Slot 3: Gingerbread / body primary base
    ]

    for i, (name, color) in enumerate(slot_defs):
        if i >= len(mesh_obj.material_slots):
            mesh_obj.data.materials.append(None)

        mat = bpy.data.materials.get(name)
        if mat is None:
            mat = bpy.data.materials.new(name=name)
        mat.use_nodes = True
        if mat.node_tree:
            for node in mat.node_tree.nodes:
                if node.type == 'BSDF_PRINCIPLED':
                    node.inputs["Base Color"].default_value = color
                    if "Roughness" in node.inputs:
                        node.inputs["Roughness"].default_value = 0.5

        mesh_obj.material_slots[i].material = mat
        print(f"[MAT_OK] Slot {i} assigned to '{name}' (color={color[:3]})")


def reset_pose_to_rest(arm_obj):
    for pb in arm_obj.pose.bones:
        pb.location = (0.0, 0.0, 0.0)
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.rotation_euler = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)


def apply_sit_pose(arm_obj, t_factor):
    """
    Applies an anatomically correct seated pose on the normalized canonical rig:
    - central (pelvis): rest head Z = 0.4677m. On a chair of ~0.45m seat height,
      settling into the cushion lowers pelvis by at most ~0.025m (2.5 cm), NOT 0.32m!
      Shifts back slightly by -0.04m into the chair backrest.
    - leg1.l / leg1.r (thighs): rotated forward ~72 deg (flat along chair seat).
    - leg2.l / leg2.r (shins): bent down ~68 deg (calves drop vertically in front of chair).
    - chest: upright with slight natural lean (~6 deg) toward dining table.
    - head: subtle forward/downward look toward table/plate.
    - arms: bent naturally at elbows with hands resting forward on dining table / lap.
    """
    reset_pose_to_rest(arm_obj)
    if t_factor <= 0.0001:
        return

    # Quintic smoothstep for ultra smooth ease-in / ease-out
    t = max(0.0, min(1.0, t_factor))
    s = t * t * t * (t * (t * 6.0 - 15.0) + 10.0)

    central = arm_obj.pose.bones.get("central")
    if central:
        central.rotation_mode = 'QUATERNION'
        central.location = (0.0, -0.025 * s, -0.04 * s)

    thigh_angle = math.radians(72.0) * s
    knee_angle = math.radians(68.0) * s

    leg1_l = arm_obj.pose.bones.get("leg1.l")
    if leg1_l:
        leg1_l.rotation_mode = 'QUATERNION'
        leg1_l.rotation_quaternion = mathutils.Euler((0.0, 0.0, -thigh_angle), 'XYZ').to_quaternion()

    leg2_l = arm_obj.pose.bones.get("leg2.l")
    if leg2_l:
        leg2_l.rotation_mode = 'QUATERNION'
        leg2_l.rotation_quaternion = mathutils.Euler((0.0, 0.0, knee_angle), 'XYZ').to_quaternion()

    leg1_r = arm_obj.pose.bones.get("leg1.r")
    if leg1_r:
        leg1_r.rotation_mode = 'QUATERNION'
        leg1_r.rotation_quaternion = mathutils.Euler((0.0, 0.0, thigh_angle), 'XYZ').to_quaternion()

    leg2_r = arm_obj.pose.bones.get("leg2.r")
    if leg2_r:
        leg2_r.rotation_mode = 'QUATERNION'
        leg2_r.rotation_quaternion = mathutils.Euler((0.0, 0.0, -knee_angle), 'XYZ').to_quaternion()

    # Chest upright with subtle natural lean toward dining table
    chest = arm_obj.pose.bones.get("chest")
    if chest:
        chest.rotation_mode = 'QUATERNION'
        chest.rotation_quaternion = mathutils.Euler((math.radians(6.0) * s, 0.0, 0.0), 'XYZ').to_quaternion()

    # Head looking subtly forward/down toward plate
    head = arm_obj.pose.bones.get("head")
    if head:
        head.rotation_mode = 'QUATERNION'
        head.rotation_quaternion = mathutils.Euler((math.radians(-4.0) * s, 0.0, 0.0), 'XYZ').to_quaternion()

    # Arms bent at elbows, hands resting forward on dining table / lap
    arm1_l = arm_obj.pose.bones.get("arm1.l")
    if arm1_l:
        arm1_l.rotation_mode = 'QUATERNION'
        arm1_l.rotation_quaternion = mathutils.Euler((-math.radians(35.0) * s, 0.0, math.radians(15.0) * s), 'XYZ').to_quaternion()

    arm2_l = arm_obj.pose.bones.get("arm2.l")
    if arm2_l:
        arm2_l.rotation_mode = 'QUATERNION'
        arm2_l.rotation_quaternion = mathutils.Euler((-math.radians(45.0) * s, 0.0, 0.0), 'XYZ').to_quaternion()

    arm1_r = arm_obj.pose.bones.get("arm1.r")
    if arm1_r:
        arm1_r.rotation_mode = 'QUATERNION'
        arm1_r.rotation_quaternion = mathutils.Euler((-math.radians(35.0) * s, 0.0, -math.radians(15.0) * s), 'XYZ').to_quaternion()

    arm2_r = arm_obj.pose.bones.get("arm2.r")
    if arm2_r:
        arm2_r.rotation_mode = 'QUATERNION'
        arm2_r.rotation_quaternion = mathutils.Euler((-math.radians(45.0) * s, 0.0, 0.0), 'XYZ').to_quaternion()


def keyframe_all_bones(arm_obj, frame):
    # Note: frame_set is intentionally done BEFORE posing bones so Blender does not wipe the pose!
    for pb in arm_obj.pose.bones:
        pb.keyframe_insert(data_path="location", frame=frame)
        pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
        pb.keyframe_insert(data_path="scale", frame=frame)


def create_procedural_action(arm_obj, clip_name, mode, start_frame, end_frame):
    for act in list(bpy.data.actions):
        bpy.data.actions.remove(act, do_unlink=True)

    act = bpy.data.actions.new(name=clip_name)
    if not arm_obj.animation_data:
        arm_obj.animation_data_create()
    arm_obj.animation_data.action = act

    if hasattr(act, "slots") and len(act.slots) > 0:
        arm_obj.animation_data.action_slot = act.slots[0]

    bpy.context.scene.frame_start = start_frame
    bpy.context.scene.frame_end = end_frame

    total_frames = max(1, end_frame - start_frame)
    for f in range(start_frame, end_frame + 1):
        # 1. Update the scene frame FIRST so Blender does not overwrite our posed bones!
        bpy.context.scene.frame_set(f)
        u = float(f - start_frame) / float(total_frames)

        # 2. Apply the procedural pose on frame f
        if mode == "IDLE":
            reset_pose_to_rest(arm_obj)
            breath = math.sin(u * math.pi * 2.0) * 0.04
            sway = math.cos(u * math.pi * 2.0) * 0.02
            chest = arm_obj.pose.bones.get("chest")
            if chest:
                chest.rotation_mode = 'QUATERNION'
                chest.rotation_quaternion = mathutils.Euler((breath, 0.0, sway), 'XYZ').to_quaternion()
            head = arm_obj.pose.bones.get("head")
            if head:
                head.rotation_mode = 'QUATERNION'
                head.rotation_quaternion = mathutils.Euler((-breath * 0.4, 0.0, -sway * 0.4), 'XYZ').to_quaternion()
            arm1_l = arm_obj.pose.bones.get("arm1.l")
            if arm1_l:
                arm1_l.rotation_mode = 'QUATERNION'
                arm1_l.rotation_quaternion = mathutils.Euler((0.0, 0.0, breath * 0.3), 'XYZ').to_quaternion()
            arm1_r = arm_obj.pose.bones.get("arm1.r")
            if arm1_r:
                arm1_r.rotation_mode = 'QUATERNION'
                arm1_r.rotation_quaternion = mathutils.Euler((0.0, 0.0, -breath * 0.3), 'XYZ').to_quaternion()
        elif mode == "SIT_IDLE":
            apply_sit_pose(arm_obj, 1.0)
            breath = math.sin(u * math.pi * 2.0) * 0.02
            chest = arm_obj.pose.bones.get("chest")
            if chest:
                chest.rotation_mode = 'QUATERNION'
                chest.rotation_quaternion = mathutils.Euler((math.radians(6.0) + breath, 0.0, 0.0), 'XYZ').to_quaternion()
            head = arm_obj.pose.bones.get("head")
            if head:
                head.rotation_mode = 'QUATERNION'
                head.rotation_quaternion = mathutils.Euler((math.radians(-4.0) - breath * 0.3, 0.0, 0.0), 'XYZ').to_quaternion()
            arm1_l = arm_obj.pose.bones.get("arm1.l")
            if arm1_l:
                arm1_l.rotation_mode = 'QUATERNION'
                arm1_l.rotation_quaternion = mathutils.Euler((-math.radians(35.0) - breath * 0.15, 0.0, math.radians(15.0)), 'XYZ').to_quaternion()
            arm1_r = arm_obj.pose.bones.get("arm1.r")
            if arm1_r:
                arm1_r.rotation_mode = 'QUATERNION'
                arm1_r.rotation_quaternion = mathutils.Euler((-math.radians(35.0) - breath * 0.15, 0.0, -math.radians(15.0)), 'XYZ').to_quaternion()
        elif mode == "SIT_DOWN":
            apply_sit_pose(arm_obj, u)
        elif mode == "STAND_UP":
            apply_sit_pose(arm_obj, 1.0 - u)

        # 3. Keyframe the posed bones on this frame
        keyframe_all_bones(arm_obj, f)

    return act


def export_fbx_file(output_path, include_mesh, bake_anim):
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    obj_types = {'ARMATURE', 'MESH'} if include_mesh else {'ARMATURE'}

    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=False,
        object_types=obj_types,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        armature_nodetype='NULL',
        bake_anim=bake_anim,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=False,
        axis_forward='-Z',
        axis_up='Y',
        path_mode='AUTO'
    )


def run_from_manifest(manifest_path):
    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    project_root = manifest["project_root"]
    output_dir = os.path.join(project_root, manifest["output_dir"])
    os.makedirs(output_dir, exist_ok=True)

    # 1. Export canonical Character.fbx (mesh + normalized rig in rest pose)
    char_cfg = manifest["character_mesh"]
    char_blend = os.path.join(project_root, char_cfg["source_blend"])
    char_out = os.path.join(output_dir, char_cfg["output_fbx"])

    bpy.ops.wm.open_mainfile(filepath=char_blend)
    clean_scene_keep_armature_and_mesh(include_mesh=True)
    arm_obj = bpy.data.objects.get("Armature")
    mesh_obj = bpy.data.objects.get("Cylinder.002")

    normalize_rig_and_mesh(arm_obj, mesh_obj)
    setup_semantic_materials(mesh_obj)

    if arm_obj:
        if arm_obj.animation_data:
            arm_obj.animation_data_clear()
        reset_pose_to_rest(arm_obj)
        arm_obj.data.pose_position = 'REST'
        bpy.context.view_layer.update()

    export_fbx_file(char_out, include_mesh=True, bake_anim=False)
    print(f"[EXPORT_OK] Canonical Mesh: {char_out}")

    # 2. Export source clips (Walk, Run, Prepare) from .blend files
    # Keeping both Armature and Mesh (include_mesh=True) ensures identical scene hierarchy
    # so Unity generates curve bindings with path='Armature/central/...' matching CharacterVisual.prefab
    for clip_cfg in manifest.get("source_clips", []):
        blend_path = os.path.join(project_root, clip_cfg["source_blend"])
        out_path = os.path.join(output_dir, clip_cfg["output_fbx"])
        clip_name = clip_cfg["clip_name"]

        bpy.ops.wm.open_mainfile(filepath=blend_path)
        clean_scene_keep_armature_and_mesh(include_mesh=True)
        arm_obj = bpy.data.objects.get("Armature")
        mesh_obj = bpy.data.objects.get("Cylinder.002")

        normalize_rig_and_mesh(arm_obj, mesh_obj)

        if arm_obj and arm_obj.animation_data and arm_obj.animation_data.action:
            act = arm_obj.animation_data.action
            act.name = clip_name
            start_f = int(clip_cfg.get("start_frame", round(act.frame_range[0])))
            end_f = int(clip_cfg.get("end_frame", round(act.frame_range[1])))
            bpy.context.scene.frame_start = start_f
            bpy.context.scene.frame_end = end_f
            arm_obj.data.pose_position = 'POSE'
            bpy.context.view_layer.update()

        export_fbx_file(out_path, include_mesh=True, bake_anim=True)
        print(f"[EXPORT_OK] Source Clip '{clip_name}' ({bpy.context.scene.frame_start}-{bpy.context.scene.frame_end}): {out_path}")

    # 3. Export procedural clips on the same canonical rig (Idle, SitIdle, SitDown, StandUp)
    for proc_cfg in manifest.get("procedural_clips", []):
        out_path = os.path.join(output_dir, proc_cfg["output_fbx"])
        clip_name = proc_cfg["clip_name"]
        mode = proc_cfg["mode"]
        start_f = int(proc_cfg.get("start_frame", 1))
        end_f = int(proc_cfg.get("end_frame", 25))

        bpy.ops.wm.open_mainfile(filepath=char_blend)
        clean_scene_keep_armature_and_mesh(include_mesh=True)
        arm_obj = bpy.data.objects.get("Armature")
        mesh_obj = bpy.data.objects.get("Cylinder.002")

        normalize_rig_and_mesh(arm_obj, mesh_obj)

        if arm_obj:
            arm_obj.data.pose_position = 'POSE'
            create_procedural_action(arm_obj, clip_name, mode, start_f, end_f)
            bpy.context.view_layer.update()

        export_fbx_file(out_path, include_mesh=True, bake_anim=True)
        print(f"[EXPORT_OK] Procedural Clip '{clip_name}': {out_path}")

    print("=== ALL_CHARACTER_FBX_EXPORTS_COMPLETED ===")


if __name__ == "__main__":
    manifest_arg = get_arg_value("--manifest")
    if not manifest_arg:
        print("ERROR: Missing --manifest <path>")
        sys.exit(1)
    run_from_manifest(manifest_arg)
