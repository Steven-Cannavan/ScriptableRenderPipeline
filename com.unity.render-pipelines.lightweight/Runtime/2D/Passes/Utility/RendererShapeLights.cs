using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LWRP
{
    public class RendererShapeLights
    {
        static private RenderTextureFormat m_RenderTextureFormatToUse;
        static RenderTargetHandle[] m_RenderTargets;
        static _2DShapeLightTypeDescription[] m_LightTypes;
        static Camera m_Camera;
        static RenderTexture m_FullScreenShadowTexture = null;
        const string k_UseShapeLightTypeKeyword = "USE_SHAPE_LIGHT_TYPE_";

        static public void Setup(_2DShapeLightTypeDescription[] lightTypes, Camera camera)
        {
            m_RenderTextureFormatToUse = RenderTextureFormat.ARGB32;
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float))
                m_RenderTextureFormatToUse = RenderTextureFormat.RGB111110Float;
            else if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                m_RenderTextureFormatToUse = RenderTextureFormat.ARGBHalf;

            m_Camera = camera;
            m_LightTypes = lightTypes;

            m_RenderTargets = new RenderTargetHandle[m_LightTypes.Length];
            for (int i = 0; i < m_LightTypes.Length; ++i)
                m_RenderTargets[i].Init("_ShapeLightTexture" + i);
        }

        static public void CreateRenderTextures(ScriptableRenderContext context)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Create Shape Light Textures");
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor();
            descriptor.colorFormat = m_RenderTextureFormatToUse;
            descriptor.sRGB = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.dimension = TextureDimension.Tex2D;

            for (int i = 0; i < m_LightTypes.Length; ++i)
            {
                float renderTextureScale = Mathf.Clamp(m_LightTypes[i].renderTextureScale, 0.01f, 1.0f);
                descriptor.width = (int)(m_Camera.pixelWidth * renderTextureScale);
                descriptor.height = (int)(m_Camera.pixelHeight * renderTextureScale);
                cmd.GetTemporaryRT(m_RenderTargets[i].id, descriptor, FilterMode.Bilinear);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        static public void ReleaseRenderTextures(ScriptableRenderContext context)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Release Shape Light Textures");

            for (int i = 0; i < m_RenderTargets.Length; ++i)
                cmd.ReleaseTemporaryRT(m_RenderTargets[i].id);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        static public void ClearTarget(CommandBuffer cmdBuffer, RenderTargetIdentifier renderTexture, Color color, string shaderKeyword)
        {
            cmdBuffer.DisableShaderKeyword(shaderKeyword);
            cmdBuffer.SetRenderTarget(renderTexture);
            cmdBuffer.ClearRenderTarget(false, true, color, 1.0f);
        }

        static public void Clear(CommandBuffer cmdBuffer)
        {
            for (int i = 0; i < m_LightTypes.Length; ++i)
                ClearTarget(cmdBuffer, m_RenderTargets[i].Identifier(), m_LightTypes[i].globalColor, k_UseShapeLightTypeKeyword + i);
        }

        static private void RenderLightSet(Camera camera, Light2D.ShapeLightType type, CommandBuffer cmdBuffer, int layerToRender, RenderTargetIdentifier renderTexture, Color fillColor, string shaderKeyword, List<Light2D> lights)
        {
            cmdBuffer.DisableShaderKeyword(shaderKeyword);
            bool renderedFirstLight = false;

            if (lights.Count > 0)
            {
                for (int i = 0; i < lights.Count; i++)
                {
                    Light2D light = lights[i];

                    if (light != null && light.isActiveAndEnabled && light.shapeLightType == type && light.IsLitLayer(layerToRender) && light.IsLightVisible(camera))
                    {
                        Material shapeLightMaterial = light.GetMaterial();
                        if (shapeLightMaterial != null)
                        {
                            Mesh lightMesh = light.GetMesh();
                            if (lightMesh != null)
                            {
                                if (!renderedFirstLight)
                                {
                                    cmdBuffer.SetRenderTarget(renderTexture);
                                    cmdBuffer.ClearRenderTarget(false, true, fillColor, 1.0f);
                                    renderedFirstLight = true;
                                }

                                cmdBuffer.EnableShaderKeyword(shaderKeyword);
                                cmdBuffer.DrawMesh(lightMesh, light.transform.localToWorldMatrix, shapeLightMaterial);
                            }
                        }
                    }
                }
            }
        }

        static private void RenderLightVolumeSet(Camera camera, Light2D.ShapeLightType type, CommandBuffer cmdBuffer, int layerToRender, RenderTargetIdentifier renderTexture, List<Light2D> lights)
        {
            if (lights.Count > 0)
            {
                for (int i = 0; i < lights.Count; i++)
                {
                    Light2D light = lights[i];

                    if (light != null && light.isActiveAndEnabled && light.LightVolumeOpacity > 0.0f && light.shapeLightType == type && light.IsLitLayer(layerToRender) && light.IsLightVisible(camera))
                    {
                        Material shapeLightVolumeMaterial = light.GetVolumeMaterial();
                        if (shapeLightVolumeMaterial != null)
                        {
                            Mesh lightMesh = light.GetMesh();
                            if (lightMesh != null)
                            {
                                cmdBuffer.DrawMesh(lightMesh, light.transform.localToWorldMatrix, shapeLightVolumeMaterial);
                            }
                        }
                    }
                }
            }
        }

        static public void SetShaderGlobals(CommandBuffer cmdBuffer)
        {
            cmdBuffer.SetGlobalTexture("_ShadowTex", m_FullScreenShadowTexture);
        }

        static public void RenderLights(Camera camera, CommandBuffer cmdBuffer, int layerToRender)
        {
            for (int i = 0; i < m_LightTypes.Length; ++i)
            {
                string sampleName = "2D Shape Lights - " + m_LightTypes[i].name;
                cmdBuffer.BeginSample(sampleName);

                var lightType = (Light2D.ShapeLightType)i;
                RenderLightSet(
                    camera,
                    lightType,
                    cmdBuffer,
                    layerToRender,
                    m_RenderTargets[i].Identifier(),
                    m_LightTypes[i].globalColor,
                    k_UseShapeLightTypeKeyword + i,
                    Light2D.GetShapeLights(lightType)
                );

                cmdBuffer.EndSample(sampleName);
            }
        }

        static public void RenderLightVolumes(Camera camera, CommandBuffer cmdBuffer, int layerToRender)
        {
            for (int i = 0; i < m_LightTypes.Length; ++i)
            {
                string sampleName = "2D Shape Light Volumes - " + m_LightTypes[i].name;
                cmdBuffer.BeginSample(sampleName);

                var lightType = (Light2D.ShapeLightType)i;
                RenderLightVolumeSet(
                    camera,
                    lightType,
                    cmdBuffer,
                    layerToRender,
                    m_RenderTargets[i].Identifier(),
                    Light2D.GetShapeLights(lightType)
                );

                cmdBuffer.EndSample(sampleName);
            }
        }
    }
}
