using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DisableRaysOnEnter : MonoBehaviour
{
    [SerializeField] private RaycastTerrain raycastTerrain;
    [SerializeField] private RaycastInput raycastInput;
    private ScreenDivider divider;
    

    private void Awake() {
        raycastInput = FindObjectOfType<RaycastInput>();
        raycastTerrain = FindObjectOfType<RaycastTerrain>();
        divider = FindObjectOfType<ScreenDivider>();
    }

    public void PointerEnter(){
        raycastInput.SetPause(true);
        raycastTerrain.SetPause(true);
        divider.SetPause(true);
    }

    public void PointerExit(){
        raycastInput.SetPause(false);
        raycastTerrain.SetPause(false);
        divider.SetPause(false);
    }
}
