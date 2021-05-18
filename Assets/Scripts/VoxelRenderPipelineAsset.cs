using UnityEngine;
using UnityEngine.Rendering;

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

    // Unity calls this method before rendering the first frame
    protected override RenderPipeline CreatePipeline()
    {
        // Instantiate the Render Pipeline that this custom SRP uses for rendering, and pass a reference to this Render Pipeline Asset
        return new VoxelRenderPipelineInstance(this);
    }
}