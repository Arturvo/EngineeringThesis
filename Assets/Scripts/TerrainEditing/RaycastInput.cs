using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastInput : MonoBehaviour
{
    [SerializeField] private DrawingTool drawTool;
    [SerializeField] private Camera mcamera;
    [SerializeField] private AudioSource audioSource;
    private UserInputPlaneScript inputPlane;
    int layerMask = 1 << 9;
    private bool paused = false;

    public void SetPause(bool pause){
        paused = pause;
    }


    private void Awake() {
        inputPlane = FindObjectOfType<UserInputPlaneScript>();
        audioSource = GetComponent<AudioSource>();
        audioSource.mute = true;
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButton(0) && !paused){
            RaycastHit hit;
            Ray ray = mcamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask)) {
                //audioSource.mute = false;
                Transform objectHit = hit.transform;
                drawTool.transform.position = hit.point;
                drawTool.KeepDrawing();
                //terrainTool.KeepAdding();
            }else{
                drawTool.StopDrawing();
                //terrainTool.StopAdding();
                //audioSource.mute = true;
            }
        }
        else{
            drawTool.StopDrawing();
            //terrainTool.StopAdding();
                //audioSource.mute = true;
        }
    }

    private void OnDisable() {
        drawTool.StopDrawing();
    }
}
