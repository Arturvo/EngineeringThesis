using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;

public class MeshRendererScript : MonoBehaviour
{
    // enable debug
    public bool debugSpheres = false;
    public bool debugConsole = false;
    public bool interpolate = true;

    // object prefabs
    public GameObject sphere;
    public GameObject chunk;

    // status variables used to monitor multithreaded rendering status
    private bool[,] chunksPrepared;
    private bool[,] chunksRendered;
    private bool isRendering = false;

    // internal variables
    private bool initDone = false;
    private int[,,] values;
    private int surfaceValue;
    private float sizeUnit;
    private int chunkSize;
    private int ignoreBelow;
    private int ignoreAbove;
    private Vector3 startingPoint;
    private GameObject[,] chunks;
    private Mesh[,] chunkMeshes;
    private List<Vector3>[,] chunkVertices;
    private List<int>[,] chunkTriangles;
    private List<Vector2>[,] chunkUvs;
    private int widthChunks;
    private int depthChunks;

    private MeshGeneratorScript meshGeneratorScript;

    private void Awake()
    {
        meshGeneratorScript = FindObjectOfType<MeshGeneratorScript>();
    }

    private void Init()
    {
        // if chunks were already initiated destroy them
        if (chunks != null)
        {
            for (int x = 0; x < widthChunks; x++)
            {
                for (int z = 0; z < depthChunks; z++)
                {
                    Destroy(chunks[x, z]);
                }
            }
        }

        // get number of chunks required to display terrain
        int width = values.GetLength(0);
        int depth = values.GetLength(2); ;
        // avoid float precision errors by making an edge case when values size % chunkSize is 0
        if (width % chunkSize == 0) widthChunks = width / chunkSize;
        else widthChunks = (int)Mathf.Ceil((float)width / chunkSize);
        if (depth % chunkSize == 0) depthChunks = depth / chunkSize;
        else depthChunks = (int)Mathf.Ceil((float)depth / chunkSize);

        //initiate new chunks arrays
        chunks = new GameObject[widthChunks, depthChunks];
        chunkMeshes = new Mesh[widthChunks, depthChunks];
        chunkVertices = new List<Vector3>[widthChunks, depthChunks];
        chunkTriangles = new List<int>[widthChunks, depthChunks];
        chunkUvs = new List<Vector2>[widthChunks, depthChunks];
        chunksPrepared = new bool[widthChunks, depthChunks];
        chunksRendered = new bool[widthChunks, depthChunks];

        // create an object for every chunk and assign new mesh
        for (int x = 0; x < widthChunks; x++)
        {
            for (int z = 0; z < depthChunks; z++)
            {
                chunksPrepared[x, z] = false;
                chunksRendered[x, z] = false;
                GameObject newChunk = Instantiate(chunk, transform);
                chunks[x, z] = newChunk;
                Mesh newMesh = new Mesh();
                newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                newChunk.GetComponent<MeshFilter>().mesh = newMesh;
                chunkMeshes[x, z] = newMesh;
            }
        }

        initDone = true;
    }

    public void Render(int[,,] values = null, List<int[]> chunksToUpdate = null, int surfaceValue = 0, float sizeUnit = 1f, int chunkSize = 50, int ignoreBelow = -1, int ignoreAbove = -1, Vector3 startingPoint = new Vector3())
    {
        // render can't be executed while last one didn't finish
        if (isRendering) return;

        // if chunks size changed new chunks need to be created
        if (chunkSize != this.chunkSize || (values != null && (values.GetLength(0) != this.values.GetLength(0) || values.GetLength(2) != this.values.GetLength(2)))) initDone = false;

        // if values were not given assume they already existed before
        // this can be used to rerender terrain with different settings but same values
        if (values != null) this.values = values;

        // set all parameters
        this.surfaceValue = surfaceValue;
        this.sizeUnit = sizeUnit;
        this.chunkSize = chunkSize;
        this.ignoreBelow = ignoreBelow;
        this.ignoreAbove = ignoreAbove;
        this.startingPoint = startingPoint;

        if (!initDone) Init();

        // add chunk rendering tasks to threadpool
        // if specific chunks to update were provided, only render those
        if (chunksToUpdate != null)
        {
            // set all chunks as rendered and then only the ones to be rendered as not rendered
            for (int x = 0; x < widthChunks; x++)
            {
                for (int z = 0; z < depthChunks; z++)
                {
                    chunksRendered[x, z] = true;
                }
            }

            foreach (int[] task in chunksToUpdate)
            {
                chunksRendered[task[0], task[1]] = false;

                // Thread pool seems to be global, it is not initiated anywhere
                // Need to have "using System.Threading" i "using System"
                // delegate allows to pass anonymus fuction into the thread
                // state is the argument for that fucntion
                ThreadPool.QueueUserWorkItem(delegate (object state)
                {
                    object[] array = state as object[];
                    RenderChunk(Convert.ToInt32(array[0]), Convert.ToInt32(array[1]));
                }
                // define what "object state" was
                , new object[] { task[0], task[1] });
            }
        }
        // if no chunksToUpdate were provided, render all chunks
        else
        {
            for (int x = 0; x < widthChunks; x++)
            {
                for (int z = 0; z < depthChunks; z++)
                {
                    ThreadPool.QueueUserWorkItem(delegate (object state)
                    {
                        object[] array = state as object[];
                        RenderChunk(Convert.ToInt32(array[0]),Convert.ToInt32(array[1]));
                    }
                    , new object[] { x, z });
                }
            }
        }

        // start looking for chunk updates
        isRendering = true;

        if (debugSpheres) CreateDebugSpheres();
    }

