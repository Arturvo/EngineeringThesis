using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraManagerScript : MonoBehaviour
{
    private UnityTemplateProjects.SimpleCameraController terrainCameraController;
    private SecondaryCamera userInputCameraController;
    private MeshGeneratorScript meshGeneratorScript;

    private void Awake()
    {
        terrainCameraController = transform.Find("TerrainCamera").GetComponent<UnityTemplateProjects.SimpleCameraController>();
        userInputCameraController = transform.Find("UserInputCamera").GetComponent<SecondaryCamera>();
        meshGeneratorScript = FindObjectOfType<MeshGeneratorScript>();
    }

    public void SetCamerasStartingPositions()
    {
        // move terrain camera above generated terrain and user input terrain above userInputPlane
        Vector3 startPosition = new Vector3(meshGeneratorScript.GetTerrainWidth() / 2, meshGeneratorScript.GetTerrainHeightLimit() + 500, meshGeneratorScript.GetTerrainDepth() / 2);
        UpdateCamerasPositions(startPosition);
    }

    public void UpdateCamerasPositions(Vector3 newPos)
    {
        SetTerrainCameraPosition(newPos);
        SetUserInputCameraPosition(newPos);
    }

    private void SetTerrainCameraPosition(Vector3 pos)
    {
        terrainCameraController.SetCameraPosition(pos);
    }

    private void SetUserInputCameraPosition(Vector3 pos)
    {
        //userInputCameraController.SetCameraPosition(new Vector3(pos.x - meshGeneratorScript.GetUserInputCameraOffset(), pos.y, pos.z));
    }
}
