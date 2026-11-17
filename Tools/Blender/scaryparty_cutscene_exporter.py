bl_info = {
    "name": "ScaryParty Cutscene Exporter",
    "author": "ScaryParty Team",
    "version": (1, 0, 0),
    "blender": (3, 6, 0),
    "location": "View3D > Sidebar > ScaryParty",
    "description": "Exports FBX, camera/cut JSON manifest, and audio references for Unity Cutscene Builder",
    "category": "Import-Export",
}

import json
import math
import os
import hashlib
import bpy
from mathutils import Matrix, Quaternion


SCHEMA_VERSION = "1.0.0"


def _effective_fps(scene):
    fps = float(scene.render.fps)
    fps_base = float(scene.render.fps_base) if scene.render.fps_base > 0 else 1.0
    return fps / fps_base, fps, fps_base


def _stable_id(obj):
    if "scaryparty_id" in obj:
        return str(obj["scaryparty_id"])
    raw = f"{obj.type}:{obj.name}"
    digest = hashlib.sha1(raw.encode("utf-8")).hexdigest()[:10]
    return f"{obj.type.lower()}_{digest}"


def _collect_objects(context, props):
    if props.source_collection is not None:
        return list(props.source_collection.all_objects)
    return list(context.scene.objects)


def _validate_cameras_and_markers(scene, frame_start, frame_end, warnings, errors):
    markers = [m for m in scene.timeline_markers if m.camera is not None]
    markers.sort(key=lambda m: m.frame)

    frame_map = {}
    for m in markers:
        if m.frame in frame_map and frame_map[m.frame] != m.camera:
            errors.append(
                f"Ambiguous camera markers at frame {m.frame}: "
                f"'{frame_map[m.frame].name}' vs '{m.camera.name}'."
            )
        frame_map[m.frame] = m.camera

    shots = []
    for m in markers:
        if frame_start <= m.frame <= frame_end:
            shots.append((m.frame, m.camera))

    # Determine initial camera at frame_start
    initial_cam = None
    prior = [m for m in markers if m.frame <= frame_start]
    if prior:
        initial_cam = prior[-1].camera
    elif scene.camera is not None:
        initial_cam = scene.camera
        warnings.append(
            f"No camera marker at or before frame {frame_start}; using active scene camera '{initial_cam.name}'."
        )
    else:
        errors.append(
            f"No valid initial camera found at or before frame {frame_start}, and scene.camera is None."
        )

    if initial_cam is not None and (not shots or shots[0][0] > frame_start):
        shots.insert(0, (frame_start, initial_cam))

    # Deduplicate consecutive shots with same frame
    cleaned_shots = []
    seen_frames = set()
    for f, cam in shots:
        if f not in seen_frames:
            cleaned_shots.append((f, cam))
            seen_frames.add(f)

    return cleaned_shots


def _sample_cameras(context, cam_objects, frame_start, frame_end, effective_fps):
    depsgraph = context.evaluated_depsgraph_get()
    samples_by_cam = {cam: [] for cam in cam_objects}
    prev_quats = {}

    for frame in range(frame_start, frame_end + 1):
        context.scene.frame_set(frame)
        depsgraph.update()
        t = (frame - frame_start) / effective_fps

        for cam_obj in cam_objects:
            eval_obj = cam_obj.evaluated_get(depsgraph)
            eval_cam = eval_obj.data
            mat = eval_obj.matrix_world
            loc, rot, _scale = mat.decompose()

            # Maintain quaternion sign continuity
            if cam_obj in prev_quats and rot.dot(prev_quats[cam_obj]) < 0.0:
                rot = -rot
            prev_quats[cam_obj] = rot.copy()

            sensor_fit = eval_cam.sensor_fit
            samples_by_cam[cam_obj].append({
                "frame": frame,
                "time": round(t, 6),
                "position": [round(loc.x, 6), round(loc.y, 6), round(loc.z, 6)],
                "rotation": [round(rot.x, 6), round(rot.y, 6), round(rot.z, 6), round(rot.w, 6)],
                "lens": round(float(eval_cam.lens), 4),
                "sensorWidth": round(float(eval_cam.sensor_width), 4),
                "sensorHeight": round(float(eval_cam.sensor_height), 4),
                "sensorFit": sensor_fit,
                "shiftX": round(float(eval_cam.shift_x), 6),
                "shiftY": round(float(eval_cam.shift_y), 6),
                "clipStart": round(float(eval_cam.clip_start), 4),
                "clipEnd": round(float(eval_cam.clip_end), 4),
                "orthoScale": round(float(eval_cam.ortho_scale), 4),
            })

    return samples_by_cam


