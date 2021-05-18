using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class PipelineManager : MonoBehaviour
{
    public RenderPipelineAsset renderPipelineAsset;
    private RenderPipelineAsset _defaultRenderPipelineAsset;

    // On Start, set render pipeline asset to ray tracing one.
    public IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();

        _defaultRenderPipelineAsset = GraphicsSettings.renderPipelineAsset;
        GraphicsSettings.renderPipelineAsset = renderPipelineAsset;
    }

    // OnDestroy set render pipeline asset back to default.
    public void OnDestroy()
    {
        GraphicsSettings.renderPipelineAsset = _defaultRenderPipelineAsset;
    }
}