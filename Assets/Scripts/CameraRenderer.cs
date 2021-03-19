using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;

public class CameraRenderer : MonoBehaviour
{
    private RayTracingShader _shader;
    private RayTracingShader _shaderGBuffer;

    private int _motionVectorKernelId;
    private int _reprojectionKernelId;
    private int _blitKernelId;
    private int _filterKernelId;

    public Texture2D _noiseTexture;

    // Parametrs for camera shader
    private static class CameraShaderParams
    {
        public static readonly int _WorldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
        public static readonly int _InvCameraViewProj = Shader.PropertyToID("_InvCameraViewProj");
        public static readonly int _CameraFarDistance = Shader.PropertyToID("_CameraFarDistance");
    }

    private static Matrix4x4 PrevViewProjMatrix = Matrix4x4.zero;
    private static Matrix4x4 ActViewProjMatrix = Matrix4x4.zero;

    private readonly int _outputTargetShaderId = Shader.PropertyToID("_OutputTarget");
    private readonly int _outputTarget2ShaderId = Shader.PropertyToID("_OutputTarget2");
    private readonly int _outputTarget3ShaderId = Shader.PropertyToID("_OutputTarget3");

    private readonly Dictionary<int, Vector4> _outputTargetSizes = new Dictionary<int, Vector4>();
    protected readonly int _outputTargetSizeShaderId = Shader.PropertyToID("_OutputTargetSize");

    private int _frameIndex = 0;
    private readonly int _frameIndexShaderId = Shader.PropertyToID("_FrameIndex");
    private int _frameCounter = 0;
    private readonly int _frameCounterShaderId = Shader.PropertyToID("_FrameCounter");

    private readonly int _depthOfRecursionShaderId = Shader.PropertyToID("_DepthOfRecursion");

    private readonly Dictionary<string, RTHandle> _Buffers = new Dictionary<string, RTHandle>();

    private readonly int _PositionsId = Shader.PropertyToID("Position");
    private readonly int _CameraXId = Shader.PropertyToID("CameraX");
    private readonly int _CameraYId = Shader.PropertyToID("CameraY");
    private readonly int _MotionVectorId = Shader.PropertyToID("MotionVectorOutput");
    private readonly int _PrevViewProjId = Shader.PropertyToID("PrevViewProj");
    private readonly int _LastFrameId = Shader.PropertyToID("LastFrame");
    private readonly int _CurrentFrameId = Shader.PropertyToID("CurrentFrame");
    private readonly int _ReprojectedOutputId = Shader.PropertyToID("ReprojectedOutput");
    private readonly int _GBufferNormalsId = Shader.PropertyToID("GBufferNormals");
    private readonly int _GBufferAlbedoId = Shader.PropertyToID("GBufferAlbedo");
    private readonly int _PrevGBufferNormalsId = Shader.PropertyToID("PrevGBufferNormals");
    private readonly int _ColorInputId = Shader.PropertyToID("ColorInput");
    private readonly int _ShadowInputId = Shader.PropertyToID("ShadowInput");
    private readonly int _BlitOutputId = Shader.PropertyToID("BlitOutput");
    private readonly int _FilteredOutputId = Shader.PropertyToID("FilteredOutput");
    private readonly int _StepWidthId = Shader.PropertyToID("StepWidth");

    // Light    
    //private readonly int _lightPositionId = Shader.PropertyToID("_LightPosition");

