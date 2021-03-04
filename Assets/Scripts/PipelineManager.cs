using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The pipeline manager.
/// </summary>
public class PipelineManager : MonoBehaviour
{
    /// <summary>
    /// The new render pipeline asset.
    /// </summary>
    public RenderPipelineAsset renderPipelineAsset;

    /// <summary>
    /// The old (default) render pipeline asset.
    /// </summary>
    private RenderPipelineAsset _defaultRenderPipelineAsset;

    /// <summary>
    /// On Start, set render pipeline asset to ray tracing one.
    /// </summary>
    public IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();

        _defaultRenderPipelineAsset = GraphicsSettings.renderPipelineAsset;
        GraphicsSettings.renderPipelineAsset = renderPipelineAsset;
    }

    /// <summary>
    /// OnDestroy set render pipeline asset back to default.
    /// </summary>
    public void OnDestroy()
    {
        GraphicsSettings.renderPipelineAsset = _defaultRenderPipelineAsset;
    }
}