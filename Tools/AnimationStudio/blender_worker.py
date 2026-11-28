"""Executed inside Blender, never overwrites the input .blend."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import runpy
import sys
import bpy

def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(path.suffix + ".tmp")
    temp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    temp.replace(path)

def vector(v):
    return [round(float(x), 6) for x in v]

def inspect(output):
    objects = []
    for o in bpy.context.scene.objects:
        item = {"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None,
                "locationWorld": vector(o.matrix_world.translation), "dimensions": vector(o.dimensions)}
        if o.type == "ARMATURE":
            item["bones"] = [{"name": b.name, "parent": b.parent.name if b.parent else None,
                              "headWorld": vector(o.matrix_world @ b.head_local),
                              "tailWorld": vector(o.matrix_world @ b.tail_local),
                              "deform": b.use_deform} for b in o.data.bones]
        if o.type == "MESH":
            modifiers = [m for m in o.modifiers if m.type == "ARMATURE" and m.object]
            deform_names = {b.name for m in modifiers for b in m.object.data.bones if b.use_deform}
            indices = {g.index for g in o.vertex_groups if g.name in deform_names}
            weights = [sum(g.weight for g in v.groups if g.group in indices) for v in o.data.vertices]
            item.update(vertices=len(weights), armatures=[m.object.name for m in modifiers],
                        vertexGroups=[g.name for g in o.vertex_groups],
                        unweighted=sum(w < 1e-5 for w in weights) if modifiers else None,
                        nonNormalized=sum(abs(w-1.0)>0.01 for w in weights if w>=1e-5) if modifiers else None)
        objects.append(item)
    data = {"blender": bpy.app.version_string, "frame": bpy.context.scene.frame_current,
            "objects": objects, "actions": [{"name": a.name, "frames": vector(a.frame_range)} for a in bpy.data.actions]}
    if output:
        write_json(output, data)
        print("Relatório:", output)
    else:
        print(json.dumps(data, ensure_ascii=False, indent=2))

def append_objects(filename):
    with bpy.data.libraries.load(filename, link=False) as (src, dst):
        dst.objects = list(src.objects)
    collection = bpy.data.collections.new(Path(filename).stem + "_Appended")
    bpy.context.scene.collection.children.link(collection)
    for obj in dst.objects:
        if obj is not None:
            collection.objects.link(obj)

def set_selection(objects):
    view_layer = bpy.context.view_layer
    for obj in view_layer.objects:
        obj.select_set(False)
    for obj in objects:
        obj.hide_set(False)
        obj.hide_select = False
        obj.select_set(True)
    if objects:
        view_layer.objects.active = objects[0]

def mesh_snapshot():
    """Bake evaluated geometry at this exact frame, including armature deformation."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    originals = [o for o in bpy.context.scene.objects if o.type in {"MESH", "CURVE", "SURFACE", "FONT", "META"} and o.visible_get()]
    collection = bpy.data.collections.new("AnimationStudio_PoseSnapshot")
    bpy.context.scene.collection.children.link(collection)
    result = []
    for obj in originals:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
        if not mesh or not mesh.vertices:
            if mesh:
                bpy.data.meshes.remove(mesh)
            continue
        clone = bpy.data.objects.new(obj.name + "_Pose", mesh)
        collection.objects.link(clone)
        clone.matrix_world = evaluated.matrix_world.copy()
        # Object-level material overrides are not always carried by the evaluated mesh.
        for i, slot in enumerate(obj.material_slots):
            if i < len(clone.material_slots) and slot.material:
                clone.material_slots[i].link = "OBJECT"
                clone.material_slots[i].material = slot.material
        result.append(clone)
    return result

def animation_objects():
    visible = [o for o in bpy.context.scene.objects if o.type in {"MESH", "ARMATURE", "EMPTY"} and o.visible_get()]
    all_objects = set(visible)
    for o in visible:
        p = o.parent
        while p:
            all_objects.add(p)
            p = p.parent
        for m in o.modifiers:
            if m.type == "ARMATURE" and m.object:
                all_objects.add(m.object)
    return [o for o in bpy.context.view_layer.objects if o in all_objects]

def publish(args):
    directory = Path(args.output)
    directory.mkdir(parents=True, exist_ok=False)
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for other in args.append:
        append_objects(other)
    if args.frame is not None:
        bpy.context.scene.frame_set(args.frame)
    if args.script:
        # Explicit trusted local script. It may edit the in-memory scene via bpy.
        runpy.run_path(args.script, run_name="__main__")
    bpy.context.view_layer.update()
    frame = bpy.context.scene.frame_current
    if args.save_copy:
        destination = Path(args.save_copy)
        destination.parent.mkdir(parents=True, exist_ok=True)
        if destination.exists():
            raise RuntimeError("Recusando sobrescrever a cópia: " + str(destination))
        bpy.ops.wm.save_as_mainfile(filepath=str(destination), copy=True)
    objects = mesh_snapshot() if args.mode == "pose" else animation_objects()
    if not any(o.type == "MESH" for o in objects):
        raise RuntimeError("Nenhuma mesh visível para exportar.")
    set_selection(objects)
    # Pose mode exports baked meshes (not rest pose); animation mode keeps the rig.
    result = bpy.ops.export_scene.gltf(filepath=str(directory / "model.glb"),
        export_format="GLB", use_selection=True, export_animations=args.mode == "animation",
        export_cameras=False, export_lights=False, export_yup=True)
    if "FINISHED" not in result or not (directory / "model.glb").is_file():
        raise RuntimeError("Exportação GLB não concluída.")
    write_json(directory / "manifest.json", {"name": args.name, "mode": args.mode, "frame": frame,
               "source": args.source, "createdAt": datetime.now(timezone.utc).isoformat(),
               "blender": bpy.app.version_string, "objects": len(objects)})
    print("GLB:", directory / "model.glb")

def main():
    argv = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="command", required=True)
    i = sub.add_parser("inspect")
    i.add_argument("--output", type=Path)
    a = sub.add_parser("publish")
    a.add_argument("--name", required=True)
    a.add_argument("--mode", choices=["pose", "animation"], required=True)
    a.add_argument("--output", required=True)
    a.add_argument("--source", required=True)
    a.add_argument("--frame", type=int)
    a.add_argument("--append", action="append", default=[])
    a.add_argument("--script")
    a.add_argument("--save-copy")
    args = p.parse_args(argv)
    if args.command == "inspect":
        inspect(args.output)
    else:
        publish(args)

if __name__ == "__main__":
    main()