def _check_unsupported_features(objects, warnings):
    for obj in objects:
        for mod in obj.modifiers:
            if mod.type in {"CLOTH", "FLUID", "SOFT_BODY", "DYNAMIC_PAINT", "OCEAN", "NODES"}:
                warnings.append(
                    f"Object '{obj.name}' has modifier '{mod.name}' ({mod.type}) which is not "
                    "directly supported as standard FBX skeletal/rigid animation."
                )


class SCARYPARTY_PG_CutsceneExportSettings(bpy.types.PropertyGroup):
    cutscene_id: bpy.props.StringProperty(
        name="Cutscene ID",
        default="cutscene_01",
        description="Stable identifier for this cutscene",
    )
    source_collection: bpy.props.PointerProperty(
        name="Collection",
        type=bpy.types.Collection,
        description="Optional collection to export (defaults to entire scene)",
    )
    use_scene_range: bpy.props.BoolProperty(
        name="Use Scene Frame Range",
        default=True,
    )
    frame_start: bpy.props.IntProperty(name="Start Frame", default=1, min=0)
    frame_end: bpy.props.IntProperty(name="End Frame", default=120, min=1)
    resolution_x: bpy.props.IntProperty(name="Width", default=1920, min=64)
    resolution_y: bpy.props.IntProperty(name="Height", default=1080, min=64)
    output_dir: bpy.props.StringProperty(
        name="Output Folder",
        subtype="DIR_PATH",
        default="//CutsceneExport/",
    )


