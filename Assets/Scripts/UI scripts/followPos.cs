using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class followPos : MonoBehaviour
{
    [SerializeField] private GameObject toFollow;
    [SerializeField] private Vector3 Offset;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = toFollow.transform.position + Offset;
    }
}
