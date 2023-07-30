using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TEST : MonoBehaviour
{
    [SerializeField] private Camera mcamera;
    private MeshGeneratorScript generator;
    private float sizeUnit;
    private float depthOffsetUnits;
    bool asdf = true;

    private void Awake() {
        generator = FindObjectOfType<MeshGeneratorScript>();
    }

    public void Init() {
        sizeUnit = generator.GetSizeUnit();
        depthOffsetUnits = generator.GetDigDepthLimit();
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButton(0) && asdf){
            asdf = false;
            StartCoroutine(asdffff());
            int[,,] currentValues = generator.GetValues();
            RaycastHit hit;
            Ray ray = mcamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, Mathf.Infinity)) {
                Transform objectHit = hit.transform;
                float HitY = hit.point.y;
                float currX = hit.point.x;
                float currZ = hit.point.z;
                int midY = Mathf.RoundToInt(HitY - depthOffsetUnits);
                Debug.Log(currentValues[Mathf.RoundToInt(currX/sizeUnit),
                    Mathf.RoundToInt(midY/sizeUnit) + 3,
                    Mathf.RoundToInt(currZ/sizeUnit)]);
                Debug.Log(currentValues[Mathf.RoundToInt(currX/sizeUnit),
                    Mathf.RoundToInt(midY/sizeUnit) + 2,
                    Mathf.RoundToInt(currZ/sizeUnit)]);
                Debug.Log(currentValues[Mathf.RoundToInt(currX/sizeUnit),
                    Mathf.RoundToInt(midY/sizeUnit) + 1,
                    Mathf.RoundToInt(currZ/sizeUnit)]);
                Debug.Log(currentValues[Mathf.RoundToInt(currX/sizeUnit),
                    Mathf.RoundToInt(midY/sizeUnit),
                    Mathf.RoundToInt(currZ/sizeUnit)]);
                Debug.Log(currentValues[Mathf.RoundToInt(currX/sizeUnit),
                    Mathf.RoundToInt(midY/sizeUnit) - 1,
                    Mathf.RoundToInt(currZ/sizeUnit)]);
                Debug.Log(currentValues[Mathf.RoundToInt(currX/sizeUnit),
                    Mathf.RoundToInt(midY/sizeUnit) - 2,
                    Mathf.RoundToInt(currZ/sizeUnit)]);

            }
        }
    }

    private IEnumerator asdffff(){
        yield return new WaitForSeconds(0.5f);
        asdf = true;
    }
}
