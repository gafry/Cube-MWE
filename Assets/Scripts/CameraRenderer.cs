﻿using UnityEngine;
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

    private readonly Dictionary<int, Vector4> _outputTargetSizes = new Dictionary<int, Vector4>();
    protected readonly int _outputTargetSizeShaderId = Shader.PropertyToID("_OutputTargetSize");

    private int _frameIndex = 0;
    private readonly int _frameIndexShaderId = Shader.PropertyToID("_FrameIndex");
    private int _frameCounter = 0;
    private readonly int _frameCounterShaderId = Shader.PropertyToID("_FrameCounter");

    public Texture2D _materials;

    private readonly Dictionary<string, RTHandle> _Buffers = new Dictionary<string, RTHandle>();

    // Variables for shaders
    // Acceleration structure
    private readonly int _accelerationStructureShaderId = Shader.PropertyToID("_AccelerationStructure");
    // Output Targets
    private readonly int _outputTargetShaderId = Shader.PropertyToID("_OutputTarget");
    private readonly int _outputTarget2ShaderId = Shader.PropertyToID("_OutputTarget2");
    private readonly int _outputTarget3ShaderId = Shader.PropertyToID("_OutputTarget3");
    private readonly int _outputTarget4ShaderId = Shader.PropertyToID("_OutputTarget4");
    // GBuffer
    private readonly int _GBufferPositionsAndDepthId = Shader.PropertyToID("GBufferPositionsAndDepth");
    private readonly int _GBufferMotionVectorAndIDId = Shader.PropertyToID("GBufferMotionVectorAndID");
    private readonly int _GBufferNormalsAndMaterialsId = Shader.PropertyToID("GBufferNormalsAndMaterials");
    private readonly int _GBufferAlbedoId = Shader.PropertyToID("GBufferAlbedo");

    private readonly int _CameraXId = Shader.PropertyToID("CameraX");
    private readonly int _CameraYId = Shader.PropertyToID("CameraY");
    private readonly int _WithIDId = Shader.PropertyToID("WithID");
    private readonly int _PrevGBufferMotionVectorAndIDId = Shader.PropertyToID("PrevGBufferMotionVectorAndID");
    private readonly int _PrevViewProjId = Shader.PropertyToID("PrevViewProj");
    private readonly int _LastFrameId = Shader.PropertyToID("LastFrame");
    private readonly int _CurrentFrameId = Shader.PropertyToID("CurrentFrame");
    private readonly int _AOBufferId = Shader.PropertyToID("AOBuffer");
    private readonly int _IndirectBufferId = Shader.PropertyToID("IndirectBuffer");
    private readonly int _PrevIndirectBufferId = Shader.PropertyToID("PrevIndirectBuffer");
    private readonly int _ReprojectedOutputId = Shader.PropertyToID("ReprojectedOutput");
    private readonly int _ReprojectedOutputIndirectId = Shader.PropertyToID("ReprojectedOutputIndirect");
    private readonly int _VarianceBufferId = Shader.PropertyToID("VarianceBuffer");
    private readonly int _PrevGBufferNormalsAndMaterialsId = Shader.PropertyToID("PrevGBufferNormals");
    private readonly int _PrevGBufferPositionsAndDepthId = Shader.PropertyToID("PrevGBufferPosition");
    private readonly int _ColorInputId = Shader.PropertyToID("ColorInput");
    private readonly int _ShadowInputId = Shader.PropertyToID("ShadowInput");
    private readonly int _BlitOutputId = Shader.PropertyToID("BlitOutput");
    private readonly int _StepWidthId = Shader.PropertyToID("StepWidth");
    private readonly int _IterationId = Shader.PropertyToID("Iteration");
    private readonly int _LightPositionId = Shader.PropertyToID("LightPosition");
    private readonly int _LightProgressId = Shader.PropertyToID("LightProgress");
    private readonly int _MaterialsId = Shader.PropertyToID("Materials");
    private readonly int _StartCoefId = Shader.PropertyToID("StartCoef");
    private readonly int _AdaptCoefId = Shader.PropertyToID("AdaptCoef");
    private readonly int _MinCoefId = Shader.PropertyToID("MinCoef");
    private readonly int _AOCoefId = Shader.PropertyToID("AOCoef");
    private readonly int _depthOfRecursionShaderId = Shader.PropertyToID("_DepthOfRecursion");
    private readonly int _VarianceId = Shader.PropertyToID("Variance");
    private readonly int _SoftShadowsOnId = Shader.PropertyToID("SoftShadowsOn");
    private readonly int _IndirectLightingOnId = Shader.PropertyToID("IndirectLightingOn");
    private readonly int _WithIndirectOnId = Shader.PropertyToID("WithIndirectOn");
    private readonly int _LightIntensityId = Shader.PropertyToID("LightIntensity");

    public void Render(ScriptableRenderContext context, Camera camera, ComputeShader MotionVectorShader, ComputeShader ReprojectionShader,
                       ComputeShader BlitShader, ComputeShader FilterShader, ComputeShader VarianceShader, bool bAmbientOcclusion, 
                       bool bDirectLighting, bool bIndirectLighting, bool bReprojection, bool bVariance, bool bFiltering, bool bReprojectWithIDs,
                       bool bCombineAlbedoAndShadows, bool bDayNightEfect, bool bSoftShadowsOn)
    {
        // Load shaders data
        _shader = Resources.Load<RayTracingShader>("RayTrace");
        _shaderGBuffer = Resources.Load<RayTracingShader>("GBuffer");
        _shaderAO = Resources.Load<RayTracingShader>("AO");
        _shaderLocalLights = Resources.Load<RayTracingShader>("TraceLocalLights");
        _shaderCubeLights = Resources.Load<RayTracingShader>("CubeLights");
        //Debug.Log(Settings.Instance.directLightingOn + " " + Settings.Instance.indirectLightingOn + " " + Settings.Instance.combineAlbedoAndShadows + " " 
        //    + Settings.Instance.AO + " " + Settings.Instance.reprojectionOn + " " + Settings.Instance.varianceOn + " " + Settings.Instance.filteringOn);
        // Set camera variables
        SetupCamera(camera);

        // Request acceleration structure from scene manager
        var accelerationStructure = SceneManager.Instance.RequestAccelerationStructure();

        var outputTargetSize = RequireOutputTargetSize(camera);

        // Request buffers
        // G-buffer
        var gBufferAlbedo = RequireBuffer(camera, "gBufferAlbedo");
        var gBufferMotionAndIDs = RequireBuffer(camera, "gBufferMotionAndIDs");
        var gBufferNormalsAndMaterials = RequireBuffer(camera, "gBufferNormalsAndMaterials");
        var gBufferWorldPositionsAndDepth = RequireBuffer(camera, "gBufferWorldPositionsAndDepth");
        // Shadow buffers
        var directLightBuffer = RequireBuffer(camera, "directLightBuffer");
        var indirectLightBuffer = RequireBuffer(camera, "indirectLightBuffer");
        var ambientOcclusionBuffer = RequireBuffer(camera, "ambientOcclusionBuffer");
        // Previous frame data
        var prevGBufferNormalsAndMaterials = RequireBuffer(camera, "prevGBufferNormalsAndMaterials");
        var prevGBufferMotionAndIDs = RequireBuffer(camera, "prevGBufferMotionAndID");
        var prevGBufferPositionsAndDepth = RequireBuffer(camera, "prevGBufferPositionsAndDepth");
        var prevDirectLightBuffer = RequireBuffer(camera, "prevDirectLightBuffer");
        var prevIndirectLightBuffer = RequireBuffer(camera, "prevIndirectLightBuffer");
        // SVGF buffers
        var reprojectedDirectBuffer = RequireBuffer(camera, "reprojectedDirectBuffer");
        var reprojectedIndirectBuffer = RequireBuffer(camera, "reprojectedIndirectBuffer");
        var varianceBuffer = RequireBuffer(camera, "varianceBuffer");        
        var blitBuffer = RequireBuffer(camera, "blitBuffer");
        var filterDirectBuffer = RequireBuffer(camera, "filterDirectBuffer");
        var filterIndirectBuffer = RequireBuffer(camera, "filterIndirectBuffer");

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
                    cmd.SetRayTracingAccelerationStructure(_shaderGBuffer, _accelerationStructureShaderId, accelerationStructure);
                    cmd.SetRayTracingTextureParam(_shaderGBuffer, _outputTargetShaderId, gBufferNormalsAndMaterials);
                    cmd.SetRayTracingTextureParam(_shaderGBuffer, _outputTarget2ShaderId, gBufferWorldPositionsAndDepth);
                    cmd.SetRayTracingTextureParam(_shaderGBuffer, _outputTarget3ShaderId, gBufferAlbedo);
                    cmd.SetRayTracingTextureParam(_shaderGBuffer, _outputTarget4ShaderId, gBufferMotionAndIDs);
                    cmd.SetRayTracingTextureParam(_shaderGBuffer, _MaterialsId, _materials);
                    if (bDayNightEfect)
                        cmd.SetRayTracingVectorParam(_shaderGBuffer, _LightPositionId, SceneManager.Instance.GetSunPosition());
                    else
                        cmd.SetRayTracingVectorParam(_shaderGBuffer, _LightPositionId, new Vector3(530.0f, 500.0f, 370.0f));
                    cmd.SetRayTracingFloatParam(_shaderGBuffer, _LightProgressId, SceneManager.Instance.GetSunProgress());
                    cmd.DispatchRays(_shaderGBuffer, "GBufferRaygenShader", (uint)gBufferNormalsAndMaterials.rt.width, (uint)gBufferNormalsAndMaterials.rt.height, 1, camera);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                using (new ProfilingScope(cmd, new ProfilingSampler("Motion Vector")))
                {
                    _motionVectorKernelId = MotionVectorShader.FindKernel("MotionVector");
                    cmd.SetComputeTextureParam(MotionVectorShader, _motionVectorKernelId, _GBufferPositionsAndDepthId, gBufferWorldPositionsAndDepth);
                    cmd.SetComputeTextureParam(MotionVectorShader, _motionVectorKernelId, _outputTargetShaderId, gBufferMotionAndIDs);
                    cmd.SetComputeMatrixParam(MotionVectorShader, _PrevViewProjId, PrevViewProjMatrix);
                    cmd.SetComputeFloatParam(MotionVectorShader, _CameraXId, outputTargetSize.x);
                    cmd.SetComputeFloatParam(MotionVectorShader, _CameraYId, outputTargetSize.y);
                    cmd.DispatchCompute(MotionVectorShader, _motionVectorKernelId, gBufferWorldPositionsAndDepth.rt.width / 24, gBufferWorldPositionsAndDepth.rt.height / 24, 1);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Save view projection matrix for next frame
                PrevViewProjMatrix = ActViewProjMatrix;

                // If ray tracing is off, show normals
                if (!bAmbientOcclusion && !bDirectLighting && !bIndirectLighting)
                {
                    cmd.Blit(gBufferAlbedo, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    return;
                }
            }

            // Ray tracing pass for direct and indirect lighting
            if (bDirectLighting || bIndirectLighting)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Direct and indirect lighting")))
                {
                    // Acceleration structure and pass
                    cmd.SetRayTracingShaderPass(_shader, "DirectLighting");
                    cmd.SetRayTracingAccelerationStructure(_shader, _accelerationStructureShaderId, accelerationStructure);
                    // GBuffer
                    cmd.SetRayTracingTextureParam(_shader, _GBufferNormalsAndMaterialsId, gBufferNormalsAndMaterials);
                    cmd.SetRayTracingTextureParam(_shader, _GBufferPositionsAndDepthId, gBufferWorldPositionsAndDepth);
                    // Shadow buffers
                    cmd.SetRayTracingTextureParam(_shader, _outputTargetShaderId, directLightBuffer);
                    cmd.SetRayTracingTextureParam(_shader, _outputTarget2ShaderId, indirectLightBuffer);
                    if (bIndirectLighting)
                        cmd.SetRayTracingIntParam(_shader, _IndirectLightingOnId, 1);
                    else
                        cmd.SetRayTracingIntParam(_shader, _IndirectLightingOnId, 0);
                    // Sun and day/night
                    /*if (Settings.Instance.dayNightEfect)
                        cmd.SetRayTracingVectorParam(_shader, _LightPositionId, SceneManager.Instance.GetSunPosition());
                    else
                        cmd.SetRayTracingVectorParam(_shader, _LightPositionId, new Vector3(530.0f, 500.0f, 370.0f));*/
                    if (bDayNightEfect)
                        cmd.SetGlobalVector(_LightPositionId, SceneManager.Instance.GetSunPosition());
                    else
                        cmd.SetGlobalVector(_LightPositionId, new Vector3(530.0f, 500.0f, 370.0f));
                    // Variables
                    cmd.SetRayTracingIntParam(_shader, _frameIndexShaderId, _frameIndex);
                    cmd.SetRayTracingIntParam(_shader, _frameCounterShaderId, _frameCounter);
                    cmd.SetRayTracingIntParam(_shader, _depthOfRecursionShaderId, Settings.Instance.depthOfRecursion);
                    cmd.SetRayTracingFloatParam(_shader, _LightProgressId, SceneManager.Instance.GetSunProgress());
                    if (bSoftShadowsOn)
                        cmd.SetRayTracingIntParam(_shader, _SoftShadowsOnId, 1);
                    else
                        cmd.SetRayTracingIntParam(_shader, _SoftShadowsOnId, 0);                    
                    // Dispatch
                    cmd.DispatchRays(_shader, "DirectAndIndirectRaygenShader", (uint)directLightBuffer.rt.width, (uint)directLightBuffer.rt.height, 1, camera);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // Ambient oclussion pass
            if (bAmbientOcclusion)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("AmbientOcclusion")))
                {
                    // Acceleration structure and pass
                    cmd.SetRayTracingShaderPass(_shaderAO, "AO");
                    cmd.SetRayTracingAccelerationStructure(_shaderAO, _accelerationStructureShaderId, accelerationStructure);
                    // GBuffer
                    cmd.SetRayTracingTextureParam(_shaderAO, _GBufferNormalsAndMaterialsId, gBufferNormalsAndMaterials);
                    cmd.SetRayTracingTextureParam(_shaderAO, _GBufferPositionsAndDepthId, gBufferWorldPositionsAndDepth);
                    // Output
                    cmd.SetRayTracingTextureParam(_shaderAO, _outputTargetShaderId, ambientOcclusionBuffer);
                    // Variables
                    cmd.SetRayTracingIntParam(_shaderAO, _frameIndexShaderId, _frameIndex);
                    cmd.SetRayTracingIntParam(_shaderAO, _frameCounterShaderId, _frameCounter);
                    cmd.SetRayTracingFloatParam(_shaderAO, _AOCoefId, Mathf.Max(1, Settings.Instance.AO));
                    // Dispatch
                    cmd.DispatchRays(_shaderAO, "AORaygenShader", (uint)ambientOcclusionBuffer.rt.width, (uint)ambientOcclusionBuffer.rt.height, 1, camera);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // Combine AO and direct lighting
            if (bAmbientOcclusion && bDirectLighting)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Combine shadows and AO")))
                {
                    _blitKernelId = BlitShader.FindKernel("BlitAOWithDirect");
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _ShadowInputId, ambientOcclusionBuffer);
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _ColorInputId, directLightBuffer);
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _BlitOutputId, blitBuffer);
                    cmd.DispatchCompute(BlitShader, _blitKernelId, blitBuffer.rt.width / 24, blitBuffer.rt.height / 24, 1);
                    cmd.Blit(blitBuffer, directLightBuffer);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
            else if (bAmbientOcclusion)
            {
                cmd.Blit(ambientOcclusionBuffer, directLightBuffer);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // Reprojection
            if (bReprojection)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Reprojection")))
                {
                    // Set buffers with shadows (based on settings)
                    if (bIndirectLighting && (bDirectLighting || bAmbientOcclusion))
                    {
                        _reprojectionKernelId = ReprojectionShader.FindKernel("ReprojectionWithIndirect");                        
                        cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _CurrentFrameId, directLightBuffer);
                        cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _IndirectBufferId, indirectLightBuffer);
                        cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _LastFrameId, prevDirectLightBuffer);
                        cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _PrevIndirectBufferId, prevIndirectLightBuffer);
                        cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _outputTargetShaderId, reprojectedDirectBuffer);
                        cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _outputTarget2ShaderId, reprojectedIndirectBuffer);
                    }
                    else
                    {
                        _reprojectionKernelId = ReprojectionShader.FindKernel("Reprojection");
                        if (bIndirectLighting)
                        {
                            cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _CurrentFrameId, indirectLightBuffer);
                            cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _LastFrameId, prevIndirectLightBuffer);
                            cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _outputTargetShaderId, reprojectedIndirectBuffer);
                        }
                        else
                        {
                            cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _CurrentFrameId, directLightBuffer);
                            cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _LastFrameId, prevDirectLightBuffer);
                            cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _outputTargetShaderId, reprojectedDirectBuffer);
                        }
                    }                    
                    // G-Buffer
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _GBufferMotionVectorAndIDId, gBufferMotionAndIDs);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _GBufferNormalsAndMaterialsId, gBufferNormalsAndMaterials);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _GBufferPositionsAndDepthId, gBufferWorldPositionsAndDepth);
                    // Prev G-Buffer
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _PrevGBufferPositionsAndDepthId, prevGBufferPositionsAndDepth);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _PrevGBufferNormalsAndMaterialsId, prevGBufferNormalsAndMaterials);
                    cmd.SetComputeTextureParam(ReprojectionShader, _reprojectionKernelId, _PrevGBufferMotionVectorAndIDId, prevGBufferMotionAndIDs);
                    // Window size
                    cmd.SetComputeFloatParam(ReprojectionShader, _CameraXId, outputTargetSize.x);
                    cmd.SetComputeFloatParam(ReprojectionShader, _CameraYId, outputTargetSize.y);
                    // If acceleration structure is rebuilding, turn off ID check
                    if (bReprojectWithIDs) cmd.SetComputeIntParam(ReprojectionShader, _WithIDId, 1);
                    else cmd.SetComputeIntParam(ReprojectionShader, _WithIDId, 0);
                    // Helpers
                    cmd.SetComputeFloatParam(ReprojectionShader, _AdaptCoefId, Settings.Instance.AdaptCoef);
                    cmd.SetComputeFloatParam(ReprojectionShader, _MinCoefId, Settings.Instance.MinCoef);
                    cmd.SetComputeFloatParam(ReprojectionShader, _StartCoefId, Settings.Instance.StartCoef);
                    // Dispatch compute shader
                    cmd.DispatchCompute(ReprojectionShader, _reprojectionKernelId, reprojectedDirectBuffer.rt.width / 24, reprojectedDirectBuffer.rt.height / 24, 1);
                    // Set this frame data for next frame reprojection
                    cmd.Blit(gBufferNormalsAndMaterials, prevGBufferNormalsAndMaterials);
                    cmd.Blit(gBufferMotionAndIDs, prevGBufferMotionAndIDs);
                    cmd.Blit(gBufferWorldPositionsAndDepth, prevGBufferPositionsAndDepth);
                    if (bIndirectLighting) cmd.Blit(reprojectedIndirectBuffer, prevIndirectLightBuffer);
                    if (bDirectLighting || bAmbientOcclusion) cmd.Blit(reprojectedDirectBuffer, prevDirectLightBuffer);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
            else
            {
                if (bIndirectLighting) cmd.Blit(indirectLightBuffer, reprojectedIndirectBuffer);
                if (bDirectLighting || bAmbientOcclusion) cmd.Blit(directLightBuffer, reprojectedDirectBuffer);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // variance
            if (bVariance)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Variance")))
                {
                    if (bIndirectLighting && (bDirectLighting || bAmbientOcclusion))
                    {
                        _varianceKernelId = VarianceShader.FindKernel("EstimateVarianceWithIndirect");
                        cmd.SetComputeTextureParam(VarianceShader, _varianceKernelId, _ShadowInputId, reprojectedDirectBuffer);
                        cmd.SetComputeTextureParam(VarianceShader, _varianceKernelId, _IndirectBufferId, reprojectedIndirectBuffer);
                    }
                    else
                    {
                        if (bIndirectLighting)
                        {
                            _varianceKernelId = VarianceShader.FindKernel("EstimateVarianceIndirect");
                            cmd.SetComputeTextureParam(VarianceShader, _varianceKernelId, _ShadowInputId, reprojectedIndirectBuffer);
                        }
                        else
                        {
                            _varianceKernelId = VarianceShader.FindKernel("EstimateVariance");
                            cmd.SetComputeTextureParam(VarianceShader, _varianceKernelId, _ShadowInputId, reprojectedDirectBuffer);
                        }
                    }
                    cmd.SetComputeTextureParam(VarianceShader, _varianceKernelId, _outputTargetShaderId, varianceBuffer);
                    cmd.SetComputeFloatParam(VarianceShader, _CameraXId, outputTargetSize.x);
                    cmd.SetComputeFloatParam(VarianceShader, _CameraYId, outputTargetSize.y);
                    cmd.SetComputeFloatParam(VarianceShader, _StartCoefId, Settings.Instance.StartCoef);
                    cmd.DispatchCompute(VarianceShader, _varianceKernelId, varianceBuffer.rt.width / 24, varianceBuffer.rt.height / 24, 1);
                    /*_varianceKernelId = VarianceShader.FindKernel("GaussianVariance");
                    cmd.SetComputeTextureParam(VarianceShader, _varianceKernelId, _HistoryBufferId, historyBuffer);
                    cmd.SetComputeFloatParam(VarianceShader, _CameraXId, outputTargetSize.x);
                    cmd.SetComputeFloatParam(VarianceShader, _CameraYId, outputTargetSize.y);
                    cmd.DispatchCompute(VarianceShader, _varianceKernelId, historyBuffer.rt.width / 24, historyBuffer.rt.height / 24, 1);*/
                }

                /*if (!bFiltering && !bCombineAlbedoAndShadows)
                {
                    if (bIndirectLighting) cmd.Blit(varianceBuffer, reprojectedIndirectBuffer);
                    else cmd.Blit(varianceBuffer, reprojectedDirectBuffer);
                }*/                    

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // A Trous filtering
            if (bFiltering && (bIndirectLighting || bSoftShadowsOn))
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("A Trous Filtering")))
                {
                    int iterations = 7;
                    for (int i = 0; i < iterations; i++)
                    {
                        // Shadow buffers
                        if (bIndirectLighting && (bDirectLighting || bAmbientOcclusion) && bSoftShadowsOn)
                        {
                            _filterKernelId = FilterShader.FindKernel("ATrousWithIndirect");
                            cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _ShadowInputId, reprojectedDirectBuffer);
                            cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _IndirectBufferId, reprojectedIndirectBuffer);
                            cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _outputTargetShaderId, filterDirectBuffer);
                            cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _outputTarget2ShaderId, filterIndirectBuffer);
                            cmd.SetComputeIntParam(FilterShader, _WithIndirectOnId, 1);
                        }
                        else
                        {
                            _filterKernelId = FilterShader.FindKernel("ATrous");
                            if (bIndirectLighting)
                            {
                                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _ShadowInputId, reprojectedIndirectBuffer);
                                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _outputTargetShaderId, filterIndirectBuffer);
                                cmd.SetComputeIntParam(FilterShader, _WithIndirectOnId, 1);
                            }
                            else
                            {
                                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _ShadowInputId, reprojectedDirectBuffer);
                                cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _outputTargetShaderId, filterDirectBuffer);
                                cmd.SetComputeIntParam(FilterShader, _WithIndirectOnId, 0);
                            }
                        }
                        // GBuffer and variance
                        cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _GBufferPositionsAndDepthId, gBufferWorldPositionsAndDepth);
                        cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _GBufferNormalsAndMaterialsId, gBufferNormalsAndMaterials);
                        cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _GBufferNormalsAndMaterialsId, gBufferNormalsAndMaterials);
                        cmd.SetComputeTextureParam(FilterShader, _filterKernelId, _VarianceBufferId, varianceBuffer);
                        // Variables
                        cmd.SetComputeFloatParam(FilterShader, _CameraXId, outputTargetSize.x);
                        cmd.SetComputeFloatParam(FilterShader, _CameraYId, outputTargetSize.y);
                        cmd.SetComputeFloatParam(FilterShader, _StepWidthId, iterations - i);
                        cmd.SetComputeFloatParam(FilterShader, _IterationId, i);
                        if (bVariance)
                            cmd.SetComputeIntParam(FilterShader, _VarianceId, 1);
                        else
                            cmd.SetComputeIntParam(FilterShader, _VarianceId, 0);
                        cmd.SetComputeFloatParam(FilterShader, _AdaptCoefId, Settings.Instance.AdaptCoef);
                        cmd.SetComputeFloatParam(FilterShader, _StartCoefId, Settings.Instance.StartCoef);
                        // Dispatch
                        cmd.DispatchCompute(FilterShader, _filterKernelId, filterDirectBuffer.rt.width / 24, filterDirectBuffer.rt.height / 24, 1);
                        // Copy buffers for next iteration
                        if (bIndirectLighting) cmd.Blit(filterIndirectBuffer, reprojectedIndirectBuffer);
                        if (bDirectLighting || bAmbientOcclusion) cmd.Blit(filterDirectBuffer, reprojectedDirectBuffer);                        
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // combine albedo and shadows
            if (bCombineAlbedoAndShadows)
            {
                using (new ProfilingScope(cmd, new ProfilingSampler("Combine shadows and albedo")))
                {
                    if (bIndirectLighting && (bDirectLighting || bAmbientOcclusion))
                    {
                        _blitKernelId = BlitShader.FindKernel("BlitDirectAndIndirect");
                        cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _ShadowInputId, reprojectedDirectBuffer);
                        cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _IndirectBufferId, reprojectedIndirectBuffer);
                    }
                    else
                    {
                        if (bIndirectLighting)
                        {
                            _blitKernelId = BlitShader.FindKernel("BlitIndirect");
                            cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _IndirectBufferId, reprojectedIndirectBuffer);
                        }
                        else
                        {
                            _blitKernelId = BlitShader.FindKernel("BlitDirect");
                            cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _ShadowInputId, reprojectedDirectBuffer);
                        }
                    }   
                    // GBuffer
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _GBufferNormalsAndMaterialsId, gBufferNormalsAndMaterials);
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _ColorInputId, gBufferAlbedo);
                    // Light intensity
                    cmd.SetComputeFloatParam(BlitShader, _LightProgressId, SceneManager.Instance.GetSunProgress());
                    cmd.SetComputeFloatParam(BlitShader, _LightIntensityId, Settings.Instance.MinCoef);
                    // Output
                    cmd.SetComputeTextureParam(BlitShader, _blitKernelId, _BlitOutputId, blitBuffer);
                    // Dispatch
                    cmd.DispatchCompute(BlitShader, _blitKernelId, blitBuffer.rt.width / 24, blitBuffer.rt.height / 24, 1);
                }
                cmd.Blit(blitBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
            else
            {
                if (bDirectLighting || bAmbientOcclusion)
                    cmd.Blit(reprojectedDirectBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);
                else if (bIndirectLighting)
                    cmd.Blit(reprojectedIndirectBuffer, BuiltinRenderTextureType.CameraTarget, Vector2.one, Vector2.zero);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
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
