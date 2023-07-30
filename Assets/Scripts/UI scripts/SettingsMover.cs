using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsMover : MonoBehaviour
{

    private bool dragging = false;
    private bool paused = false;

    public void SetPause(bool pause){
        paused = pause;
    }

    private void Awake() {
    }

    private void Start() {
    }
    
    //mousePos = Mathf.Clamp(Input.mousePosition.x/Screen.width, 0.1f, 0.9f);


    // Update is called once per frame
    void Update()
    {
        if(dragging){
            float mousePosX = Mathf.Clamp(Input.mousePosition.x, 0f, Screen.width);
            float mousePosY = Mathf.Clamp(Input.mousePosition.y, 0f, Screen.height);
            gameObject.transform.position = new Vector3(mousePosX, mousePosY, 0);
        }
    }

    public void OnStartDrag(){
        dragging = true;
    }
    public void OnEndDrag(){
        dragging = false;
    }

    private void OnDisable() {
        dragging = false;
    }
}
