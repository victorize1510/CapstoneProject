using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Capstone.Game.CaptureSystem
{
    // Chi sao chep hinh dang dang hien; khong clone actor, Animator, collider hoac xuong.
    internal sealed class CaptureVisualProxy : IDisposable
    {
        readonly List<Mesh> meshes = new List<Mesh>();
        public readonly GameObject Root;
        public readonly Vector3 Center, HalfExtents;
        public CaptureVisualProxy(WildPetCaptureTarget target, Vector3 fallbackCenter)
        {
            Root = new GameObject("Capture visual only");
            Root.transform.position = fallbackCenter;
            var visual = target.GetComponent<PetEvolutionVisualController>();
            bool hasBounds = false;
            Bounds bounds = default;
            try
            {
                foreach (var source in target.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(source is SkinnedMeshRenderer) && !(source is MeshRenderer)) continue;
                    // Khong bao gio lay renderer cua player hay actor long ben trong.
                    if (source.GetComponentInParent<PetController>() != target.Pet
                        || source.GetComponentInParent<BasicPlayerMovement>() != null
                        || source.GetComponentInParent<Canvas>() != null
                        || source.GetComponentInParent<AAMAP.MapIcon>(true) != null
                        || source.GetComponent<ParticleSystem>() != null
                        || visual != null && !visual.IsCurrentVisualRenderer(source)) continue;
                    Mesh mesh;
                    if (source is SkinnedMeshRenderer skin)
                    {
                        mesh = new Mesh();
                        skin.BakeMesh(mesh);
                    }
                    else
                    {
                        var original = source.GetComponent<MeshFilter>()?.sharedMesh;
                        if (original == null || !original.isReadable) continue;
                        mesh = Object.Instantiate(original);
                    }
                    meshes.Add(mesh);
                    mesh.name = "Capture pose buffer";
                    var matrix = Matrix4x4.Translate(-fallbackCenter) * source.localToWorldMatrix;
                    var vertices = mesh.vertices;
                    for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                    mesh.vertices = vertices;
                    var normals = mesh.normals;
                    var normalMatrix = matrix.inverse.transpose;
                    for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                    mesh.normals = normals;
                    var tangents = mesh.tangents;
                    for (int i = 0; i < tangents.Length; i++)
                    {
                        var direction = matrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                        tangents[i] = new Vector4(direction.x, direction.y, direction.z, tangents[i].w * Mathf.Sign(matrix.determinant));
                    }
                    mesh.tangents = tangents;
                    mesh.RecalculateBounds();
                    var child = new GameObject("Captured body image", typeof(MeshFilter), typeof(MeshRenderer));
                    child.layer = source.gameObject.layer;
                    child.transform.SetParent(Root.transform, false);
                    child.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = source.sharedMaterials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = source.receiveShadows;
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                Center = hasBounds ? bounds.center : fallbackCenter;
                HalfExtents = hasBounds ? bounds.extents : Vector3.one * .4f;
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (Root != null) { Root.SetActive(false); Object.Destroy(Root); }
            foreach (var mesh in meshes) if (mesh != null) Object.Destroy(mesh);
            meshes.Clear();
        }
    }
}
