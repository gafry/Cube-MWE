using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;

public class CameraRenderer : MonoBehaviour
{
    private RayTracingShader _shader;
    private RayTracingShader _shaderGBuffer;
    private RayTracingShader _shaderAO;
    private RayTracingShader _shaderLocalLights;
    private RayTracingShader _shaderCubeLights;

    private int _motionVectorKernelId;
    private int _reprojectionKernelId;
    private int _varianceKernelId;
    private int _blitKernelId;
    private int _filterKernelId;

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

    public Texture2D _materials;

    private readonly Dictionary<string, RTHandle> _Buffers = new Dictionary<string, RTHandle>();

    private readonly int _PositionsId = Shader.PropertyToID("Position");
    private readonly int _CameraXId = Shader.PropertyToID("CameraX");
    private readonly int _CameraYId = Shader.PropertyToID("CameraY");
    private readonly int _WithIDId = Shader.PropertyToID("WithID");
    private readonly int _MotionVectorId = Shader.PropertyToID("MotionVectorOutput");
    private readonly int _PrevViewProjId = Shader.PropertyToID("PrevViewProj");
    private readonly int _LastFrameId = Shader.PropertyToID("LastFrame");
    private readonly int _CurrentFrameId = Shader.PropertyToID("CurrentFrame");
    private readonly int _ReprojectedOutputId = Shader.PropertyToID("ReprojectedOutput");
    private readonly int _GBufferNormalsId = Shader.PropertyToID("GBufferNormals");
    private readonly int _GBufferAlbedoId = Shader.PropertyToID("GBufferAlbedo");
    private readonly int _HistoryBufferId = Shader.PropertyToID("HistoryBuffer");
    private readonly int _PrevHistoryBufferId = Shader.PropertyToID("PrevHistoryBuffer");
    private readonly int _PrevGBufferNormalsId = Shader.PropertyToID("PrevGBufferNormals");
    private readonly int _PrevGBufferPositionId = Shader.PropertyToID("PrevGBufferPosition");
    private readonly int _ColorInputId = Shader.PropertyToID("ColorInput");
    private readonly int _ShadowInputId = Shader.PropertyToID("ShadowInput");
    private readonly int _BlitOutputId = Shader.PropertyToID("BlitOutput");
    private readonly int _FilteredOutputId = Shader.PropertyToID("FilteredOutput");
    private readonly int _StepWidthId = Shader.PropertyToID("StepWidth");
    private readonly int _IterationId = Shader.PropertyToID("Iteration");
    private readonly int _LightPositionId = Shader.PropertyToID("LightPosition");
    private readonly int _LightsId = Shader.PropertyToID("Lights");
    private readonly int _NumberOfLightsId = Shader.PropertyToID("NumberOfLights");
    private readonly int _PlayerPositionId = Shader.PropertyToID("PlayerPosition");
    private readonly int _MaterialsId = Shader.PropertyToID("Materials");
    private readonly int _StartCoefId = Shader.PropertyToID("StartCoef");
    private readonly int _AdaptCoefId = Shader.PropertyToID("AdaptCoef");
    private readonly int _MinCoefId = Shader.PropertyToID("MinCoef");
    private readonly int _AOCoefId = Shader.PropertyToID("AOCoef");
    private readonly int _depthOfRecursionShaderId = Shader.PropertyToID("_DepthOfRecursion");
    private readonly int _VarianceId = Shader.PropertyToID("Variance");
    private readonly int _SoftShadowsOnId = Shader.PropertyToID("SoftShadowsOn");

