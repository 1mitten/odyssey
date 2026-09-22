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
    /// do not.</para>
    /// </summary>
    public static class AnimalImport
    {
        public const string Folder = "Assets/Art/Custom/Animals";

        public static readonly (string file, float scale)[] Scales =
        {
            ("Pig.fbx", 0.105f),
            ("Rat.fbx", 0.09f),
        };

        static bool Loops(string clipName) =>
            clipName.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0
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

                ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                    ? importer.clipAnimations
                    : importer.defaultClipAnimations;
                for (int i = 0; i < clips.Length; i++)
                {
                    bool loop = Loops(clips[i].name);
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
