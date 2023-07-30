using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenDivider : MonoBehaviour
{

    [SerializeField] private Camera RightCamera, LeftCamera;
    private RaycastInput drawingScript;
    private RaycastTerrain terrainScript;
    private float mousePos;
    private bool dragging = false;
    private bool paused = false;

    public void SetPause(bool pause){
        paused = pause;
    }

    private void Awake() {
        drawingScript = FindObjectOfType<RaycastInput>();
        terrainScript = FindObjectOfType<RaycastTerrain>();
    }

    private void Start() {
        mousePos = 0.5f;
    }
    
    private void OnMouseDrag() {
        if(!paused){
            if(!dragging){
                dragging = true;
                StartCoroutine(StopScripts());
            }
            mousePos = Mathf.Clamp(Input.mousePosition.x/Screen.width, 0.1f, 0.9f);
            //RightCamera.rect.Set(Input.mousePosition.x/Screen.width, RightCamera.rect.y, 1 - Input.mousePosition.x/Screen.width, RightCamera.rect.height);
            RightCamera.rect = new Rect(mousePos, RightCamera.rect.y, 1 - mousePos, RightCamera.rect.height);
            LeftCamera.rect = new Rect(LeftCamera.rect.x, LeftCamera.rect.y, mousePos, LeftCamera.rect.height);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(!dragging){
            if(Input.mousePosition.x/Screen.width < mousePos){
                drawingScript.enabled = true;
                terrainScript.enabled = false;
            }
            else{
                drawingScript.enabled = false;
                terrainScript.enabled = true;
            }
        }
    }

    private IEnumerator StopScripts(){
        drawingScript.enabled = false;
        terrainScript.enabled = false;
        yield return new WaitForSeconds(0.3f);
        dragging = false;
    }
}
