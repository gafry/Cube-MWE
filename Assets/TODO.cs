using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TODO : MonoBehaviour
{
    // Zbavit se tech bilych sracek na horami (mozna v blit shaderu)
    // Pridat lokalni svetla
    // Zkusit udelat 2 meshe pro chunk - jeden se svetelnymi kostkami a druhy s normalnimi
    
    // Na konci je potreba zmenit nastaveni Jobs - vypnout kontroly atd pro maximalni performance
       
    // GraphicsSettings.useScriptableRenderPipelineBatching = true;
    
    // # DXR Extra - Simple Lighting
    /*float factor = isShadowed ? 0.3 : 1.0;
    float nDotL = max(0.f, dot(normal, lightDir));
    float3 hitColor = float3(0.7, 0.7, 0.7)*nDotL*factor;
    payload.colorAndDistance = float4(hitColor, 1);*/
    // hitColor = hitObject->albedo / M_PI * light->intensity * light->color * std::max(0.f, hitNormal.dotProduct(L));
    
    // dodělat ten history buffer a podle něho volit velikost kernelu u filtru
    
    // zkusit u reprojekce AO(i) = lerp(AO(new), AO(i-1), a), a = 0.003
    
    // u svgf je sample code, zkusit stáhnout
    
    // predelat settings do pipeline
}
