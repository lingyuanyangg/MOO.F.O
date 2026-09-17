using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class TDFeedbackRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material feedbackMaterial;
        [Range(0.8f, 0.999f)] public float decay = 0.96f;
        [Range(0.95f, 1.05f)] public float zoom = 1.002f;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public Settings settings = new Settings();

    class TDFeedbackPass : ScriptableRenderPass
    {
        private Settings settings;
        private RenderTexture prevBuffer;
        private RenderTexture tempBuffer;

        public TDFeedbackPass(Settings settings)
        {
            this.settings = settings;
        }

        private void EnsureBuffers(int width, int height)
        {
            if (prevBuffer == null || prevBuffer.width != width || prevBuffer.height != height)
            {
                CleanUp();

                prevBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                tempBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                prevBuffer.Create();
                tempBuffer.Create();
            }
        }

        // Unity 6 唯一的 Render Pass 重写入口
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (settings.feedbackMaterial == null) return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle activeColorTarget = resourceData.activeColorTexture;
            if (!activeColorTarget.IsValid()) return;

            int width = cameraData.cameraTargetDescriptor.width;
            int height = cameraData.cameraTargetDescriptor.height;
            EnsureBuffers(width, height);

            settings.feedbackMaterial.SetFloat("_Decay", settings.decay);
            settings.feedbackMaterial.SetFloat("_Zoom", settings.zoom);
            settings.feedbackMaterial.SetTexture("_PrevTex", prevBuffer);

            // 通过 UnsafePass 处理历史帧双缓存读写
            using (var builder = renderGraph.AddUnsafePass<PassData>("TD Feedback Loop Pass", out var passData))
            {
                passData.material = settings.feedbackMaterial;
                passData.tempBuffer = tempBuffer;
                passData.prevBuffer = prevBuffer;

                builder.UseTexture(activeColorTarget, AccessFlags.ReadWrite);

                builder.SetRenderFunc<PassData>((data, context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // 1. 将当前屏幕 (activeColorTarget) 与上一帧混合输出到 tempBuffer
                    cmd.Blit(activeColorTarget, data.tempBuffer, data.material);

                    // 2. 将混合好的结果写回屏幕
                    cmd.Blit(data.tempBuffer, activeColorTarget);

                    // 3. 将结果保存到 prevBuffer，作为下一帧的历史缓存
                    cmd.Blit(data.tempBuffer, data.prevBuffer);
                });
            }
        }

        class PassData
        {
            public Material material;
            public RenderTexture tempBuffer;
            public RenderTexture prevBuffer;
        }

        public void CleanUp()
        {
            if (prevBuffer != null) { prevBuffer.Release(); prevBuffer = null; }
            if (tempBuffer != null) { tempBuffer.Release(); tempBuffer = null; }
        }
    }

    private TDFeedbackPass pass;

    public override void Create()
    {
        pass = new TDFeedbackPass(settings);
        pass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.CleanUp();
    }
}