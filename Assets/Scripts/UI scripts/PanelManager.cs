using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PanelManager : MonoBehaviour
{
    [SerializeField] private InputSlider[] InputSliders;
    
    private void Start() {
    }

    public void OpenTooltip(int index){
        for(int i = 0; i < InputSliders.Length; i++){
            if(i == index){
                InputSliders[i].OpenTooltip();
            }else{
                InputSliders[i].CloseTooltip();
            }
        }
    }
}
