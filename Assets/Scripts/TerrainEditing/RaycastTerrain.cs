using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastTerrain : MonoBehaviour
{
    [SerializeField] private TerrainTool terrainTool;
    [SerializeField] private Camera mcamera;
    [SerializeField] private AudioSource audioSource;
    int layerMask = 1 << 8;
    private bool paused = false;

    public void SetPause(bool pause){
        paused = pause;
    }

    private void Awake() {
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
                audioSource.mute = false;
                Transform objectHit = hit.transform;
                terrainTool.transform.position = hit.point;
                terrainTool.KeepAdding();
            }else{
                terrainTool.StopAdding();
                audioSource.mute = true;
            }
        }
        else{
            terrainTool.StopAdding();
            audioSource.mute = true;
        }
    }

    private void OnDisable() {
        terrainTool.StopAdding();
        audioSource.mute = true;
    }
}
