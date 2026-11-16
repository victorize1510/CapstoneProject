"""Blender background build. Originals stay on E; only runtime exports enter Assets.

blender -b --factory-startup -t 2 --python this.py -- --project <Capstone> --qa <external-root>
"""
import argparse
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
parser = argparse.ArgumentParser()
parser.add_argument("--project", required=True)
parser.add_argument("--qa", required=True)
opts = parser.parse_args(args)
root = Path(opts.project) / "Assets/Game/WorldFeedback/Art"
qa = Path(opts.qa)
qa.mkdir(parents=True, exist_ok=True)
owned = []
report = []
specs = [
    ("BallCommon", "BallThuong", (8000, 2000, 512), False),
    ("BallRare", "BallRare", (8000, 2000, 512), False),
    ("BallUltra", "BallUltra", (8000, 2000, 512), False),
    ("RecoveryTable", "tABLE", (40000, 15000, 4000), True),
]


def bounds(obj):
    points = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector(tuple(min(v[i] for v in points) for i in range(3)))
    hi = Vector(tuple(max(v[i] for v in points) for i in range(3)))
    return lo, hi


def select_only(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def material_for(folder, stem):
    mat = bpy.data.materials.new(stem)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = nodes.get("Principled BSDF")
    for suffix, socket in (("", "Base Color"), ("_metallic", "Metallic"), ("_roughness", "Roughness")):
        path = folder / (stem + suffix + ".png")
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(str(path), check_existing=True)
        if suffix:
            tex.image.colorspace_settings.name = "Non-Color"
        links.new(tex.outputs["Color"], shader.inputs[socket])
    normal_tex = nodes.new("ShaderNodeTexImage")
    normal_tex.image = bpy.data.images.load(str(folder / (stem + "_normal.png")), check_existing=True)
    normal_tex.image.colorspace_settings.name = "Non-Color"
    normal = nodes.new("ShaderNodeNormalMap")
    normal.inputs["Strength"].default_value = 0.7
    links.new(normal_tex.outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], shader.inputs["Normal"])
    return mat


def render_icon(obj, destination, size):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.world.color = (0.18, 0.18, 0.18)
    scene.view_settings.view_transform = "AgX"
    lo, hi = bounds(obj)
    center = (lo + hi) * 0.5
    extent = max(hi - lo)
    cam_data = bpy.data.cameras.new("PreviewCamera")
    cam = bpy.data.objects.new("PreviewCamera", cam_data)
    scene.collection.objects.link(cam)
    cam.location = center + Vector((2.4, -4, 2.1)).normalized() * extent * 4
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = extent * 1.32
    scene.camera = cam
    lights = []
    for location, power, scale in (((-3, -4, 5), 650, 4), ((4, -1, 2), 400, 3), ((1, 3, 4), 700, 3)):
        data = bpy.data.lights.new("PreviewLight", "AREA")
        data.energy = power
        data.shape = "DISK"
        data.size = scale * extent
        light = bpy.data.objects.new("PreviewLight", data)
        scene.collection.objects.link(light)
        light.location = center + Vector(location) * extent
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
        lights.append(light)
    scene.render.filepath = str(destination)
    bpy.ops.render.render(write_still=True)
    for ob in [cam] + lights:
        bpy.data.objects.remove(ob, do_unlink=True)


for name, source_name, budgets, table in specs:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    source = next((Path("E:/Download/ModelItem") / source_name).rglob("*.fbx"))
    folder, stem = source.parent, source.stem
    destination = root / name
    destination.mkdir(parents=True, exist_ok=True)
    for suffix in ("", "_normal", "_metallic", "_roughness"):
        shutil.copy2(folder / (stem + suffix + ".png"), destination / (name + suffix + ".png"))
    bpy.ops.import_scene.fbx(filepath=str(source))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for ob in meshes:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    original = bpy.context.object
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    lo, hi = bounds(original)
    original.location -= (lo + hi) * 0.5
    scale = (1.1 / (hi.z - lo.z)) if table else (1.0 / max(hi - lo))
    original.scale *= scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    lo, hi = bounds(original)
    if table:
        original.location.z -= lo.z
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    original.data.materials.clear()
    original.data.materials.append(material_for(folder, stem))
    original.data.calc_loop_triangles()
    source_faces = len(original.data.loop_triangles)
    entry = {"id": name, "source": str(source), "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
             "sourceTriangles": source_faces, "lods": []}
    for index, budget in enumerate(budgets):
        obj = original.copy()
        obj.data = original.data.copy()
        obj.name = name + "_LOD" + str(index)
        bpy.context.collection.objects.link(obj)
        select_only(obj)
        modifier = obj.modifiers.new("RuntimeDecimation", "DECIMATE")
        modifier.ratio = min(1, budget / source_faces)
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.data.calc_loop_triangles()
        export = destination / (obj.name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=str(export), use_selection=True, object_types={"MESH"},
                                 axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                                 bake_anim=False, use_mesh_modifiers=True, path_mode="STRIP")
        lo, hi = bounds(obj)
        entry["lods"].append({"triangles": len(obj.data.loop_triangles), "size": list(hi-lo), "asset": str(export)})
        if index == 0:
            original.hide_render = True
            icon = destination / (name + "_Icon.png")
            render_icon(obj, icon, 256)
            if name in ("BallCommon", "RecoveryTable"):
                preview = qa / (name + "-optimized.png")
                render_icon(obj, preview, 512)
                owned.append(str(preview))
            original.hide_render = False
        bpy.data.objects.remove(obj, do_unlink=True)
    report.append(entry)
    print("PVE_MODEL " + json.dumps(entry), flush=True)

(root / "runtime-models.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
result = qa / "model-build.json"
result.write_text(json.dumps(report, indent=2), encoding="utf-8")
owned.append(str(result))
(qa / "model-created-files.json").write_text(json.dumps(owned, indent=2), encoding="utf-8")
print("PVE_MODEL_BUILD_COMPLETE", flush=True)
