using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UserInputPlaneScript : MonoBehaviour
{
    private MeshGeneratorScript meshGenerator;
    private float[,] values; //user input values, floats between 0-1
    [SerializeField] private Gradient gradient;
    private Color[] colors;
    private Vector3[] vertices;
    private int[] triangles;
    private Mesh inputPlane;

    // store tmp variables to check if they were change on reinit (DO NOT USE THEM)
    int width = -1;
    int depth = -1;
    float sizeUnit = -1f;

    public float[,] GetValues()
    {
        return values;
    }

    private void Start() {
    }

    public void Init()
    {
        if (width == -1 || meshGenerator.GetWidth() != width || meshGenerator.GetDepth() != depth || meshGenerator.GetSizeUnit() != sizeUnit)
        {
            width = meshGenerator.GetWidth();
            depth = meshGenerator.GetDepth();
            sizeUnit = meshGenerator.GetSizeUnit();

            values = new float[meshGenerator.GetWidth(), meshGenerator.GetDepth()];
            for (int x = 0; x < meshGenerator.GetWidth(); x++)
            {
                for (int z = 0; z < meshGenerator.GetDepth(); z++)
                {
                    values[x, z] = 0f;
                }
            }

            transform.position = new Vector3(-meshGenerator.GetUserInputCameraOffset(), 0, 0);
            inputPlane = new Mesh();
            inputPlane.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            gameObject.GetComponent<MeshFilter>().mesh = inputPlane;
            vertices = new Vector3[meshGenerator.GetWidth() * meshGenerator.GetDepth()];
            for (int x = 0; x < meshGenerator.GetWidth(); x++)
            {
                for (int z = 0; z < meshGenerator.GetDepth(); z++)
                {
                    vertices[x * meshGenerator.GetDepth() + z] = new Vector3(x * meshGenerator.GetSizeUnit(), 0, z * meshGenerator.GetSizeUnit());
                }
            }
            triangles = new int[(meshGenerator.GetWidth() - 1) * (meshGenerator.GetDepth() - 1) * 2 * 3];
            int trainglesCouter = 0;
            for (int x = 0; x < meshGenerator.GetWidth() - 1; x++)
            {
                for (int z = 0; z < meshGenerator.GetDepth() - 1; z++)
                {
                    triangles[trainglesCouter] = x * meshGenerator.GetDepth() + z;
                    triangles[trainglesCouter + 1] = x * meshGenerator.GetDepth() + z + 1;
                    triangles[trainglesCouter + 2] = x * meshGenerator.GetDepth() + z + meshGenerator.GetDepth() + 1;
                    triangles[trainglesCouter + 3] = x * meshGenerator.GetDepth() + z;
                    triangles[trainglesCouter + 4] = x * meshGenerator.GetDepth() + z + meshGenerator.GetDepth() + 1;
                    triangles[trainglesCouter + 5] = x * meshGenerator.GetDepth() + z + meshGenerator.GetDepth();
                    trainglesCouter += 6;
                }
            }
            inputPlane.Clear();
            inputPlane.vertices = vertices;
            inputPlane.triangles = triangles;
            inputPlane.RecalculateNormals();

            CalculateColors();
            gameObject.GetComponent<MeshCollider>().sharedMesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
        }
    }

    private void CalculateColors()
    {
        colors = new Color[(meshGenerator.GetWidth()) * (meshGenerator.GetDepth())];
        int currentIndex = 0;
        for (int x = 0; x < meshGenerator.GetWidth(); x++)
        {
            for (int z = 0; z < meshGenerator.GetDepth(); z++)
            {
                float height = values[x, z];
                colors[currentIndex] = gradient.Evaluate(height);
                currentIndex++;
            }
        }
        inputPlane.colors = colors;
    }

    private void Awake()
    {
        meshGenerator = FindObjectOfType<MeshGeneratorScript>();
    }
    
    public void colorMesh(float[,] newvalues){
        values = newvalues;
        inputPlane.Clear();
        inputPlane.vertices = vertices;
        inputPlane.triangles = triangles;
        CalculateColors();
        inputPlane.RecalculateNormals();
    }
}