    public void Render(ScriptableRenderContext context, Camera camera, ComputeShader MotionVectorShader, ComputeShader ReprojectionShader,
                       ComputeShader BlitShader, ComputeShader FilterShader)
    {
        // Load shaders data
        _shader = Resources.Load<RayTracingShader>("RayTrace");
        _shaderGBuffer = Resources.Load<RayTracingShader>("GBuffer");

        // Set camera variables
        SetupCamera(camera);

        // Request acceleration structure from scene manager
        var accelerationStructure = SceneManager.Instance.RequestAccelerationStructure();

        var outputTargetSize = RequireOutputTargetSize(camera);

        // Request buffers
        var albedoBuffer = RequireBuffer(camera, "albedoBuffer");
        var globalLightBuffer = RequireBuffer(camera, "globalLightBuffer");
        var motionVectorBuffer = RequireBuffer(camera, "motionVectorBuffer");
        var gBufferNormals = RequireBuffer(camera, "normalsBuffer");
        var gBufferWorldPositions = RequireBuffer(camera, "positionsBuffer");
        var reprojectedBuffer = RequireBuffer(camera, "reprojectedBuffer");
        var prevGlobalLightBuffer = RequireBuffer(camera, "prevGlobalLightBuffer");
        var prevGBufferNormals = RequireBuffer(camera, "prevGBufferNormals");
        var blitBuffer = RequireBuffer(camera, "blitBuffer");
        var filterBuffer = RequireBuffer(camera, "filterBuffer");

        // Ray tracing command
        var cmd = CommandBufferPool.Get("RayTracingCommand");
        try
        {
            // increase frame index (for ground truth) and frame counter (for randomness)
            // comment else statement to disable ground truth
            _frameCounter++;
            if (Settings.Instance.cameraMoved || Settings.Instance.mouseMoved)
                _frameIndex = 1;
            else if (Settings.Instance.groundTruthIfThereIsNoMotion)
                _frameIndex++;

            // get GBuffer - normals and world positions
            // output - gBufferNormals & gBufferWorldPositions
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Normals, Albedo and World Positions")))
                {
                    cmd.SetRayTracingShaderPass(_shaderGBuffer, "GBuffer");
                    cmd.SetRayTracingAccelerationStructure(_shaderGBuffer, SceneManager.Instance.accelerationStructureShaderId, accelerationStructure);
                    cmd.SetRayTracingTextureParam(_shaderGBuffer, _outputTargetShaderId, gBufferNormals);
                    cmd.SetRayTracingTextureParam(_shaderGBuffer, _outputTarget2ShaderId, gBufferWorldPositions);
                    cmd.SetRayTracingTextureParam(_shaderGBuffer, _outputTarget3ShaderId, albedoBuffer);
                    cmd.DispatchRays(_shaderGBuffer, "NormalsRaygenShader", (uint)gBufferWorldPositions.rt.width, (uint)gBufferWorldPositions.rt.height, 1, camera);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                using (new ProfilingScope(cmd, new ProfilingSampler("Motion Vector")))
                {
                    _motionVectorKernelId = MotionVectorShader.FindKernel("MotionVector");
                    cmd.SetComputeTextureParam(MotionVectorShader, _motionVectorKernelId, _PositionsId, gBufferWorldPositions);
                    cmd.SetComputeTextureParam(MotionVectorShader, _motionVectorKernelId, _MotionVectorId, motionVectorBuffer);
                    cmd.SetComputeMatrixParam(MotionVectorShader, _PrevViewProjId, PrevViewProjMatrix);
                    cmd.SetComputeFloatParam(MotionVectorShader, _CameraXId, outputTargetSize.x);
                    cmd.SetComputeFloatParam(MotionVectorShader, _CameraYId, outputTargetSize.y);
                    cmd.DispatchCompute(MotionVectorShader, _motionVectorKernelId, gBufferWorldPositions.rt.width / 24, gBufferWorldPositions.rt.height / 24, 1);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // ray tracing pass to get light from sun
            // output - globalLightBuffer
            if (Settings.Instance.rayTracingOn || Settings.Instance.reprojectionOn)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Global lighting")))
                {
                    cmd.SetRayTracingShaderPass(_shader, "RayTracing");
                    cmd.SetRayTracingAccelerationStructure(_shader, SceneManager.Instance.accelerationStructureShaderId, accelerationStructure);
                    cmd.SetRayTracingTextureParam(_shader, _outputTargetShaderId, globalLightBuffer);
                    cmd.SetRayTracingVectorParam(_shader, _outputTargetSizeShaderId, outputTargetSize);
                    cmd.SetRayTracingIntParam(_shader, _frameIndexShaderId, _frameIndex);
                    cmd.SetRayTracingIntParam(_shader, _frameCounterShaderId, _frameCounter);
                    cmd.SetRayTracingIntParam(_shader, _depthOfRecursionShaderId, Settings.Instance.depthOfRecursion);
                    cmd.DispatchRays(_shader, "MyRaygenShader", (uint)globalLightBuffer.rt.width, (uint)globalLightBuffer.rt.height, 1, camera);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // reprojection
            //if (Settings.Instance.reprojectionOn)
            //{
                using (new ProfilingScope(cmd, new ProfilingSampler("Reprojection")))
                {
                    _reprojectionKernelId = ReprojectionShader.FindKernel("Reprojection");
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _LastFrameId, prevGlobalLightBuffer);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _MotionVectorId, motionVectorBuffer);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _CurrentFrameId, globalLightBuffer);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _ReprojectedOutputId, reprojectedBuffer);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _GBufferNormalsId, gBufferNormals);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _GBufferAlbedoId, albedoBuffer);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _PrevGBufferNormalsId, prevGBufferNormals);
                    cmd.DispatchCompute(ReprojectionShader, _reprojectionKernelId, reprojectedBuffer.rt.width / 24, reprojectedBuffer.rt.height / 24, 1);
                    cmd.Blit(reprojectedBuffer, prevGlobalLightBuffer);
                    cmd.Blit(gBufferNormals, prevGBufferNormals);
                }
            //}

            // A Trous filtering
            //if (Settings.Instance.filtering)
            //{
                using (new ProfilingScope(cmd, new ProfilingSampler("A Trous Filtering")))
                {
                    _filterKernelId = FilterShader.FindKernel("ATrous5x5");
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _ShadowInputId, reprojectedBuffer);
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _PositionsId, gBufferWorldPositions);
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _GBufferNormalsId, gBufferNormals);
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _FilteredOutputId, filterBuffer);
                    cmd.SetComputeFloatParam(FilterShader, _CameraXId, outputTargetSize.x);
                    cmd.SetComputeFloatParam(FilterShader, _CameraYId, outputTargetSize.y);
                    cmd.SetComputeFloatParam(FilterShader, _StepWidthId, 10);
                    cmd.DispatchCompute(FilterShader, _filterKernelId, filterBuffer.rt.width / 24, filterBuffer.rt.height / 24, 1);
                }

                /*using (new ProfilingScope(cmd, new ProfilingSampler("A Trous Filtering")))
                {
                    _filterKernelId = FilterShader.FindKernel("ATrous5x5");
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _ShadowInputId, filterBuffer);
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _PositionsId, gBufferWorldPositions);
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _GBufferNormalsId, gBufferNormals);
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _FilteredOutputId, reprojectedBuffer);
                    cmd.SetComputeFloatParam(FilterShader, _CameraXId, outputTargetSize.x);
                    cmd.SetComputeFloatParam(FilterShader, _CameraYId, outputTargetSize.y);
                    cmd.SetComputeFloatParam(FilterShader, _StepWidthId, 2);
                    cmd.DispatchCompute(FilterShader, _filterKernelId, filterBuffer.rt.width / 24, filterBuffer.rt.height / 24, 1);
                }

                using (new ProfilingScope(cmd, new ProfilingSampler("A Trous Filtering")))
                {
                    _filterKernelId = FilterShader.FindKernel("ATrous5x5");
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _ShadowInputId, reprojectedBuffer);
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _PositionsId, gBufferWorldPositions);
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _GBufferNormalsId, gBufferNormals);
                    cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _FilteredOutputId, filterBuffer);
                    cmd.SetComputeFloatParam(FilterShader, _CameraXId, outputTargetSize.x);
                    cmd.SetComputeFloatParam(FilterShader, _CameraYId, outputTargetSize.y);
                    cmd.SetComputeFloatParam(FilterShader, _StepWidthId, 3);
                    cmd.DispatchCompute(FilterShader, _filterKernelId, filterBuffer.rt.width / 24, filterBuffer.rt.height / 24, 1);
                }

            using (new ProfilingScope(cmd, new ProfilingSampler("A Trous Filtering")))
            {
                _filterKernelId = FilterShader.FindKernel("ATrous5x5");
                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _ShadowInputId, filterBuffer);
                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _PositionsId, gBufferWorldPositions);
                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _GBufferNormalsId, gBufferNormals);
                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _FilteredOutputId, reprojectedBuffer);
                cmd.SetComputeFloatParam(FilterShader, _CameraXId, outputTargetSize.x);
                cmd.SetComputeFloatParam(FilterShader, _CameraYId, outputTargetSize.y);
                cmd.SetComputeFloatParam(FilterShader, _StepWidthId, 4);
                cmd.DispatchCompute(FilterShader, _filterKernelId, filterBuffer.rt.width / 24, filterBuffer.rt.height / 24, 1);
            }

            using (new ProfilingScope(cmd, new ProfilingSampler("A Trous Filtering")))
            {
                _filterKernelId = FilterShader.FindKernel("ATrous5x5");
                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _ShadowInputId, reprojectedBuffer);
                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _PositionsId, gBufferWorldPositions);
                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _GBufferNormalsId, gBufferNormals);
                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _FilteredOutputId, filterBuffer);
                cmd.SetComputeFloatParam(FilterShader, _CameraXId, outputTargetSize.x);
                cmd.SetComputeFloatParam(FilterShader, _CameraYId, outputTargetSize.y);
                cmd.SetComputeFloatParam(FilterShader, _StepWidthId, 5);
                cmd.DispatchCompute(FilterShader, _filterKernelId, filterBuffer.rt.width / 24, filterBuffer.rt.height / 24, 1);
            }*/
            //}

            // combine albedo and AO
            // output - outputTarget
            if (Settings.Instance.combineAlbedoAndShadows)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Combine shadows and albedo")))
                {
                    _blitKernelId = BlitShader.FindKernel("BlitIt");
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _ShadowInputId, reprojectedBuffer);
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _ColorInputId, albedoBuffer);
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _BlitOutputId, blitBuffer);
                    cmd.DispatchCompute(BlitShader, _blitKernelId, blitBuffer.rt.width / 24, blitBuffer.rt.height / 24, 1);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // Final blit - based on settings
            /*using (new ProfilingScope(cmd, new ProfilingSampler("Final Blit")))
            {
                if (Settings.Instance.combineAlbedoAndShadows)
                    cmd.Blit(blitBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
                else if (Settings.Instance.reprojectionOn)
                    cmd.Blit(reprojectedBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
                else if (Settings.Instance.rayTracingOn)
                    cmd.Blit(globalLightBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
                else
                    cmd.Blit(albedoBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
            }*/

            cmd.Blit(filterBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // Save view projection matrix for next frame
            PrevViewProjMatrix = ActViewProjMatrix;
        }
        finally
        {
            CommandBufferPool.Release(cmd);
        }
    }

    // Returns buffer for specified texture
    protected RTHandle RequireBuffer(Camera camera, string bufferName)
    {
        RTHandle outputTarget;
        if (_Buffers.TryGetValue(bufferName, out outputTarget))
            return outputTarget;

        outputTarget = RTHandles.Alloc(
          camera.pixelWidth,
          camera.pixelHeight,
          1,
          DepthBits.None,
          GraphicsFormat.R32G32B32A32_SFloat,
          FilterMode.Point,
          TextureWrapMode.Clamp,
          TextureDimension.Tex2D,
          true,
          false,
          false,
          false,
          1,
          0f,
          MSAASamples.None,
          false,
          false,
          RenderTextureMemoryless.None,
          bufferName + "_${camera.name}");
        _Buffers.Add(bufferName, outputTarget);

        return outputTarget;
    }

    protected Vector4 RequireOutputTargetSize(Camera camera)
    {
        var id = camera.GetInstanceID();

        if (_outputTargetSizes.TryGetValue(id, out var outputTargetSize))
            return outputTargetSize;

        outputTargetSize = new Vector4(camera.pixelWidth, camera.pixelHeight, 1.0f / camera.pixelWidth, 1.0f / camera.pixelHeight);
        _outputTargetSizes.Add(id, outputTargetSize);

        return outputTargetSize;
    }

    // Set up matrices for camera shader
    private static void SetupCamera(Camera camera)
    {
        Shader.SetGlobalVector(CameraShaderParams._WorldSpaceCameraPos, camera.transform.position);
        var projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        var viewMatrix = camera.worldToCameraMatrix;
        var viewProjMatrix = projMatrix * viewMatrix;
        var invViewProjMatrix = Matrix4x4.Inverse(viewProjMatrix);
        Shader.SetGlobalMatrix(CameraShaderParams._InvCameraViewProj, invViewProjMatrix);
        Shader.SetGlobalFloat(CameraShaderParams._CameraFarDistance, camera.farClipPlane);
        ActViewProjMatrix = viewProjMatrix;
    }
}
