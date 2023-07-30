using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
    private bool InSettings = false;
    [SerializeField] private GameObject SettingsWindow;
    [SerializeField] private RaycastTerrain raycastTerrain;
    [SerializeField] private RaycastInput raycastInput;
    private ScreenDivider divider;
    //[SerializeField] private InputSlider[] InputSliders;
    [SerializeField] private GameObject[] SettingsPanels;
    private int ActivePanel = 0;
    

    private void Awake() {
        SettingsWindow.SetActive(false);
        raycastInput = FindObjectOfType<RaycastInput>();
        raycastTerrain = FindObjectOfType<RaycastTerrain>();
        divider = FindObjectOfType<ScreenDivider>();
    }

    private void Start() {
        ChangePanel(ActivePanel);
       // InputSliders[0].value =
    }

    private void Update() {
        if(Input.GetKeyDown(KeyCode.Space)){
            SettingsButton();
        }
        if(Input.GetKeyDown(KeyCode.Escape)){
            Application.Quit();
        }
    }

    public void ChangePanel(int panel){
        for(int i = 0; i < SettingsPanels.Length; i++){
            if(i == panel){
                SettingsPanels[i].SetActive(true);
            }else{
                SettingsPanels[i].SetActive(false);
            }
        }
        ActivePanel = panel;
    }

    public void SettingsButton(){
        raycastInput = FindObjectOfType<RaycastInput>();
        raycastTerrain = FindObjectOfType<RaycastTerrain>();
        if(InSettings){
            SettingsWindow.SetActive(false);
            InSettings = false;
        }else{
            SettingsWindow.SetActive(true);
            InSettings = true;
        }
        /*
        raycastInput.SetPause(InSettings);
        raycastTerrain.SetPause(InSettings);
        divider.SetPause(InSettings);
        */
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
