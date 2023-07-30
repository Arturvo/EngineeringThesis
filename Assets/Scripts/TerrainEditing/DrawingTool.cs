using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DrawingTool : MonoBehaviour
{
    [SerializeField] private Image[] buttons;
    [SerializeField] private Color SelectedColor, UnselectedColor;

    [SerializeField] private float effectDistance = 5f;
    [SerializeField] private float strength = 3f;
    [Range(0f, 1f)]
    [SerializeField] private float AddMax = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float RemoveMin = 0f;
    [Range(0f, 5f)]
    [SerializeField] private float falloff = 1f;

    private UserInputPlaneScript inputPlane;
    private MeshGeneratorScript generator;
    private MeshRenderer mrenderer;
    private float sizeUnit, width, height, depth;
    private bool isDrawing = false;
    private int Cwidth, Cdepth;
    [SerializeField] private float mode = 1;
    private bool dividerLock = false, settingsLock = false;

    public float GetDistance(){
        return effectDistance;
    }
    public void SetDistance(float newDistance){
        effectDistance = newDistance;
    }

    public float GetStrength(){
        return strength;
    }
    public void SetStrength(float newStr){
        strength = newStr;
    }

    public float GetAddMax(){
        return AddMax;
    }
    public void SetAddMax(float newMax){
        AddMax = newMax;
    }

    public float GetRemoveMin(){
        return RemoveMin;
    }
    public void SetRemoveMin(float newMin){
        RemoveMin = newMin;
    }

    public float GetFalloff(){
        return falloff;
    }
    public void SetFalloff(float newfalloff){
        falloff = newfalloff;
    }

    private void Awake() {
        inputPlane = FindObjectOfType<UserInputPlaneScript>();
        mrenderer = GetComponent<MeshRenderer>();
        generator = FindObjectOfType<MeshGeneratorScript>();
    }

    public void Init()
    {
        sizeUnit = generator.GetSizeUnit();
        Cwidth = generator.GetWidth();
        Cdepth = generator.GetDepth();
        width = Cwidth * (int)sizeUnit;
        height = generator.GetHeight() * (int)sizeUnit;
        depth = Cdepth * (int)sizeUnit;
    }

    
    // Start is called before the first frame update
    void Start()
    {
        SetButtonColors(0);
    }

    // Update is called once per frame
    void Update()
    {
        transform.localScale = new Vector3(effectDistance * 1.5f, effectDistance * 1.5f, effectDistance * 1.5f);
    }

    private void SetButtonColors(int buttonIndex){
        for(int i = 0; i < buttons.Length; i++){
            if(i == buttonIndex) buttons[i].color = SelectedColor;
            else buttons[i].color = UnselectedColor;
        }
    }

    public void SetAdding(){
        //toolType = 1;
        mode = 1;
        SetButtonColors(0);
    }

    public void SetRemoving(){
        //toolType = 1;
        mode = -1;
        SetButtonColors(1);
    }

    public void KeepDrawing(){
        mrenderer.enabled = true;
        if(!isDrawing){
            isDrawing = true;
            StartCoroutine(Draw());
        }
    }

    public void StopDrawing(){
        mrenderer.enabled = false;
        isDrawing = false;
        StopAllCoroutines();
    }

    private IEnumerator Draw(){
        //yield return new WaitForSeconds(0.1f);
        int Xindex = Mathf.RoundToInt((transform.position.x - inputPlane.transform.position.x)/sizeUnit);
        int Zindex = Mathf.RoundToInt(transform.position.z/sizeUnit);
        int effectUnits = Mathf.RoundToInt(effectDistance/sizeUnit);
        yield return new WaitForFixedUpdate();
        float[,] values = inputPlane.GetValues();
        for(int x = -effectUnits -1; x < effectUnits +1; x += 1){
            int currX = Xindex + x;
            if(currX >= 0 && currX <= Cwidth-1){
                for(int z = -effectUnits -1; z < effectUnits +1; z += 1){
                    int currZ = Zindex + z;
                    if(currZ >= 0 && currZ <= Cdepth-1){
                        float distance = Vector3.Distance(new Vector3(transform.position.x - inputPlane.transform.position.x,transform.position.y,transform.position.z),
                                                        new Vector3(currX*sizeUnit,transform.position.y,currZ*sizeUnit));
                        if(distance <= effectDistance){
                            if(mode == 1){
                                if(values[currX,currZ] < AddMax){
                                    values[currX,currZ] = 
                                    Mathf.Min(values[currX,currZ] + (mode * strength * 0.02f * (Mathf.Pow(1 - distance/effectDistance, falloff))), AddMax);
                                }
                            }else if(mode == -1){
                                if(values[currX,currZ] > RemoveMin){
                                    values[currX,currZ] = 
                                    Mathf.Max(values[currX,currZ] + (mode * strength * 0.02f * ( Mathf.Pow(1 - distance/effectDistance, falloff))), RemoveMin);
                                }
                            }
                        }
                    }
                }
            }
        }
        inputPlane.colorMesh(values);
        //yield return new WaitForFixedUpdate();
        StartCoroutine(Draw());
    }
}
