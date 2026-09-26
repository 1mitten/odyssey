#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The dream's blur on the way into a colony (<c>docs/design/56-wake-up.md</c> §4): a
    /// dual-filter blur laid over the frame before post-processing, at the strength the wake asks
    /// for, on one camera, for about five seconds — and then gone.
    ///
    /// <para><b>Injected, not a renderer feature.</b> It exists only while a wake is running, so it
    /// enqueues its pass from <see cref="RenderPipelineManager.beginCameraRendering"/> for the one
    /// camera it was given and unsubscribes when disposed. Nothing is added to the renderer asset
    /// and nothing runs, not even an early return, once the player is awake.</para>
    ///
    /// <para><b>Our own rather than URP's depth of field</b> (§4a). URP's Bokeh radius is capped at
    /// twenty pixels of the screen's height and its Gaussian at 1.5, so at 4K either reads as a
    /// soft picture rather than as waking; a dual-filter chain's radius doubles with every level,
    /// and the number of levels is chosen from the screen's height.</para>
    ///
    /// <para><b>Before post-processing</b>, so the blurred bright patches still bloom — the
    /// dream's glow — and the golden hour grades the blur exactly as it grades the world.</para>
    /// </summary>
    public sealed class WakeBlur : IDisposable
    {
        public const string ShaderName = "Odyssey/WakeBlur";

        readonly Camera _camera;
        readonly Material? _material;
        readonly BlurPass? _pass;
        bool _subscribed;

        static readonly int OffsetId = Shader.PropertyToID("_WakeOffset");
        static readonly int StrengthId = Shader.PropertyToID("_WakeStrength");

        /// <summary>Below this the pass is not enqueued at all: the last few hundredths of a blur
        /// are invisible and cost the whole chain.</summary>
        public const float Threshold = 0.004f;

        public WakeBlur(Camera camera)
        {
            _camera = camera;
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                // Missing from a player build is the failure ShaderInclusion exists for; here it
                // costs the blur and nothing else, rather than a pipeline throwing every frame.
                Debug.LogWarning($"[Odyssey] shader {ShaderName} not found; the wake is in focus.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new BlurPass(_material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            _subscribed = true;
        }

        /// <summary>Whether the shader was found, so there is a blur to draw.</summary>
        public bool Available => _pass != null;

        /// <summary>How far out of focus the world is, 0 to 1.</summary>
        public float Strength { get; set; }

        /// <summary>How many times the pass was enqueued, for a test that wants to know it ran.</summary>
        public int Enqueued { get; private set; }

        void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_pass == null || _material == null || camera != _camera) return;
            if (Strength < Threshold) return;
            UniversalAdditionalCameraData? data = camera.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;

            float s = Mathf.Clamp01(Strength);
            // The taps spread with the strength, so the blur shrinks as it fades rather than only
            // thinning — the difference between a focus pull and a fade between two pictures.
            _material.SetFloat(OffsetId, Mathf.Lerp(0.6f, 1.6f, s));
            // Laid on faster than it grows, so the middle of the wake is still properly soft.
            _material.SetFloat(StrengthId, Mathf.Sqrt(s));
            data.scriptableRenderer.EnqueuePass(_pass);
            Enqueued++;
        }

        public void Dispose()
        {
            if (_subscribed)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
                _subscribed = false;
            }
            CoreUtils.Destroy(_material);
        }

        /// <summary>
        /// How many levels the chain goes down for a target this tall: halved until it is about
        /// thirty-two pixels high, so 480 lines take three, 1080 five and 2160 six, and the blur
        /// covers the same share of the screen at every resolution.
        /// </summary>
        public static int LevelsFor(int height) =>
            Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Max(height, 1) / 32f, 2f)), 1, 7);

        sealed class BlurPass : ScriptableRenderPass
        {
            const int DownPass = 0, UpPass = 1, LayPass = 2;
            static readonly Vector4 Whole = new Vector4(1f, 1f, 0f, 0f);

            readonly Material _material;

            public BlurPass(Material material)
            {
                _material = material;
                // A pass that reads the camera colour needs it to be a real texture.
                requiresIntermediateTexture = true;
            }

            class StepData
            {
                public Material Material = null!;
                public TextureHandle Source;
                public int Pass;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                TextureHandle colour = resources.activeColorTexture;
                if (!colour.IsValid()) return;

                TextureDesc colourDesc = renderGraph.GetTextureDesc(colour);
                int levels = LevelsFor(colourDesc.height);

                var down = new TextureHandle[levels];
                TextureHandle previous = colour;
                int width = colourDesc.width, height = colourDesc.height;
                for (int i = 0; i < levels; i++)
                {
                    width = Mathf.Max(1, width / 2);
                    height = Mathf.Max(1, height / 2);
                    down[i] = renderGraph.CreateTexture(Level(colourDesc, width, height, "Odyssey wake down " + i));
                    Step(renderGraph, "Odyssey wake down", previous, down[i], DownPass);
                    previous = down[i];
                }

                for (int i = levels - 2; i >= 0; i--)
                {
                    TextureDesc at = renderGraph.GetTextureDesc(down[i]);
                    TextureHandle up = renderGraph.CreateTexture(Level(colourDesc, at.width, at.height, "Odyssey wake up " + i));
                    Step(renderGraph, "Odyssey wake up", previous, up, UpPass);
                    previous = up;
                }

                // Laid over the camera colour with alpha: a lerp between the frame and its blur,
                // which is what lets the strength fall smoothly to nothing.
                using (IRasterRenderGraphBuilder builder =
                       renderGraph.AddRasterRenderPass<StepData>("Odyssey wake lay", out StepData data))
                {
                    data.Material = _material;
                    data.Source = previous;
                    data.Pass = LayPass;
                    builder.UseTexture(previous, AccessFlags.Read);
                    builder.SetRenderAttachment(colour, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((StepData pass, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, pass.Source, Whole, pass.Material, pass.Pass));
                }
            }

            void Step(RenderGraph renderGraph, string name, TextureHandle source, TextureHandle destination, int shaderPass)
            {
                using IRasterRenderGraphBuilder builder =
                    renderGraph.AddRasterRenderPass<StepData>(name, out StepData data);
                data.Material = _material;
                data.Source = source;
                data.Pass = shaderPass;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc((StepData pass, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, pass.Source, Whole, pass.Material, pass.Pass));
            }

            static TextureDesc Level(TextureDesc colour, int width, int height, string name) =>
                new TextureDesc(width, height)
                {
                    name = name,
                    format = colour.format,
                    msaaSamples = MSAASamples.None,
                    depthBufferBits = DepthBits.None,
                    clearBuffer = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
        }
    }
}
