using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

public class VoxelRenderPipelineInstance : RenderPipeline
{
    // Use this variable to a reference to the Render Pipeline Asset that was passed to the constructor
    private VoxelRenderPipelineAsset _renderPipelineAsset;
    private Stopwatch st = new Stopwatch();

    // Camera renderer
    CameraRenderer renderer = new CameraRenderer();

    // The constructor has an instance of the ExampleRenderPipelineAsset class as its parameter.
    public VoxelRenderPipelineInstance(VoxelRenderPipelineAsset asset)
    {
        _renderPipelineAsset = asset;
        st.Start();
    }

    // Unity calls this method once per frame for each CameraType that is currently rendering.
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        // Measure time between frames
        st.Stop();
        Settings.Instance.frameTime = st.ElapsedMilliseconds;
        st = new Stopwatch();
        st.Start();

        // Execute Render function for each camera
        foreach (Camera camera in cameras)
        {
            renderer.Render(context, camera, _renderPipelineAsset.MotionVectorShader, _renderPipelineAsset.ReprojectionShader, _renderPipelineAsset.BlitShader,
                _renderPipelineAsset.FilterShader, _renderPipelineAsset.VarianceShader);
        }

        // Tell the Scriptable Render Context to tell the graphics API to perform the scheduled commands
        context.Submit();
    }
}
