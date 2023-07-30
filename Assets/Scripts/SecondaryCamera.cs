using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SecondaryCamera : MonoBehaviour
{
    [SerializeField] private Camera cameraToFollow;
    private UserInputPlaneScript inputPlane;

    // Start is called before the first frame update
    void Start()
    {
        inputPlane = FindObjectOfType<UserInputPlaneScript>();
    }

    // Update is called once per frame
    void Update()
    {
        transform.rotation = cameraToFollow.transform.rotation;
        transform.position = new Vector3(cameraToFollow.transform.position.x + inputPlane.transform.position.x,
             cameraToFollow.transform.position.y,
             cameraToFollow.transform.position.z);
    }
}