    private void Update()
    {
        // all unity functions must be called in the main thread, so chunk meshes can't be updated inside of the threads.
        // Instead their data is prepared in the threads and then chunk is marked as prepared to be detected here
        if (isRendering)
        {
            bool stillRendering = false;
            for (int x = 0; x < widthChunks; x++)
            {
                for (int z = 0; z < depthChunks; z++)
                {
                    // if chunk is prepared but not rendered, render the chunk
                    if (chunksPrepared[x, z] && !chunksRendered[x, z])
                    {
                        UpdateChunkMesh(x, z);
                        chunksRendered[x, z] = true;
                    }
                    // if any chunk is not rendered continue searching
                    if (!chunksRendered[x, z]) stillRendering = true;
                }
            }
            // when render is finished clear status arrays
            if (!stillRendering)
            {
                isRendering = false;
                for (int x = 0; x < widthChunks; x++)
                {
                    for (int z = 0; z < depthChunks; z++)
                    {
                        chunksPrepared[x, z] = false;
                        chunksRendered[x, z] = false;
                    }
                }
            }
        }
    }

    // update mesh of one chunk. Called in update method after mesh data is prepared
    private void UpdateChunkMesh(int chunkX, int chunkZ)
    {
        Mesh mesh = chunkMeshes[chunkX, chunkZ];
        mesh.Clear();
        mesh.vertices = chunkVertices[chunkX, chunkZ].ToArray();
        mesh.triangles = chunkTriangles[chunkX, chunkZ].ToArray();
        mesh.uv = chunkUvs[chunkX, chunkZ].ToArray();
        mesh.RecalculateNormals();

        // update mesh collider to work with other scripts
        chunks[chunkX, chunkZ].GetComponent<MeshCollider>().sharedMesh = chunks[chunkX, chunkZ].GetComponent<MeshFilter>().sharedMesh;
    }

