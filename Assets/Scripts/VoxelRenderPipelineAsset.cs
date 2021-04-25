using UnityEngine;
using UnityEngine.Rendering;

// The CreateAssetMenu attribute lets you create instances of this class in the Unity Editor.
[CreateAssetMenu(menuName = "Rendering/VoxelRenderPipelineAsset")]
public class VoxelRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    public ComputeShader MotionVectorShader;

    [SerializeField]
    public ComputeShader ReprojectionShader;

    [SerializeField]
    public ComputeShader VarianceShader;

    [SerializeField]
    public ComputeShader FilterShader;

    [SerializeField]
    public ComputeShader BlitShader;

    // Unity calls this method before rendering the first frame.
    // If a setting on the Render Pipeline Asset changes, Unity destroys the current Render Pipeline Instance and calls this method again before rendering the next frame.
    protected override RenderPipeline CreatePipeline()
    {
        // Instantiate the Render Pipeline that this custom SRP uses for rendering, and pass a reference to this Render Pipeline Asset.
        // The Render Pipeline Instance can then access the configuration data defined above.
        return new VoxelRenderPipelineInstance(this);
    }
}