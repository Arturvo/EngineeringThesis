using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGeneratorScript : MonoBehaviour
{
    private MeshRendererScript meshRenderer;
    private EnvironmentGeneratorScript environmentGenerator;
    private TerrainTool terrainTool;
    private DrawingTool drawingTool;
    private UserInputPlaneScript userInputPlaneScript;
    private CameraManagerScript cameraManagerScript;

    // user inputs:
    // terrain size in meters
    [Range(100, 3000)]
    public int terrainWidth = 1000;
    [Range(100, 3000)]
    public int terrainDepth = 1000;
    // cubes per 100 meters
    [Range(10, 100)]
    public int terrainQuality = 20;
    // chunk size in meters
    [Range(10, 500)]
    public int chunkSize = 100;
    // adding removing terrain limits in meters that will determine y size of values array
    [Range(100, 3000)]
    public int bulidHeightLimit = 1000;
    [Range(-1000, 0)]
    public int digDepthLimit = -500;
    // maximum height of terrain in meters
    [Range(100, 3000)]
    public int terrainHeightLimit = 500;
    // how bumpy the rerrain is
    [Range(0.005f, 0.1f)]
    public float terrainDensity = 0.02f;

    public void SetTerrainWidth(float newValue){
        terrainWidth = (int)newValue;
    }
    public void SetTerrainDepth(float newValue){
        terrainDepth = (int)newValue;
    }
    public void SetTerrainQuality(float newValue){
        terrainQuality = (int)newValue;
    }
    public void SetChunkSize(float newValue){
        chunkSize = (int)newValue;
    }
    public void SetBulidHeightLimit(float newValue){
        bulidHeightLimit = (int)newValue;
    }
    public void SetDigDepthLimit(float newValue){
        digDepthLimit = (int)newValue;
    }
    public void SetTerrainHeightLimit(float newValue){
        terrainHeightLimit = (int)newValue;
    }
    public void SetTerrainDensity(float newValue){
        terrainDensity = newValue;
    }

    // parameters generated based on user inputs
    // dimentions of the values array
    private int width;
    private int height;
    private int depth;
    // minimum and maximum indexes that terrain generates in
    private int minTerrainHeight;
    private int maxTerrainHeight;
    // generate function parameters
    // chunk size in cubes
    private int cubesChunkSize;
    // cube size in Unity units
    private float sizeUnit;
    // actual min and max considering multiple layers of noise
    private float basicTerrainMaxHeight;
    private float basicTerrainMinHeight;
    private float bottomTerrainMinHeight;

    // change offstes to get different terrain
    public string seed = "example_seed";
    private int seedInt;

    public string GetSeed(){
        return seed;
    }
    public void SetSeed(string newSeed){
        seed = newSeed;
    }

    // minimum and maximum marching cube indexes for initial generate algorythm
    private int minMarchingValue = -256;
    private int maxMarchingValue = 256;
    private int surfaceValue = 0;

    // holds generated values to be used in other scripts
    private float[,] heightMap;
    private int[,,] values;
    private float[,] rockMap;
    // this map is used to determine non linear borders between different textures
    private float[,] textureLayerBorderMap;

    // what to generate
    public bool generateEnvironment = true;

    public void SetGenerateEnvironment(float newValue){
        if(newValue > 0.5f) generateEnvironment = true;
        else generateEnvironment = false;
    }
    // input plane variables
    private GameObject inputPlane;
    private Mesh inputPlaneMesh;

    // how far input plane and terrain are from each other
    private int userInputCameraOffset;

    // perlin generation rules for layers
    [Range(1, 9)]
    public int perlin2DLayers = 5;
    [Range(0.05f, 0.5f)]
    public float perlin2DHeight = 0.2f;
    [Range(0.5f, 10f)]
    public float perlin2DDensity = 2f;
    [Range(1f, 3f)]
    public float perlin2DDensityLayerBase = 2f;
    [Range(0f, 1f)]
    public float perlin2DLayerRand = 0.4f;
    private float[] perlin2DHeightMultipliers;
    private float[] perlin2DDensityMultipliers;

    public void SetPerlin2DLayers(float newValue){
        perlin2DLayers = (int)newValue;
    }
    public void SetPerlin2DHeight(float newValue){
        perlin2DHeight = newValue;
    }
    public void SetPerlin2DDensity(float newValue){
        perlin2DDensity = newValue;
    }
    public void SetPerlin2DDensityLayerBase(float newValue){
        perlin2DDensityLayerBase = newValue;
    }
    public void SetPerlin2DLayerRand(float newValue){
        perlin2DLayerRand = newValue;
    }

    // texturing parameters in pixels
    private int textureSize = 512;
    [Range(10, 100)]
    public int cubePixelSize = 70;

    // texturing borders in percent of terrainHeightLimit
    [Range(0f,1f)]
    public float snowStart = 0.9f;
    [Range(0f, 1f)]
    public float rockStart = 0.65f;
    [Range(0f, 1f)]
    public float dirtStart = 0.4f;

    // line between textures parameters
    [Range(0f, 1f)]
    public float layerRand = 0.2f;
    [Range(0.1f, 0.3f)]
    public float layerRandDensity = 0.12f;

    // rock map parameters
    [Range(0f, 1f)]
    public float rockTreshold = 0.8f;
    [Range(0.05f, 0.3f)]
    public float rockDensity = 0.1f;

    // 3d perlin noise parameters
    [Range(1, 9)]
    public int perlin3DLayers = 5;
    [Range(0.5f, 1.5f)]
    public float perlin3DPower = 0.8f;
    [Range(0.2f, 1.5f)]
    public float perlin3DDensity = 0.8f;
    [Range(0f, 0.5f)]
    public float perlin3DLayerRand = 0.4f;
    [Range(1f, 3f)]
    public float perlin3DDensityLayerBase = 2f;
    [Range(0.05f, 0.2f)]
    public float bottomNoiseHeight = 0.1f;
    private float[] perlin3DDHeightMultipliers;
    private float[] perlin3DDensityMultipliers;
    private float[] perlin2DHeightMultipliersBottom;

    public void SetCubePixelSize(float newValue){
        cubePixelSize = (int)newValue;
    }
    public void SetSnowStart(float newValue){
        snowStart = newValue;
    }
    public void SetRockStart(float newValue){
        rockStart = newValue;
    }
    public void SetDirtStart(float newValue){
        dirtStart = newValue;
    }
    public void SetLayerRand(float newValue){
        layerRand = newValue;
    }
    public void SetLayerRandDensity(float newValue){
        layerRandDensity = newValue;
    }
    public void SetRockDensity(float newValue){
        rockDensity = newValue;
    }
    public void SetRockTreshold(float newValue){
        rockTreshold = 1 - newValue;
    }
    public void SetPerlin3DLayers(float newValue){
        perlin3DLayers = (int)newValue;
    }
    public void SetPerlin3DDensity(float newValue){
        perlin3DDensity = newValue;
    }
    public void SetPerlin3DPower(float newValue){
        perlin3DPower = newValue;
    }
    public void SetPerlin3DLayerRand(float newValue){
        perlin3DLayerRand = newValue;
    }
    public void SetPerlin3DDensityLayerBase(float newValue){
        perlin3DDensityLayerBase = newValue;
    }
    public void SetBottomNoiseHeight(float newValue){
        bottomNoiseHeight = newValue;
    }

    private void Awake()
    {
        meshRenderer = FindObjectOfType<MeshRendererScript>();
        environmentGenerator = FindObjectOfType<EnvironmentGeneratorScript>();
        terrainTool = FindObjectOfType<TerrainTool>();
        drawingTool = FindObjectOfType<DrawingTool>();
        userInputPlaneScript = FindObjectOfType<UserInputPlaneScript>();
        cameraManagerScript = FindObjectOfType<CameraManagerScript>();
    }

    private void Start()
    {
        CalculateVariables();
        cameraManagerScript.SetCamerasStartingPositions();
        Generate(true);
    }

    public int[,,] GetValues()
    {
        return values;
    }
    public float[,] GetHeightMap()
    {
        return heightMap;
    }
    public float GetSizeUnit()
    {
        return sizeUnit;
    }
    public int GetSurfaceValue()
    {
        return surfaceValue;
    }
    public int GetChunkSize()
    {
        return cubesChunkSize;
    }
    public int GetChunksLength()
    {
        return Mathf.RoundToInt(cubesChunkSize * sizeUnit);
    }
    public int GetWidth()
    {
        return width;
    }
    public int GetHeight()
    {
        return height;
    }
    public int GetDepth()
    {
        return depth;
    }
    public int GetTerrainWidth()
    {
        return terrainWidth;
    }
    public int GetTerrainDepth()
    {
        return terrainDepth;
    }
    public int GetTerrainHeightLimit()
    {
        return terrainHeightLimit;
    }
    public int GetUserInputCameraOffset()
    {
        return userInputCameraOffset;
    }
    public int GetDigDepthLimit()
    {
        return digDepthLimit;
    }
    public int GetBulidHeightLimit()
    {
        return bulidHeightLimit;
    }
    public int GetSeedInt()
    {
        return seedInt;
    }
    public int GetCubePixelSize()
    {
        return cubePixelSize;
    }
    public int GetTextureSize()
    {
        return textureSize;
    }
    public float GetSnowStart()
    {
        return snowStart;
    }
    public float GetRockStart()
    {
        return rockStart;
    }
    public float GetDirtStart()
    {
        return dirtStart;
    }
    public int GetMinTerrainHeight()
    {
        return minTerrainHeight;
    }
    public int GetMaxTerrainHeight()
    {
        return maxTerrainHeight;
    }
    public float[,] GetTextureLayerBorderMap()
    {
        return textureLayerBorderMap;
    }
    public float[,] GetRockMap()
    {
        return rockMap;
    }
    public float GetRockThreshold()
    {
        return rockTreshold;
    }

    private void CreateUserInputScene()
    {
        inputPlane = transform.Find("InputPlane").gameObject;
        inputPlaneMesh = new Mesh();
        inputPlaneMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        inputPlane.GetComponent<MeshFilter>().mesh = inputPlaneMesh;
        Vector3[] vertices = new Vector3[width * depth];
    }

    private void CalculateVariables()
    {
        seedInt = StringSeedToInteger(seed);
        width = (int)Mathf.Floor(terrainWidth * ((float)terrainQuality / 100));
        height = (int)Mathf.Floor((bulidHeightLimit - digDepthLimit) * ((float)terrainQuality / 100));
        depth = (int)Mathf.Floor(terrainDepth * ((float)terrainQuality / 100));

        basicTerrainMinHeight = 0;
        bottomTerrainMinHeight = 0;
        basicTerrainMaxHeight = terrainHeightLimit;

        Random.InitState(seedInt);
        perlin2DHeightMultipliers = new float[perlin2DLayers];
        perlin2DHeightMultipliersBottom = new float[perlin2DLayers];
        perlin2DDensityMultipliers = new float[perlin2DLayers];
        perlin2DHeightMultipliers[0] = perlin2DHeight;
        perlin2DHeightMultipliersBottom[0] = bottomNoiseHeight;
        perlin2DDensityMultipliers[0] = perlin2DDensity;
        for (int i = 1; i < perlin2DLayers; i++)
        {
            perlin2DHeightMultipliers[i] = perlin2DHeightMultipliers[i - 1] / (2 + Random.value * 0.2f - 0.1f);
            perlin2DHeightMultipliersBottom[i] = perlin2DHeightMultipliersBottom[i - 1] / (2 + Random.value * 0.2f - 0.1f);
            perlin2DDensityMultipliers[i] = perlin2DDensityMultipliers[i - 1] * (perlin2DDensityLayerBase + Random.value * 0.2f - 0.1f);
            bottomTerrainMinHeight += 0.5f * perlin2DHeightMultipliersBottom[i] * terrainHeightLimit;
            basicTerrainMinHeight += 0.5f * perlin2DHeightMultipliers[i] * terrainHeightLimit;
            basicTerrainMaxHeight -= 0.5f * perlin2DHeightMultipliers[i] * terrainHeightLimit;
        }
        
        minTerrainHeight = (int)Mathf.Floor(Mathf.Abs(digDepthLimit) * ((float)terrainQuality / 100));
        maxTerrainHeight = (int)Mathf.Floor((Mathf.Abs(digDepthLimit) + terrainHeightLimit) * ((float)terrainQuality / 100));
        sizeUnit = (float)terrainWidth / width;
        cubesChunkSize = (int)Mathf.Floor(chunkSize * ((float)terrainQuality / 100));
        userInputCameraOffset = terrainWidth + 20000;
    }

    public void Generate(bool noRecalculate, bool use3DNoise = false)
    {
        // calculate internal variables based on user input
        if (!noRecalculate) CalculateVariables();

        // initiate terrain editing tool
        userInputPlaneScript.Init();
        terrainTool.Init();
        drawingTool.Init();
        //FindObjectOfType<TEST>().Init(); //DEBUG

        // generate texture layer border map
        textureLayerBorderMap = GenerateTextureLayerBorderMap();
        rockMap = GenerateRockMap();

        // generate values
        if (use3DNoise) values = GetMarchingValuesV2();
        else values = GetMarchingValuesV1();
        // render new mesh
        meshRenderer.Render(values, null, surfaceValue, sizeUnit, cubesChunkSize, minTerrainHeight-1, maxTerrainHeight+1, new Vector3(0,digDepthLimit,0));
        // generate environment

        environmentGenerator.GenerateEnvironment(GetHeightMap(), GetWidth(), GetDepth(), GetSizeUnit(), GetSeedInt(), GetTerrainHeightLimit(), 
            generateEnvironment, GetTextureLayerBorderMap(), GetDirtStart(), GetRockStart(), GetSnowStart(), GetRockMap(), GetRockThreshold());
    }

    public void RegenerateEnvironment()
    {
        // recalculate hight map
        heightMap = new float[width, depth];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                bool foundSomething = false;
                for (int y = bulidHeightLimit - digDepthLimit; y >= 0; y--)
                {
                    if (y > 0 && y < height && values[x, y, z] > surfaceValue)
                    {
                        foundSomething = true;
                        heightMap[x, z] = y * sizeUnit + digDepthLimit + sizeUnit * (Mathf.Abs(values[x, y, z] - values[x, y + 1, z]) / (2 * maxMarchingValue));
                        break;
                    }
                }
                if (!foundSomething) heightMap[x, z] = 0f;
            }
        }

        environmentGenerator.GenerateEnvironment(GetHeightMap(), GetWidth(), GetDepth(), GetSizeUnit(), GetSeedInt(), GetTerrainHeightLimit(),
            generateEnvironment, GetTextureLayerBorderMap(), GetDirtStart(), GetRockStart(), GetSnowStart(), GetRockMap(), GetRockThreshold());
    }

    public void Generate2DNoise(){
        Generate(false,false);
    }

    public void Generate3DNoise()
    {
        Generate(false, true);
    }

    public void Rerender(int[,,] values = null, List<int[]> chunksToUpdate = null, int ignoreBelow = -1, int ignoreAbove = -1)
    {
        // Parameters:
        // values(optional - default null) - 3-dimentional int values representing marching cubes mesh. If not provided old ones will be used
        // chunksToUpdate(optional - default null) - only update selected chunks [x coord, z coord]. If not provided all chunks will be rendered. Here is an example that will generate only chunks 0,0 and 1,1 : new List<int[]>(new int[][] {new int[2]{0,0}, new int[2]{1,1}})
        // surfaceValue(optional - default 0) - a value used by marching cubes algorith as a border between outside and inside of the mesh
        // sizeUnit(optional - default 1f) - a distance between 2 points on the values array. If set to one, the size of created obect will be the same as vlaues array dimentions
        // chunkSize(optional - default 50) - how many cubes are in a chunk (will determine number of chunks)
        // ignoreBelow(optional - default -1) - if set the render function will not render below this y index increasing performance in case you are sure there is nothing there to be rendered
        // ignoreAbove(optional - default -1) - if set the render function will not render above this y index increasing performance in case you are sure there is nothing there to be rendered
        // startingPoint(optional - default 0,0,0) - starting point of a mesh

        if (values != null) this.values = values;

        meshRenderer.Render(values, chunksToUpdate, surfaceValue, sizeUnit, cubesChunkSize, ignoreBelow, ignoreAbove, new Vector3(0, digDepthLimit, 0));
    }

    // genearate marching cube values array based on Perlin noise height map
    int[,,] GetMarchingValuesV1()
    {
        // get two dimentional height map
        heightMap = GetPerlinHeights();
        values = new int[width, height, depth];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    // for each x and z populate y column with positive values below perlin height and negative above
                    float perlinHeight = heightMap[x, z] - digDepthLimit;
                    int marchingValue = 0;
                    // if point is far from edge assign max or min value
                    if ((y + 1)*sizeUnit <= perlinHeight) marchingValue = maxMarchingValue;
                    else if ((y - 1) *sizeUnit >= perlinHeight) marchingValue = minMarchingValue;
                    // if point is on the edge calculate value based on exact height
                    else if (y *sizeUnit <= perlinHeight) marchingValue = (int)Mathf.Round(((perlinHeight - y * sizeUnit)/sizeUnit) * maxMarchingValue);
                    else if (y *sizeUnit >= perlinHeight) marchingValue = (int)Mathf.Round(((y * sizeUnit - perlinHeight)/ sizeUnit) * minMarchingValue);
                    values[x, y, z] = marchingValue;
                }
            }
        }
        return values;
    }

    int[,,] GetMarchingValuesV2()
    {
        Random.InitState(seedInt);
        perlin3DDHeightMultipliers = new float[perlin3DLayers];
        perlin3DDensityMultipliers = new float[perlin3DLayers];
        perlin3DDHeightMultipliers[0] = 1f;
        perlin3DDensityMultipliers[0] = perlin3DDensity;
        for (int i = 1; i < perlin3DLayers; i++)
        {
            perlin3DDHeightMultipliers[i] = perlin3DDHeightMultipliers[i - 1] / (2 + (Random.value - 0.5f)* perlin3DLayerRand);
            perlin3DDensityMultipliers[i] = perlin3DDensityMultipliers[i - 1] * (2 + (Random.value - 0.5f)* perlin3DLayerRand);
        }

        // get 3d perlin noise map
        float[,,] perlinNoise3D = Get3DPerlinHeights();
        float[,] perlinNoise2D = GetPerlinHeights();
        float[,] perlinNoise2DBottom = GetPerlinHeights(true);
        values = new int[width, height, depth];
        int bottomY = Mathf.RoundToInt(-digDepthLimit / sizeUnit);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    values[x, y, z] = Mathf.RoundToInt((Mathf.Pow(perlinNoise3D[x, y, z],perlin3DPower) - 0.5f) * 2 * maxMarchingValue);
                    if (values[x, y, z] > maxMarchingValue) values[x, y, z] = maxMarchingValue;
                    if (values[x, y, z] < minMarchingValue) values[x, y, z] = minMarchingValue;

                    // for each x and z populate y column with positive values below perlin height and negative above
                    float perlinHeight = perlinNoise2D[x, z] - digDepthLimit;
                    if ((y - 1) * sizeUnit >= perlinHeight) values[x, y, z] = minMarchingValue;
                    else if (y * sizeUnit <= perlinHeight && values[x, y, z] > surfaceValue && (y + 1) * sizeUnit > perlinHeight) values[x, y, z] = (int)Mathf.Round(((perlinHeight - y * sizeUnit) / sizeUnit) * maxMarchingValue);
                    else if (y * sizeUnit >= perlinHeight && values[x, y, z] > surfaceValue && (y + 1) * sizeUnit > perlinHeight) values[x, y, z] = (int)Mathf.Round(((y * sizeUnit - perlinHeight) / sizeUnit) * minMarchingValue);

                    // fix up bottom values using bottom noise map
                    perlinHeight = perlinNoise2DBottom[x, z] - digDepthLimit;
                    if (values[x, y, z] < surfaceValue)
                    { 
                        if ((y + 1) * sizeUnit <= perlinHeight) values[x, y, z] = maxMarchingValue;
                        else if (y * sizeUnit <= perlinHeight) values[x, y, z] = (int)Mathf.Round(((perlinHeight - y * sizeUnit) / sizeUnit) * maxMarchingValue);
                        else if ((y - 1) * sizeUnit < perlinHeight && y * sizeUnit >= perlinHeight) values[x, y, z] = (int)Mathf.Round(((y * sizeUnit - perlinHeight) / sizeUnit) * minMarchingValue);
                    }
                }
            }
        }

        // create height map based on values (generation itself doesn't use hight map)
        heightMap = new float[width, depth];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                int maxHeight = Mathf.RoundToInt((perlinNoise2D[x, z] - digDepthLimit) / sizeUnit) + 5;
                int minHeight = Mathf.RoundToInt(digDepthLimit / sizeUnit);
                bool foundSomething = false;
                for (int y = maxHeight; y >= minHeight; y--)
                {
                    if (y > 0 && y < height && values[x, y, z] > surfaceValue)
                    {
                        foundSomething = true;
                        heightMap[x, z] = y * sizeUnit + digDepthLimit + sizeUnit * (Mathf.Abs(values[x, y, z] - values[x, y + 1, z]) / (2*maxMarchingValue));
                        break;
                    }
                }
                if (!foundSomething) heightMap[x, z] = 0f;
            }
        }
        return values;
    }

    float[,,] Get3DPerlinHeights()
    {
        // get 3d perlin noise map
        float[,,] PerlinNoise3D = new float[width, height, depth];
        float[,] userInputValues = userInputPlaneScript.GetValues();
        float maxPerlinValue = 0;
        for (int i = 0; i < perlin3DLayers; i++)
        {
            maxPerlinValue += perlin3DDHeightMultipliers[i];
        }

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int y = minTerrainHeight; y < maxTerrainHeight; y++)
                {
                    float perlinValue = 0;
                    for (int i = 0; i < perlin3DLayers; i++)
                    {
                        perlinValue += Get3DPerlinValue(x, y, z, perlin3DDensityMultipliers[i]) * perlin3DDHeightMultipliers[i];
                    }
                    perlinValue = perlinValue / maxPerlinValue;
                    PerlinNoise3D[x, y, z] = perlinValue;
                }
            }
        }
        return PerlinNoise3D;
    }

    public float Get3DPerlinValue(int x, int y, int z, float density)
    {
        Random.InitState(seedInt);
        float xCoord = (float)x / width * (density / ((float)terrainQuality / 100));
        float yCoord = (float)y / height * (density / ((float)terrainQuality / 100));
        float zCoord = (float)z / depth * (density / ((float)terrainQuality / 100));

        float AB = Mathf.PerlinNoise(xCoord + Random.value, yCoord + Random.value);
        float BC = Mathf.PerlinNoise(yCoord + Random.value, zCoord + Random.value);
        float AC = Mathf.PerlinNoise(xCoord + Random.value, zCoord + Random.value);

        float BA = Mathf.PerlinNoise(yCoord + Random.value, zCoord + Random.value);
        float CB = Mathf.PerlinNoise(zCoord + Random.value, yCoord + Random.value);
        float CA = Mathf.PerlinNoise(zCoord + Random.value, xCoord + Random.value);

        float ABC = AB + BC + AC + BA + CB + CA;
        return ABC / 6f;
    }

    // generate two dimentional height map
    float[,] GetPerlinHeights(bool bottom3Dnoise = false)
    {
        // generate offset for each layer
        Random.InitState(seedInt);
        float[] offsets = new float[perlin2DLayers];
        for (int i = 0; i < perlin2DLayers; i++)
        {
            offsets[i] = Random.value;
        }
        float[,] heights = new float[width, depth];

        // get user input heights (float 0-1)
        float[,] userInputValues = userInputPlaneScript.GetValues();

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                // get height straight from user input. Min and max values are calculated so after applying noise the terrain will go from 0 to maxTerrainHeight
                // the height itself only goes from basicTerrainMinHeight to basicTerrainMaxHeight in order to allow that
                float height;
                if (bottom3Dnoise) height = bottomTerrainMinHeight;
                else height = userInputValues[x, z] * (basicTerrainMaxHeight- basicTerrainMinHeight) + basicTerrainMinHeight;

                // apply multiple layers of noise to the user input height
                for (int i = 0; i < perlin2DLayers; i++)
                {
                    // each layer has different density, maximum height and random offset
                    if (bottom3Dnoise) height += CalculateHeight(x * terrainQuality, z * terrainQuality, offsets[i], perlin2DDensityMultipliers[i] * terrainDensity, perlin2DHeightMultipliersBottom[i] * terrainHeightLimit);
                    else height += CalculateHeight(x * terrainQuality, z * terrainQuality, offsets[i], perlin2DDensityMultipliers[i]*terrainDensity, perlin2DHeightMultipliers[i]* terrainHeightLimit);
                }
                heights[x, z] = height;
            }
        }
        return heights;
    }

    float[,] GenerateTextureLayerBorderMap()
    {
        Random.InitState(seedInt);
        float offset = Random.value;
        float[,] borderMap = new float[width, depth];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                borderMap[x, z] = CalculateHeight(x * terrainQuality, z * terrainQuality, offset, layerRandDensity, layerRand * terrainHeightLimit) - 0.5f* layerRand;
            }
        }
        return borderMap;
    }

    float[,] GenerateRockMap()
    {
        Random.InitState(seedInt);
        float offset = Random.value;
        float[,] rockMap = new float[width, depth];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                rockMap[x, z] = CalculateHeight(x * terrainQuality, z * terrainQuality, offset, rockDensity, 1.0f, true);
            }
        }
        return rockMap;
    }

    // generate a single point of a two dimentional height map based on coordinates
    float CalculateHeight(int x, int z, float offset, float density, float heightLimit, bool simpleValue = false)
    {
        float xCoord = (float)x / width * (density / ((float)terrainQuality / 100))  + offset;
        float yCoord = (float)z / depth * (density / ((float)terrainQuality / 100))  + offset;

        //Perlin noise return value from 0 to 1 so it needs to be brought into appropiate range
        if (simpleValue) return Mathf.PerlinNoise(xCoord, yCoord) * heightLimit;
        return Mathf.PerlinNoise(xCoord, yCoord) * (heightLimit - 2 * sizeUnit) + 2 * sizeUnit - heightLimit/2;
    }

    // uses polynomial rolling hash function to get a unique integer out of a string
    int StringSeedToInteger(string seed)
    {
        int result = 0;
        // p should be somewhere close to the number of possible characters
        int p = 103;
        long p_pow = 1;
        // m should be a big prime number, 10^9 + 9 is popular
        int m = (int)Mathf.Pow(10, 9) + 9;
        foreach (char letter in seed)
        {
            // ref. https://cp-algorithms.com/string/string-hashing.html#:~:text=For%20the%20conversion%2C%20we%20need,%3Dhash(t)).
            result = (int) (result + (System.Convert.ToInt32(letter)-31) * p_pow) % m;
            p_pow = (p_pow * p) % m;
        }
        return result;
    }

    // get terrain type for location, taking all factors into consideration
    // 0 - grass
    // 1 - dirt
    // 2 - rock
    // 3 - snow
    public int GetTerrainType(int x, int z)
    {
        float textureHeight = heightMap[x, z] + textureLayerBorderMap[x, z];
        int terrainType = 0;
        if (textureHeight > terrainHeightLimit * dirtStart) terrainType = 1;
        if (textureHeight > terrainHeightLimit * rockStart) terrainType = 2;
        if (textureHeight > terrainHeightLimit * snowStart) terrainType = 3;
        return terrainType;
    }
}