    private void RenderChunk(int chunkX, int chunkZ)
    {
        GameObject chunk = chunks[chunkX, chunkZ];
        Mesh mesh = chunkMeshes[chunkX, chunkZ];
        List<Vector3> verticesList = new List<Vector3>();
        List<int> trianglesList = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        int textureSizeX = meshGeneratorScript.GetTextureSize();
        int textureSizeY = 4 * textureSizeX;
        int cubePixelSize = meshGeneratorScript.GetCubePixelSize();
        float snowStart = meshGeneratorScript.GetSnowStart();
        float rockStart = meshGeneratorScript.GetRockStart();
        float dirtStart = meshGeneratorScript.GetDirtStart();
        float digDepthLimit = meshGeneratorScript.GetDigDepthLimit();
        float terrainHeightLimit = meshGeneratorScript.GetTerrainHeightLimit();
        float[,] textureLayerBorderMap = meshGeneratorScript.GetTextureLayerBorderMap();
        int seed = meshGeneratorScript.GetSeedInt();

        // pre-allocate support arrays to avoid reallocating memory for each cube
        int[] currentCubeXvalues = new int[8];
        int[] currentCubeYvalues = new int[8];
        int[] currentCubeZvalues = new int[8];

        // pre-allocate variables
        float t, xDiff, yDiff, zDiff, xPos1, yPos1, zPos1, xPos2, yPos2, zPos2, avarageHeight, avarageX, avarageZ, textureHeight, startPositionRandomizer, edge1Length, edge2Length, edge3Length, longestEdgeLength, minPixelsFromEdges, startPointX, startPointY, cabAngle, addToX, addToY, abcAngle;
        int a0, b0, a1, b1, a2, b2, currValue1, currValue2, yStart, yEnd, xStart, xEnd, zStart, zEnd, avarageXInt, avarageZInt, textureToUse, verticesListLenght;

        // calculate y range (if min and max are provided with ignoreBelow or ignoreAbove arguments)
        if (ignoreBelow > 0) yStart = ignoreBelow - 1;
        else yStart = 0;
        if (ignoreAbove > 0) yEnd = ignoreAbove;
        else yEnd = values.GetLength(1);

        // determine x and z bondaries for values array that apply to this chunk
        xStart = chunkSize * chunkX;
        zStart = chunkSize * chunkZ;
        // make sure that xEnd and zEnd don't go over array size when edge chunks are not full size
        xEnd = Mathf.Clamp(chunkSize * (chunkX + 1), xStart, values.GetLength(0) - 1);
        zEnd = Mathf.Clamp(chunkSize * (chunkZ + 1), zStart, values.GetLength(2) - 1);

        // avoid holes in bottom and top of the terrain by modifying values on the top and botoom edge
        for (int x = xStart; x < xEnd; x++)
        {
            for (int z = zStart; z < zEnd; z++)
            {
                if (values[x, 0, z] < surfaceValue) values[x, 0, z] = surfaceValue;
                if (values[x, values.GetLength(1) - 1, z] > surfaceValue) values[x, values.GetLength(1) - 1, z] = surfaceValue - 1;
            }
        }

        // for each point in the 3-dimentional array that is not on the edge (index != array.length - 1):
        for (int y = yStart; y < yEnd - 1; y++)
        {
            for (int x = xStart; x < xEnd; x++)
            {
                for (int z = zStart; z < zEnd; z++)
                {
                    // we will get a cube with one corner x,y,z and oposite one x+1,y+1,z+1 (hence the "index != array.length - 1" requirenment)

                    // prepare support arrays (could be done in less code but would require memory allocation)
                    // the order needs to match the reference http://paulbourke.net/geometry/polygonise/
                    currentCubeXvalues[0] = x;
                    currentCubeXvalues[1] = x + 1;
                    currentCubeXvalues[2] = x + 1;
                    currentCubeXvalues[3] = x;
                    currentCubeXvalues[4] = x;
                    currentCubeXvalues[5] = x + 1;
                    currentCubeXvalues[6] = x + 1;
                    currentCubeXvalues[7] = x;

                    currentCubeYvalues[0] = y;
                    currentCubeYvalues[1] = y;
                    currentCubeYvalues[2] = y;
                    currentCubeYvalues[3] = y;
                    currentCubeYvalues[4] = y + 1;
                    currentCubeYvalues[5] = y + 1;
                    currentCubeYvalues[6] = y + 1;
                    currentCubeYvalues[7] = y + 1;

                    currentCubeZvalues[0] = z + 1;
                    currentCubeZvalues[1] = z + 1;
                    currentCubeZvalues[2] = z;
                    currentCubeZvalues[3] = z;
                    currentCubeZvalues[4] = z + 1;
                    currentCubeZvalues[5] = z + 1;
                    currentCubeZvalues[6] = z;
                    currentCubeZvalues[7] = z;

                    // calculate triangulation index, ref. http://paulbourke.net/geometry/polygonise/
                    int triangulationIndex = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        if (values[currentCubeXvalues[i], currentCubeYvalues[i], currentCubeZvalues[i]] < surfaceValue)
                            triangulationIndex |= Mathf.RoundToInt(Mathf.Pow(2, i));
                    }

                    // debugging information
                    if (debugConsole)
                    {
                        Debug.Log("Finished preparing cube: " + x + "," + y + "," + z);
                        Debug.Log("Support arrays:");
                        string currentCubeXvaluesString = "currentCubeXvalues: ";
                        string currentCubeYvaluesString = "currentCubeYvalues: ";
                        string currentCubeZvaluesString = "currentCubeZvalues: ";
                        for (int v = 0; v < currentCubeXvalues.Length; v++)
                        {
                            currentCubeXvaluesString += currentCubeXvalues[v];
                            if (v < currentCubeXvalues.Length - 1) currentCubeXvaluesString += ", ";
                        }
                        for (int v = 0; v < currentCubeYvalues.Length; v++)
                        {
                            currentCubeYvaluesString += currentCubeYvalues[v];
                            if (v < currentCubeXvalues.Length - 1) currentCubeYvaluesString += ", ";
                        }
                        for (int v = 0; v < currentCubeZvalues.Length; v++)
                        {
                            currentCubeZvaluesString += currentCubeZvalues[v];
                            if (v < currentCubeXvalues.Length - 1) currentCubeZvaluesString += ", ";
                        }
                        Debug.Log(currentCubeXvaluesString);
                        Debug.Log(currentCubeYvaluesString);
                        Debug.Log(currentCubeZvaluesString);
                        string cornerValues = "Cube corner values: ";
                        for (int v = 0; v < 8; v++)
                        {
                            cornerValues += values[currentCubeXvalues[v], currentCubeYvalues[v], currentCubeZvalues[v]];
                            if (v < 7) cornerValues += ", ";
                        }
                        Debug.Log(cornerValues);
                        Debug.Log("Triangulation index: " + triangulationIndex);
                        string triangulationArray = "Triangulation array: ";
                        for (int v = 0; v < triangulation[triangulationIndex].Length; v++)
                        {
                            triangulationArray += triangulation[triangulationIndex][v];
                            if (v < triangulation[triangulationIndex].Length - 1) triangulationArray += ", ";
                        }
                        Debug.Log(triangulationArray);
                    }

                    // Get actual interpolated edge vertices and create triangles, ref. https://github.com/SebLague/Marching-Cubes
                    for (int i = 0; triangulation[triangulationIndex][i] != -1; i += 3)
                    {
                        // Get indices of corner points A and B for each of the three edges
                        // of the cube that need to be joined to form the triangle.
                        a0 = cornerIndexAFromEdge[triangulation[triangulationIndex][i]];
                        b0 = cornerIndexBFromEdge[triangulation[triangulationIndex][i]];

                        a1 = cornerIndexAFromEdge[triangulation[triangulationIndex][i + 1]];
                        b1 = cornerIndexBFromEdge[triangulation[triangulationIndex][i + 1]];

                        a2 = cornerIndexAFromEdge[triangulation[triangulationIndex][i + 2]];
                        b2 = cornerIndexBFromEdge[triangulation[triangulationIndex][i + 2]];

                        // for each pair of points calculate actual interpolated vertices to make a face
                        currValue1 = values[currentCubeXvalues[a0], currentCubeYvalues[a0], currentCubeZvalues[a0]];
                        currValue2 = values[currentCubeXvalues[b0], currentCubeYvalues[b0], currentCubeZvalues[b0]];
                        xPos1 = currentCubeXvalues[a0] * sizeUnit + startingPoint.x;
                        yPos1 = currentCubeYvalues[a0] * sizeUnit + startingPoint.y;
                        zPos1 = currentCubeZvalues[a0] * sizeUnit + startingPoint.z;
                        xPos2 = currentCubeXvalues[b0] * sizeUnit + startingPoint.x;
                        yPos2 = currentCubeYvalues[b0] * sizeUnit + startingPoint.y;
                        zPos2 = currentCubeZvalues[b0] * sizeUnit + startingPoint.z;
                        t = (float)(surfaceValue - currValue1) / (currValue2 - currValue1);
                        if (!interpolate) t = 1f / 2f;
                        xDiff = (xPos2 - xPos1) * t;
                        yDiff = (yPos2 - yPos1) * t;
                        zDiff = (zPos2 - zPos1) * t;
                        Vector3 vertexA = new Vector3(xPos1 + xDiff, yPos1 + yDiff, zPos1 + zDiff);

                        currValue1 = values[currentCubeXvalues[a1], currentCubeYvalues[a1], currentCubeZvalues[a1]];
                        currValue2 = values[currentCubeXvalues[b1], currentCubeYvalues[b1], currentCubeZvalues[b1]];
                        xPos1 = currentCubeXvalues[a1] * sizeUnit + startingPoint.x;
                        yPos1 = currentCubeYvalues[a1] * sizeUnit + startingPoint.y;
                        zPos1 = currentCubeZvalues[a1] * sizeUnit + startingPoint.z;
                        xPos2 = currentCubeXvalues[b1] * sizeUnit + startingPoint.x;
                        yPos2 = currentCubeYvalues[b1] * sizeUnit + startingPoint.y;
                        zPos2 = currentCubeZvalues[b1] * sizeUnit + startingPoint.z;
                        t = (float)(surfaceValue - currValue1) / (currValue2 - currValue1);
                        if (!interpolate) t = 1f / 2f;
                        xDiff = (xPos2 - xPos1) * t;
                        yDiff = (yPos2 - yPos1) * t;
                        zDiff = (zPos2 - zPos1) * t;
                        Vector3 vertexB = new Vector3(xPos1 + xDiff, yPos1 + yDiff, zPos1 + zDiff);

                        currValue1 = values[currentCubeXvalues[a2], currentCubeYvalues[a2], currentCubeZvalues[a2]];
                        currValue2 = values[currentCubeXvalues[b2], currentCubeYvalues[b2], currentCubeZvalues[b2]];
                        xPos1 = currentCubeXvalues[a2] * sizeUnit + startingPoint.x;
                        yPos1 = currentCubeYvalues[a2] * sizeUnit + startingPoint.y;
                        zPos1 = currentCubeZvalues[a2] * sizeUnit + startingPoint.z;
                        xPos2 = currentCubeXvalues[b2] * sizeUnit + startingPoint.x;
                        yPos2 = currentCubeYvalues[b2] * sizeUnit + startingPoint.y;
                        zPos2 = currentCubeZvalues[b2] * sizeUnit + startingPoint.z;
                        t = (float)(surfaceValue - currValue1) / (currValue2 - currValue1);
                        if (!interpolate) t = 1f / 2f;
                        xDiff = (xPos2 - xPos1) * t;
                        yDiff = (yPos2 - yPos1) * t;
                        zDiff = (zPos2 - zPos1) * t;
                        Vector3 vertexC = new Vector3(xPos1 + xDiff, yPos1 + yDiff, zPos1 + zDiff);

                        // add vertices and triangles to the list
                        verticesListLenght = verticesList.Count;
                        verticesList.Add(vertexA);
                        verticesList.Add(vertexB);
                        verticesList.Add(vertexC);
                        trianglesList.Add(verticesListLenght);
                        trianglesList.Add(verticesListLenght + 1);
                        trianglesList.Add(verticesListLenght + 2);

                        // calculate average parameters of vertices to assign texture
                        avarageHeight = (vertexA.y + vertexB.y + vertexC.y) / 3;
                        avarageX = (vertexA.x + vertexB.x + vertexC.x) / 3;
                        avarageZ = (vertexA.z + vertexB.z + vertexC.z) / 3;
                        avarageXInt = Mathf.RoundToInt(avarageX / sizeUnit);
                        avarageZInt = Mathf.RoundToInt(avarageZ / sizeUnit);
                        textureHeight = avarageHeight + textureLayerBorderMap[avarageXInt, avarageZInt];

                        // calculate which texture to use based on calculated height
                        textureToUse = 0;
                        if (textureHeight > terrainHeightLimit * dirtStart) textureToUse = 1;
                        if (textureHeight > terrainHeightLimit * rockStart) textureToUse = 2;
                        if (textureHeight > terrainHeightLimit * snowStart) textureToUse = 3;

                        // create uvs for vertices
                        var random = new System.Random(seed + Mathf.RoundToInt(avarageHeight + avarageX + avarageZ));
                        startPositionRandomizer = (float) random.NextDouble();
                        edge1Length = Vector3.Distance(vertexA, vertexB);
                        edge2Length = Vector3.Distance(vertexA, vertexC);
                        edge3Length = Vector3.Distance(vertexB, vertexC);
                        longestEdgeLength = Mathf.Max(edge1Length, edge2Length, edge3Length);
                        minPixelsFromEdges = (longestEdgeLength / sizeUnit) * cubePixelSize + 1;
                        startPointX = startPositionRandomizer * (1 - minPixelsFromEdges / textureSizeX);
                        startPointY = 0.25f * textureToUse + startPositionRandomizer * (0.25f - minPixelsFromEdges / textureSizeY);

                        if (edge1Length >= edge2Length && edge1Length >= edge3Length)
                        {
                            uvs.Add(new Vector2(startPointX, startPointY));
                            uvs.Add(new Vector2(startPointX + (edge1Length/sizeUnit)*((float) cubePixelSize/ textureSizeX), startPointY));
                            cabAngle = Mathf.Abs(Vector3.Angle(vertexB - vertexA, vertexC - vertexA));
                            addToX = Mathf.Cos(cabAngle * Mathf.Deg2Rad) * edge2Length;
                            addToY = Mathf.Sin(cabAngle * Mathf.Deg2Rad) * edge2Length;
                            uvs.Add(new Vector2(startPointX + (addToX / sizeUnit) * ((float)cubePixelSize / textureSizeX), startPointY + (addToY / sizeUnit) * ((float)cubePixelSize / textureSizeY)));
                        }
                        else if (edge2Length >= edge1Length && edge2Length >= edge3Length)
                        {
                            uvs.Add(new Vector2(startPointX, startPointY));
                            cabAngle = Mathf.Abs(Vector3.Angle(vertexB - vertexA, vertexC - vertexA));
                            addToX = Mathf.Cos(cabAngle * Mathf.Deg2Rad) * edge1Length;
                            addToY = Mathf.Sin(cabAngle * Mathf.Deg2Rad) * edge1Length;
                            uvs.Add(new Vector2(startPointX + (addToX / sizeUnit) * ((float)cubePixelSize / textureSizeX), startPointY + (addToY / sizeUnit) * ((float)cubePixelSize / textureSizeY)));
                            uvs.Add(new Vector2(startPointX + (edge2Length / sizeUnit) * ((float)cubePixelSize / textureSizeX), startPointY));
                        }
                        else if (edge3Length >= edge1Length && edge3Length >= edge2Length)
                        {
                            abcAngle = Mathf.Abs(Vector3.Angle(vertexA - vertexB, vertexC - vertexB));
                            addToX = Mathf.Cos(abcAngle * Mathf.Deg2Rad) * edge1Length;
                            addToY = Mathf.Sin(abcAngle * Mathf.Deg2Rad) * edge1Length;
                            uvs.Add(new Vector2(startPointX + (addToX / sizeUnit) * ((float)cubePixelSize / textureSizeX), startPointY + (addToY / sizeUnit) * ((float)cubePixelSize / textureSizeY)));
                            uvs.Add(new Vector2(startPointX, startPointY));
                            uvs.Add(new Vector2(startPointX + (edge3Length / sizeUnit) * ((float)cubePixelSize / textureSizeX), startPointY));
                        }

                        // degbugging information
                        if (debugConsole)
                        {
                            Debug.Log("Cube: " + x + "," + y + "," + z);
                            Debug.Log("Creating a new triangle. Edges: " + a0 + "," + b0 + " " + a1 + "," + b1 + " " + a2 + "," + b2);
                            Debug.Log("Points calulated:");
                            Debug.Log(vertexA);
                            Debug.Log(vertexB);
                            Debug.Log(vertexC);
                        }
                    }
                }
            }
        }

