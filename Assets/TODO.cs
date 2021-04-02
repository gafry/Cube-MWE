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
}