    public void Render(ScriptableRenderContext context, Camera camera, ComputeShader MotionVectorShader, ComputeShader ReprojectionShader,
                       ComputeShader BlitShader, ComputeShader FilterShader, ComputeShader VarianceShader)
    {
        // Load shaders data
        _shader = Resources.Load<RayTracingShader>("RayTrace");
        _shaderGBuffer = Resources.Load<RayTracingShader>("GBuffer");
        _shaderAO = Resources.Load<RayTracingShader>("AO");
        _shaderLocalLights = Resources.Load<RayTracingShader>("TraceLocalLights");
        _shaderCubeLights = Resources.Load<RayTracingShader>("CubeLights");

        // Set camera variables
        SetupCamera(camera);

        // Request acceleration structure from scene manager
        var accelerationStructure = SceneManager.Instance.RequestAccelerationStructure();

        var outputTargetSize = RequireOutputTargetSize(camera);

        // Request buffers
        var albedoBuffer = RequireBuffer(camera, "albedoBuffer");
        var directLightBuffer = RequireBuffer(camera, "globalLightBuffer");
        var motionVectorBuffer = RequireBuffer(camera, "motionVectorBuffer");
        var gBufferNormals = RequireBuffer(camera, "normalsBuffer");
        var gBufferWorldPositions = RequireBuffer(camera, "positionsBuffer");
        var reprojectedBuffer = RequireBuffer(camera, "reprojectedBuffer");
        var historyBuffer = RequireBuffer(camera, "historyBuffer");
        var prevHistoryBuffer = RequireBuffer(camera, "prevHistoryBuffer");
        var prevGlobalLightBuffer = RequireBuffer(camera, "prevGlobalLightBuffer");
        var prevGBufferNormals = RequireBuffer(camera, "prevGBufferNormals");
        var prevGBufferPosition = RequireBuffer(camera, "prevGBufferPosition");
        var blitBuffer = RequireBuffer(camera, "blitBuffer");
        var filterBuffer = RequireBuffer(camera, "filterBuffer");
        var aoBuffer = RequireBuffer(camera, "aoBuffer");
        var localLightsBuffer = RequireBuffer(camera, "localLightsBuffer");

        // Ray tracing command
        var cmd = CommandBufferPool.Get("RayTracingCommand");
        try
        {
            // increase frame index (for ground truth) and frame counter (for randomness)
            // comment else statement to disable ground truth
            _frameCounter++;
            if (Settings.Instance.cameraMoved)
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
                    cmd.SetRayTracingTextureParam(_shaderGBuffer, _MaterialsId, _materials);
                    if (Settings.Instance.dayNightEfect)
                        cmd.SetRayTracingVectorParam(_shaderGBuffer, _LightPositionId, SceneManager.Instance.GetSunPosition());
                    else
                        cmd.SetRayTracingVectorParam(_shaderGBuffer, _LightPositionId, new Vector3(530.0f, 500.0f, 370.0f));
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
                using (new ProfilingScope(cmd, new ProfilingSampler("Direct lighting")))
                {
                    cmd.SetRayTracingShaderPass(_shader, "DirectLighting");
                    cmd.SetRayTracingAccelerationStructure(_shader, SceneManager.Instance.accelerationStructureShaderId, accelerationStructure);
                    cmd.SetRayTracingTextureParam(_shader, _outputTargetShaderId, directLightBuffer);
                    cmd.SetRayTracingIntParam(_shader, _frameIndexShaderId, _frameIndex);
                    cmd.SetRayTracingIntParam(_shader, _frameCounterShaderId, _frameCounter);
                    cmd.SetRayTracingIntParam(_shader, _depthOfRecursionShaderId, Settings.Instance.depthOfRecursion);
                    if (Settings.Instance.softShadowsOn)
                        cmd.SetRayTracingIntParam(_shader, _SoftShadowsOnId, 1);
                    else
                        cmd.SetRayTracingIntParam(_shader, _SoftShadowsOnId, 0);
                    cmd.SetRayTracingTextureParam(_shader, _GBufferNormalsId, gBufferNormals);
                    cmd.SetRayTracingTextureParam(_shader, _PositionsId, gBufferWorldPositions);
                    if (Settings.Instance.dayNightEfect)
                        cmd.SetRayTracingVectorParam(_shader, _LightPositionId, SceneManager.Instance.GetSunPosition());
                    else
                        cmd.SetRayTracingVectorParam(_shader, _LightPositionId, new Vector3(530.0f, 500.0f, 370.0f));
                    if (Settings.Instance.dayNightEfect)
                        cmd.SetGlobalVector(_LightPositionId, SceneManager.Instance.GetSunPosition());
                    else
                        cmd.SetGlobalVector(_LightPositionId, new Vector3(530.0f, 500.0f, 370.0f));
                    cmd.DispatchRays(_shader, "DirectRaygenShader", (uint)directLightBuffer.rt.width, (uint)directLightBuffer.rt.height, 1, camera);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // local lights pass
            /*if (Settings.Instance.localLightsOn)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Local lights evaluation")))
                {
                    cmd.SetRayTracingShaderPass(_shaderLocalLights, "LocalLights");
                    cmd.SetRayTracingAccelerationStructure(_shaderLocalLights, SceneManager.Instance.accelerationStructureShaderId, accelerationStructure);
                    cmd.SetRayTracingTextureParam(_shaderLocalLights, _PositionsId, gBufferWorldPositions);
                    cmd.SetRayTracingFloatParams(_shaderLocalLights, _LightsId, lightsPositions);
                    cmd.SetRayTracingTextureParam(_shaderLocalLights, _outputTargetShaderId, localLightsBuffer);
                    cmd.SetRayTracingIntParam(_shaderLocalLights, _frameIndexShaderId, _frameIndex);
                    cmd.SetRayTracingIntParam(_shaderLocalLights, _frameCounterShaderId, _frameCounter);
                    cmd.SetRayTracingIntParam(_shaderLocalLights, _NumberOfLightsId, 3);
                    cmd.DispatchRays(_shaderLocalLights, "LocalLightsShader", (uint)localLightsBuffer.rt.width, (uint)localLightsBuffer.rt.height, 1, camera);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }*/

            // local lights pass
            if (Settings.Instance.localLightsOn)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Local lights evaluation")))
                {
                    cmd.SetRayTracingShaderPass(_shaderCubeLights, "CubeLights");
                    cmd.SetRayTracingAccelerationStructure(_shaderCubeLights, SceneManager.Instance.accelerationStructureShaderId, accelerationStructure);
                    cmd.SetRayTracingTextureParam(_shaderCubeLights, _PositionsId, gBufferWorldPositions);
                    cmd.SetRayTracingTextureParam(_shaderCubeLights, _GBufferNormalsId, gBufferNormals);
                    cmd.SetRayTracingTextureParam(_shaderCubeLights, _outputTargetShaderId, localLightsBuffer);
                    cmd.SetRayTracingIntParam(_shaderCubeLights, _frameIndexShaderId, _frameIndex);
                    cmd.SetRayTracingIntParam(_shaderCubeLights, _frameCounterShaderId, _frameCounter);
                    cmd.DispatchRays(_shaderCubeLights, "CubeLightsRaygenShader", (uint)localLightsBuffer.rt.width, (uint)localLightsBuffer.rt.height, 1, camera);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // ambient oclussion pass
            if (Settings.Instance.AO > 0)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("AmbientOcclusion")))
                {
                    cmd.SetRayTracingShaderPass(_shaderAO, "AO");
                    cmd.SetRayTracingAccelerationStructure(_shaderAO, SceneManager.Instance.accelerationStructureShaderId, accelerationStructure);
                    cmd.SetRayTracingTextureParam(_shaderAO, _GBufferNormalsId, gBufferNormals);
                    cmd.SetRayTracingTextureParam(_shaderAO, _PositionsId, gBufferWorldPositions);
                    cmd.SetRayTracingTextureParam(_shaderAO, _outputTargetShaderId, aoBuffer);
                    cmd.SetRayTracingIntParam(_shaderAO, _frameIndexShaderId, _frameIndex);
                    cmd.SetRayTracingIntParam(_shaderAO, _frameCounterShaderId, _frameCounter);
                    cmd.SetRayTracingFloatParam(_shaderAO, _AOCoefId, Settings.Instance.AO);
                    cmd.DispatchRays(_shaderAO, "AORaygenShader", (uint)aoBuffer.rt.width, (uint)aoBuffer.rt.height, 1, camera);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            if (Settings.Instance.AO > 0 && Settings.Instance.rayTracingOn)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Combine shadows and albedo")))
                {
                    _blitKernelId = BlitShader.FindKernel("BlitAOWithDirect");
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _ShadowInputId, aoBuffer);
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _ColorInputId, directLightBuffer);
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _BlitOutputId, blitBuffer);
                    cmd.DispatchCompute(BlitShader, _blitKernelId, blitBuffer.rt.width / 24, blitBuffer.rt.height / 24, 1);
                    cmd.Blit(blitBuffer, directLightBuffer);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // reprojection
            if (Settings.Instance.reprojectionOn)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Reprojection")))
                {
                    _reprojectionKernelId = ReprojectionShader.FindKernel("Reprojection");
                    if (Settings.Instance.reprojectWithIDs)
                        cmd.SetComputeIntParam(ReprojectionShader, _WithIDId, 1);
                    else
                        cmd.SetComputeIntParam(ReprojectionShader, _WithIDId, 0);                    
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _LastFrameId, prevGlobalLightBuffer);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _MotionVectorId, motionVectorBuffer);
                    if (Settings.Instance.rayTracingOn)
                        cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _CurrentFrameId, directLightBuffer);
                    else if (Settings.Instance.AO > 0)
                        cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _CurrentFrameId, aoBuffer);
                    else
                        cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _CurrentFrameId, localLightsBuffer);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _ReprojectedOutputId, reprojectedBuffer);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _GBufferNormalsId, gBufferNormals);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _PositionsId, gBufferWorldPositions);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _HistoryBufferId, historyBuffer);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _PrevHistoryBufferId, prevHistoryBuffer);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _PrevGBufferPositionId, prevGBufferPosition);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _PrevGBufferNormalsId, prevGBufferNormals);
                    cmd.SetComputeFloatParam(ReprojectionShader, _CameraXId, outputTargetSize.x);
                    cmd.SetComputeFloatParam(ReprojectionShader, _CameraYId, outputTargetSize.y);
                    cmd.SetComputeFloatParam(ReprojectionShader, _AdaptCoefId, Settings.Instance.AdaptCoef);
                    cmd.SetComputeFloatParam(ReprojectionShader, _MinCoefId, Settings.Instance.MinCoef);
                    cmd.SetComputeFloatParam(ReprojectionShader, _StartCoefId, Settings.Instance.StartCoef);
                    cmd.DispatchCompute(ReprojectionShader, _reprojectionKernelId, reprojectedBuffer.rt.width / 24, reprojectedBuffer.rt.height / 24, 1);
                    cmd.Blit(reprojectedBuffer, prevGlobalLightBuffer);
                    cmd.Blit(gBufferNormals, prevGBufferNormals);
                    cmd.Blit(historyBuffer, prevHistoryBuffer);
                    // TODO toto tu nemusi mozna byt + kod co k tomu patri v shaderu
                    cmd.Blit(gBufferWorldPositions, prevGBufferPosition);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
            else
            {
                if (Settings.Instance.rayTracingOn)
                    cmd.Blit(directLightBuffer, reprojectedBuffer);
                else if (Settings.Instance.localLightsOn)
                    cmd.Blit(localLightsBuffer, reprojectedBuffer);
            }

            // variance
            if (Settings.Instance.varianceOn)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Variance")))
                {
                    _varianceKernelId = VarianceShader.FindKernel("EstimateVariance");
                    cmd.SetComputeTextureParam(VarianceShader, _varianceKernelId, _ShadowInputId, reprojectedBuffer);
                    cmd.SetComputeTextureParam(VarianceShader, _varianceKernelId, _HistoryBufferId, historyBuffer);
                    cmd.SetComputeFloatParam(VarianceShader, _CameraXId, outputTargetSize.x);
                    cmd.SetComputeFloatParam(VarianceShader, _CameraYId, outputTargetSize.y);
                    cmd.SetComputeFloatParam(VarianceShader, _StartCoefId, Settings.Instance.StartCoef);
                    cmd.DispatchCompute(VarianceShader, _varianceKernelId, historyBuffer.rt.width / 24, historyBuffer.rt.height / 24, 1);
                }

                if (!Settings.Instance.filteringOn && !Settings.Instance.combineAlbedoAndShadows)
                    cmd.Blit(historyBuffer, reprojectedBuffer);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // A Trous filtering
            if (Settings.Instance.filteringOn)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("A Trous Filtering")))
                {
                    int iterations = 7;
                    for (int i = 0; i < iterations; i++)
                    {
                        _filterKernelId = FilterShader.FindKernel("ATrous");
                        cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _ShadowInputId, reprojectedBuffer);
                        cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _PositionsId, gBufferWorldPositions);
                        cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _GBufferNormalsId, gBufferNormals);
                        cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _FilteredOutputId, filterBuffer);
                        cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _HistoryBufferId, historyBuffer);
                        cmd.SetComputeFloatParam(FilterShader, _CameraXId, outputTargetSize.x);
                        cmd.SetComputeFloatParam(FilterShader, _CameraYId, outputTargetSize.y);
                        cmd.SetComputeFloatParam(FilterShader, _StepWidthId, /*iterations - i*/Mathf.Max(i, 1));
                        cmd.SetComputeFloatParam(FilterShader, _IterationId, i);
                        if (Settings.Instance.varianceOn)
                            cmd.SetComputeIntParam(FilterShader, _VarianceId, 1);
                        else
                            cmd.SetComputeIntParam(FilterShader, _VarianceId, 0);
                        cmd.DispatchCompute(FilterShader, _filterKernelId, filterBuffer.rt.width / 24, filterBuffer.rt.height / 24, 1);
                        cmd.Blit(filterBuffer, reprojectedBuffer);
                    }

                    //cmd.Blit(reprojectedBuffer, prevGlobalLightBuffer);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

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
                    cmd.Blit(blitBuffer, reprojectedBuffer);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

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

            //cmd.Blit(localLightsBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
            cmd.Blit(reprojectedBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
            //cmd.Blit(motionVectorBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
            //cmd.Blit(historyBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
            //cmd.Blit(albedoBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);

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