        // save mesh data and mark chunk as preapred to be updated in update method
        chunkVertices[chunkX, chunkZ] = verticesList;
        chunkTriangles[chunkX, chunkZ] = trianglesList;
        chunkUvs[chunkX, chunkZ] = uvs;
        chunksPrepared[chunkX, chunkZ] = true;
    }

    private void CreateDebugSpheres (){
        Transform sphereContainer = transform.Find("SphereContainer");

        // clean previous spheres if there are any
        for (int i = 0; i < sphereContainer.childCount; i++) Destroy(sphereContainer.GetChild(i).gameObject);

        // create a sphere for debugging in each point if "debugSpheres" is set to true
        // white sphere represents a point above the surface and a black sphere represents a point below the surface
        int maxValue = surfaceValue;
        int minValue = surfaceValue;

        //find min and max value to properly set sphere color later on
        for (int x = 0; x < values.GetLength(0); x++)
        {
            for (int y = 0; y < values.GetLength(1); y++)
            {
                for (int z = 0; z < values.GetLength(2); z++)
                {
                    int value = values[x, y, z];
                    if (value > maxValue) maxValue = value;
                    if (value < minValue) minValue = value;
                }
            }
        }

        int minMaxDifference = maxValue - minValue;

        // create a sphere in each point of the grid
        for (int x = 0; x < values.GetLength(0); x++)
        {
            for (int y = 0; y < values.GetLength(1); y++)
            {
                for (int z = 0; z < values.GetLength(2); z++)
                {
                    GameObject generatedSphere = Instantiate(sphere, new Vector3(x * sizeUnit + startingPoint.x, y * sizeUnit + startingPoint.y, z * sizeUnit + startingPoint.z), Quaternion.identity, sphereContainer);
                    // select an accurate shade of grey for the sphere
                    int value = values[x, y, z];
                    int minValDifference = value - minValue;
                    Renderer renderer = generatedSphere.GetComponent<Renderer>();
                    renderer.material.SetColor("_Color", Color.Lerp(Color.white, Color.black, (float)minValDifference / minMaxDifference));
                }
            }
        }
    }

    // Support arrays
    private readonly int[] cornerIndexAFromEdge = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3 };
    private readonly int[] cornerIndexBFromEdge = { 1, 2, 3, 0, 5, 6, 7, 4, 4, 5, 6, 7 };

    // Values from http://paulbourke.net/geometry/polygonise/
    private readonly int[] edges ={
        0x0  , 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c,
        0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
        0x190, 0x99 , 0x393, 0x29a, 0x596, 0x49f, 0x795, 0x69c,
        0x99c, 0x895, 0xb9f, 0xa96, 0xd9a, 0xc93, 0xf99, 0xe90,
        0x230, 0x339, 0x33 , 0x13a, 0x636, 0x73f, 0x435, 0x53c,
        0xa3c, 0xb35, 0x83f, 0x936, 0xe3a, 0xf33, 0xc39, 0xd30,
        0x3a0, 0x2a9, 0x1a3, 0xaa , 0x7a6, 0x6af, 0x5a5, 0x4ac,
        0xbac, 0xaa5, 0x9af, 0x8a6, 0xfaa, 0xea3, 0xda9, 0xca0,
        0x460, 0x569, 0x663, 0x76a, 0x66 , 0x16f, 0x265, 0x36c,
        0xc6c, 0xd65, 0xe6f, 0xf66, 0x86a, 0x963, 0xa69, 0xb60,
        0x5f0, 0x4f9, 0x7f3, 0x6fa, 0x1f6, 0xff , 0x3f5, 0x2fc,
        0xdfc, 0xcf5, 0xfff, 0xef6, 0x9fa, 0x8f3, 0xbf9, 0xaf0,
        0x650, 0x759, 0x453, 0x55a, 0x256, 0x35f, 0x55 , 0x15c,
        0xe5c, 0xf55, 0xc5f, 0xd56, 0xa5a, 0xb53, 0x859, 0x950,
        0x7c0, 0x6c9, 0x5c3, 0x4ca, 0x3c6, 0x2cf, 0x1c5, 0xcc ,
        0xfcc, 0xec5, 0xdcf, 0xcc6, 0xbca, 0xac3, 0x9c9, 0x8c0,
        0x8c0, 0x9c9, 0xac3, 0xbca, 0xcc6, 0xdcf, 0xec5, 0xfcc,
        0xcc , 0x1c5, 0x2cf, 0x3c6, 0x4ca, 0x5c3, 0x6c9, 0x7c0,
        0x950, 0x859, 0xb53, 0xa5a, 0xd56, 0xc5f, 0xf55, 0xe5c,
        0x15c, 0x55 , 0x35f, 0x256, 0x55a, 0x453, 0x759, 0x650,
        0xaf0, 0xbf9, 0x8f3, 0x9fa, 0xef6, 0xfff, 0xcf5, 0xdfc,
        0x2fc, 0x3f5, 0xff , 0x1f6, 0x6fa, 0x7f3, 0x4f9, 0x5f0,
        0xb60, 0xa69, 0x963, 0x86a, 0xf66, 0xe6f, 0xd65, 0xc6c,
        0x36c, 0x265, 0x16f, 0x66 , 0x76a, 0x663, 0x569, 0x460,
        0xca0, 0xda9, 0xea3, 0xfaa, 0x8a6, 0x9af, 0xaa5, 0xbac,
        0x4ac, 0x5a5, 0x6af, 0x7a6, 0xaa , 0x1a3, 0x2a9, 0x3a0,
        0xd30, 0xc39, 0xf33, 0xe3a, 0x936, 0x83f, 0xb35, 0xa3c,
        0x53c, 0x435, 0x73f, 0x636, 0x13a, 0x33 , 0x339, 0x230,
        0xe90, 0xf99, 0xc93, 0xd9a, 0xa96, 0xb9f, 0x895, 0x99c,
        0x69c, 0x795, 0x49f, 0x596, 0x29a, 0x393, 0x99 , 0x190,
        0xf00, 0xe09, 0xd03, 0xc0a, 0xb06, 0xa0f, 0x905, 0x80c,
        0x70c, 0x605, 0x50f, 0x406, 0x30a, 0x203, 0x109, 0x0
    };

    // Values from http://paulbourke.net/geometry/polygonise/
    private readonly int[][] triangulation = {
        new int[] {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1 },
        new int[] { 8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1 },
        new int[] { 3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1 },
        new int[] { 4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1 },
        new int[] { 4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1 },
        new int[] { 9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1 },
        new int[] { 10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1 },
        new int[] { 5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1 },
        new int[] { 5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1 },
        new int[] { 8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1 },
        new int[] { 2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1 },
        new int[] { 2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1 },
        new int[] { 11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1 },
        new int[] { 5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1 },
        new int[] { 11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1 },
        new int[] { 11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1 },
        new int[] { 2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1 },
        new int[] { 6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1 },
        new int[] { 3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1 },
        new int[] { 6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1 },
        new int[] { 6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1 },
        new int[] { 8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1 },
        new int[] { 7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1 },
        new int[] { 3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1 },
        new int[] { 0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1 },
        new int[] { 9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1 },
        new int[] { 8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1 },
        new int[] { 5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1 },
        new int[] { 0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1 },
        new int[] { 6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1 },
        new int[] { 10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1 },
        new int[] { 1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1 },
        new int[] { 0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1 },
        new int[] { 3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1 },
        new int[] { 6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1 },
        new int[] { 9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1 },
        new int[] { 8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1 },
        new int[] { 3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1 },
        new int[] { 10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1 },
        new int[] { 10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1 },
        new int[] { 2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1 },
        new int[] { 7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1 },
        new int[] { 2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1 },
        new int[] { 1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1 },
        new int[] { 11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1 },
        new int[] { 8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1 },
        new int[] { 0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1 },
        new int[] { 7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1 },
        new int[] { 7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1 },
        new int[] { 10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1 },
        new int[] { 0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1 },
        new int[] { 7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1 },
        new int[] { 6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1 },
        new int[] { 4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1 },
        new int[] { 10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1 },
        new int[] { 8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1 },
        new int[] { 1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1 },
        new int[] { 10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1 },
        new int[] { 10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1 },
        new int[] { 9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1 },
        new int[] { 7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1 },
        new int[] { 3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1 },
        new int[] { 7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1 },
        new int[] { 3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1 },
        new int[] { 6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1 },
        new int[] { 9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1 },
        new int[] { 1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1 },
        new int[] { 4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1 },
        new int[] { 7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1 },
        new int[] { 6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1 },
        new int[] { 0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1 },
        new int[] { 6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1 },
        new int[] { 0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1 },
        new int[] { 11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1 },
        new int[] { 6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1 },
        new int[] { 5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1 },
        new int[] { 9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1 },
        new int[] { 1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1 },
        new int[] { 10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1 },
        new int[] { 0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1 },
        new int[] { 11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1 },
        new int[] { 9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1 },
        new int[] { 7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1 },
        new int[] { 2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1 },
        new int[] { 9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1 },
        new int[] { 9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1 },
        new int[] { 1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1 },
        new int[] { 0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1 },
        new int[] { 10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1 },
        new int[] { 2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1 },
        new int[] { 0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1 },
        new int[] { 0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1 },
        new int[] { 9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1 },
        new int[] { 5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1 },
        new int[] { 5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1 },
        new int[] { 8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1 },
        new int[] { 9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1 },
        new int[] { 1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1 },
        new int[] { 3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1 },
        new int[] { 4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1 },
        new int[] { 9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1 },
        new int[] { 11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1 },
        new int[] { 2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1 },
        new int[] { 9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1 },
        new int[] { 3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1 },
        new int[] { 1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1 },
        new int[] { 4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1 },
        new int[] { 0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1 },
        new int[] { 1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] { 0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[] {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }
    };
}
