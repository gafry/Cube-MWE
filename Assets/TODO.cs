using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TODO : MonoBehaviour
{
    // Zkusit vypnout reprojekci po nacteni chunku
    // Zbavit se tech bilych sracek na horami (mozna v blit shaderu)
    // Pridat lokalni svetla
    // Zkusit udelat 2 meshe pro chunk - jeden se svetelnymi kostkami a druhy s normalnimi
    
    // Na konci je potreba zmenit nastaveni Jobs - vypnout kontroly atd pro maximalni performance
    /* Pokud to nacitani chunku nebude stacit a bude dlouho trvat, jde jeste udelat, aby se prekryvalo
       FinishcreatingChunk() s generovanim bloku, protoze dokoncovani chunku jede jen na hlavnim vlakne,
       ale bloky se generuji jen na workerech*/
       
    // GraphicsSettings.useScriptableRenderPipelineBatching = true;
    
    // rayIntersection.color = _Color * 0.5f * color;
    
    // # DXR Extra - Simple Lighting
    /*float factor = isShadowed ? 0.3 : 1.0;
    float nDotL = max(0.f, dot(normal, lightDir));
    float3 hitColor = float3(0.7, 0.7, 0.7)*nDotL*factor;
    payload.colorAndDistance = float4(hitColor, 1);*/
    
    // dodělat ten history buffer a podle něho volit velikost kernelu u filtru
}
