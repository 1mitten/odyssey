#nullable enable
using System;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// The import settings the animal models need, applied in one place so the probe that
    /// measured them and the catalogue that draws them agree (design 29, e-08).
    ///
    /// <para><b>Scale.</b> Both files are a Blender "units scale" export — 1 cm units with a
    /// ×100 on the mesh — and imported at 11.39 m and 7.07 m long, measured from the bones (the
    /// baked mesh said a hundredth of that and was wrong). ×0.105 and ×0.09 stand them
    /// life-size: a 1.20 m hog and a 0.25 m rat body. Whether life-size is right beside a 2.49 m
    /// colonist is the first playtest question.</para>
    ///
    /// <para><b>Loops.</b> No clip in either file is flagged to loop, and the figure mixer plays a
    /// clip once and then holds its last frame, so an unlooped walk is an animal that takes one
    /// stride and freezes mid-air. Idle, walk and run loop; the one-shots (jump, attack, death)
    /// do not — except the frog's jump, which is its gait (<see cref="Hops"/>).</para>
    ///
    /// <para><b>The frog</b> (design 30 §8) is the same author's export: ×0.11 stood it 0.40 m
    /// wide, 0.25 m tall and 0.39 m nose to toe, measured by <c>AnimalProbe.ShootFrog</c>, and
    /// that could not be picked out from the play camera; ×0.24 (owner, 2026-09-26: "just over
    /// double the size") is about 0.87 m. Its skin is repainted (<see cref="Paints"/>).</para>
    /// </summary>
    public static class AnimalImport
    {
        public const string Folder = "Assets/Art/Custom/Animals";

        public static readonly (string file, float scale)[] Scales =
        {
            ("Pig.fbx", 0.105f),
            ("Rat.fbx", 0.09f),
            ("Frog.fbx", 0.24f),
        };

        /// <summary>
        /// Models whose Jump clip is their locomotion and so loops (design 30 §8). The frog has
        /// no walk: left a one-shot, its figure took one hop and then slid along the ground on
        /// the held last frame, which is exactly what <c>AnimalProbe.ShootMovingFrog</c> printed
        /// the first time — the body sat at 195 mm for the rest of the run while the figure went
        /// on moving.
        /// </summary>
        public static readonly string[] Hops = { "Frog.fbx" };

        /// <summary>
        /// Embedded materials replaced by a project material of one colour (design 30 §8): the
        /// file, the material's name in the file, the asset written for it, and the colour. The
        /// frog's own green was the meadow's green and it vanished into the grass (owner,
        /// 2026-09-26: "a different green colour to the environment so they can be spotted"), so
        /// its skin is a saturated jade — blue of the grass's yellow-green, and brighter than any
        /// of it. Its yellow belly, red eyes and black pupils are left as the author painted them.
        /// </summary>
        public static readonly (string file, string material, string asset, Color colour)[] Paints =
        {
            ("Frog.fbx", "Green", Folder + "/Materials/Frog_Skin.mat", new Color(0.08f, 0.78f, 0.55f)),
        };

        /// <summary>
        /// The material a paint names, written once and kept in step with the table: URP Lit, the
        /// colour, and a low smoothness so the flat-shaded model stays flat.
        /// </summary>
        static Material PaintMaterial(string asset, Color colour)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(asset);
            if (material == null)
            {
                string? directory = System.IO.Path.GetDirectoryName(asset);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(directory)!.Replace('\\', '/'),
                        System.IO.Path.GetFileName(directory));
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, asset);
            }
            bool dirty = false;
            if (material.GetColor("_BaseColor") != colour) { material.SetColor("_BaseColor", colour); dirty = true; }
            if (!Mathf.Approximately(material.GetFloat("_Smoothness"), 0.2f)) { material.SetFloat("_Smoothness", 0.2f); dirty = true; }
            if (dirty) EditorUtility.SetDirty(material);
            return material;
        }

        static bool Loops(string file, string clipName) =>
            (Array.IndexOf(Hops, file) >= 0 && clipName.IndexOf("Jump", StringComparison.OrdinalIgnoreCase) >= 0)
            || clipName.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0
            || clipName.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0
            || clipName.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Apply the settings to every model that does not already carry them. Returns how many changed.</summary>
        public static int Apply()
        {
            int changed = 0;
            foreach (var (file, scale) in Scales)
            {
                string path = Folder + "/" + file;
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;

                bool dirty = false;
                if (!Mathf.Approximately(importer.globalScale, scale)) { importer.globalScale = scale; dirty = true; }
                if (importer.animationType != ModelImporterAnimationType.Generic)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    dirty = true;
                }

                foreach (var (paintFile, name, asset, colour) in Paints)
                {
                    if (paintFile != file) continue;
                    Material paint = PaintMaterial(asset, colour);
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                    importer.GetExternalObjectMap().TryGetValue(id, out UnityEngine.Object? mapped);
                    if (mapped == paint) continue;
                    importer.AddRemap(id, paint);
                    dirty = true;
                }

                ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                    ? importer.clipAnimations
                    : importer.defaultClipAnimations;
                for (int i = 0; i < clips.Length; i++)
                {
                    bool loop = Loops(file, clips[i].name);
                    if (clips[i].loopTime == loop && clips[i].loopPose == false) continue;
                    clips[i].loopTime = loop;
                    clips[i].loopPose = false;
                    dirty = true;
                }
                if (dirty)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            return changed;
        }
    }
}