class SCARYPARTY_OT_ExportCutscene(bpy.types.Operator):
    bl_idname = "scaryparty.export_cutscene"
    bl_label = "Validate & Export Cutscene"
    bl_options = {"REGISTER"}

    def execute(self, context):
        props = context.scene.scaryparty_cutscene
        scene = context.scene

        saved_frame = scene.frame_current
        saved_active = context.view_layer.objects.active
        saved_selected = list(context.selected_objects)

        warnings = []
        errors = []

        try:
            frame_start = scene.frame_start if props.use_scene_range else props.frame_start
            frame_end = scene.frame_end if props.use_scene_range else props.frame_end
            if frame_end < frame_start:
                errors.append(f"Invalid frame range: {frame_start}..{frame_end}.")

            objects = _collect_objects(context, props)
            _check_unsupported_features(objects, warnings)
            shots = _validate_cameras_and_markers(scene, frame_start, frame_end, warnings, errors)

            if errors:
                for err in errors:
                    self.report({"ERROR"}, err)
                return {"CANCELLED"}

            out_dir = bpy.path.abspath(props.output_dir)
            os.makedirs(out_dir, exist_ok=True)

            effective_fps, fps, fps_base = _effective_fps(scene)
            duration = (frame_end - frame_start + 1) / effective_fps

            cam_objects = sorted(
                {cam for _, cam in shots} | {o for o in objects if o.type == "CAMERA"},
                key=lambda o: o.name,
            )
            samples_by_cam = _sample_cameras(context, cam_objects, frame_start, frame_end, effective_fps)

            fbx_filename = f"{props.cutscene_id}.fbx"
            fbx_path = os.path.join(out_dir, fbx_filename)

            # Select only exportable objects (meshes, armatures, empties)
            bpy.ops.object.select_all(action="DESELECT")
            for obj in objects:
                if obj.type in {"MESH", "ARMATURE", "EMPTY"}:
                    obj.select_set(True)

            bpy.ops.export_scene.fbx(
                filepath=fbx_path,
                use_selection=True,
                bake_anim=True,
                bake_anim_use_all_actions=False,
                bake_anim_use_nla_strips=False,
                add_leaf_bones=False,
                apply_scale_options="FBX_SCALE_ALL",
            )

            manifest = {
                "schemaVersion": SCHEMA_VERSION,
                "sequence": {
                    "id": props.cutscene_id,
                    "fps": fps,
                    "fpsBase": fps_base,
                    "effectiveFps": effective_fps,
                    "frameStart": frame_start,
                    "frameEnd": frame_end,
                    "duration": round(duration, 6),
                    "resolutionX": props.resolution_x,
                    "resolutionY": props.resolution_y,
                },
                "coordinates": {
                    "unit": "meters",
                    "axisConvention": "BLENDER_Z_UP_RIGHT_HANDED",
                    "sampleSpace": "WORLD_RELATIVE_TO_ORIGIN",
                },
                "fbxFile": fbx_filename,
                "objects": [
                    {
                        "id": _stable_id(obj),
                        "name": obj.name,
                        "parentId": _stable_id(obj.parent) if obj.parent else "",
                        "type": obj.type,
                        "fbxNodePath": obj.name,
                        "materialSlots": [
                            slot.material.name if slot.material else ""
                            for slot in getattr(obj, "material_slots", [])
                        ],
                    }
                    for obj in objects
                    if obj.type in {"MESH", "ARMATURE", "EMPTY"}
                ],
                "cameras": [
                    {
                        "id": _stable_id(cam),
                        "name": cam.name,
                        "projection": cam.data.type,
                        "samples": samples_by_cam[cam],
                    }
                    for cam in cam_objects
                ],
                "shots": [
                    {
                        "startFrame": f,
                        "startTime": round((f - frame_start) / effective_fps, 6),
                        "cameraId": _stable_id(cam),
                        "cameraName": cam.name,
                    }
                    for f, cam in shots
                ],
                "audioEvents": [],
                "warnings": warnings,
            }

            manifest_path = os.path.join(out_dir, f"{props.cutscene_id}.manifest.json")
            with open(manifest_path, "w", encoding="utf-8") as fp:
                json.dump(manifest, fp, indent=2)

            self.report(
                {"INFO"},
                f"Exported '{props.cutscene_id}' ({len(shots)} shots, {len(warnings)} warnings) to {out_dir}",
            )
            return {"FINISHED"}
        finally:
            scene.frame_set(saved_frame)
            bpy.ops.object.select_all(action="DESELECT")
            for obj in saved_selected:
                if obj.name in bpy.data.objects:
                    obj.select_set(True)
            context.view_layer.objects.active = saved_active


class SCARYPARTY_PT_CutscenePanel(bpy.types.Panel):
    bl_label = "ScaryParty Cutscene"
    bl_idname = "SCARYPARTY_PT_cutscene_panel"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "ScaryParty"

    def draw(self, context):
        layout = self.layout
        props = context.scene.scaryparty_cutscene

        layout.prop(props, "cutscene_id")
        layout.prop(props, "source_collection")
        layout.prop(props, "use_scene_range")
        if not props.use_scene_range:
            row = layout.row(align=True)
            row.prop(props, "frame_start")
            row.prop(props, "frame_end")
        row = layout.row(align=True)
        row.prop(props, "resolution_x")
        row.prop(props, "resolution_y")
        layout.prop(props, "output_dir")
        layout.operator("scaryparty.export_cutscene", icon="EXPORT")


classes = (
    SCARYPARTY_PG_CutsceneExportSettings,
    SCARYPARTY_OT_ExportCutscene,
    SCARYPARTY_PT_CutscenePanel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.scaryparty_cutscene = bpy.props.PointerProperty(
        type=SCARYPARTY_PG_CutsceneExportSettings
    )


def unregister():
    del bpy.types.Scene.scaryparty_cutscene
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
