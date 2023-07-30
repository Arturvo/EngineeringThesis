using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;

public class EnvironmentGeneratorScript : MonoBehaviour
{
	//user defined densities
	[Range(0, 1)] public float grassTreeDensity;
	[Range(0, 1)] public float grassBushDensity;
	[Range(0, 1)] public float grassRockDensity;
	[Range(0, 1)] public float dirtTreeDensity;
	[Range(0, 1)] public float dirtBushDensity;
	[Range(0, 1)] public float dirtRockDensity;

	public void SetGrassTreeDensity(float newValue){
		grassTreeDensity = newValue;
	}
	public void SetGrassBushDensity(float newValue){
		grassBushDensity = newValue;
	}
	public void SetGrassRockDensity(float newValue){
		grassRockDensity = newValue;
	}
	public void SetDirtTreeDensity(float newValue){
		dirtTreeDensity = newValue;
	}
	public void SetDirtBushDensity(float newValue){
		dirtBushDensity = newValue;
	}
	public void SetDirtRockDensity(float newValue){
		dirtRockDensity = newValue;
	}
	//references to prefabs
	public GameObject[] conTrees = new GameObject[3];
	public GameObject[] decTrees = new GameObject[5];
	public GameObject[] rockTypes = new GameObject[2];
	public GameObject bush;
	//list of game objects in scene
	private List<GameObject> gameObjects = new List<GameObject>();
	//are objects generated
	private bool generated = false;
	//densities in code
	private float grassTreePercentage;
	private float grassBushPercentage;
	private float grassRockPercentage;
	private float dirtTreePercentage;
	private float dirtBushPercentage;
	private float dirtRockPercentage;
	//variables from mesh generator script
	float[,] heightMap;
	private int width;
	private int depth;
	private float sizeUnit;
	private int terrainHeightLimit;
	private float[,] textureLayerBorderMap;
	private float snowStart;
	private float rockStart;
	private float dirtStart;
	private float[,] rockMap;
	private float rockThreshold;
	//radius of 
	private float radius;
	//how many times we try to add point before we reject it
	private int SamplesBeforeRejection = 30;
	//list of points
	private List<Vector2> points = new List<Vector2>();
	//list of points and their radiuses
	private List<Vector3> pointsWithRadius = new List<Vector3>();

	//sets needed variables from mesh generator and starts the generation
	public void GenerateEnvironment(float[,] heightMap, int width, int depth, float sizeUnit, int seed, int terrainHeightLimit, bool generateEnvironment, float[,] textureLayerBorderMap, float dirtStart, float rockStart, float snowStart, float[,] rockMap, float rockThreshold)
	{
		this.heightMap = heightMap;
		this.width = width;
		this.depth = depth;
		this.sizeUnit = sizeUnit;
		this.radius = 7.5f / sizeUnit;
		this.textureLayerBorderMap = textureLayerBorderMap;
		this.dirtStart = dirtStart;
		this.snowStart = snowStart;
		this.rockStart = rockStart;
		this.terrainHeightLimit = terrainHeightLimit;
		this.rockMap = rockMap;
		this.rockThreshold = rockThreshold;
		this.grassRockPercentage = 0.5f + (grassRockDensity / 6.0f);
		this.grassBushPercentage = grassRockPercentage + (grassBushDensity / 6.0f);
		this.grassTreePercentage = grassBushPercentage + (grassTreeDensity / 6.0f);
		this.dirtRockPercentage = 0.5f+(dirtRockDensity/ 6.0f);
		this.dirtBushPercentage = dirtRockPercentage + (dirtBushDensity / 6.0f);
		this.dirtTreePercentage = dirtBushPercentage + (dirtTreeDensity / 6.0f);
		UnityEngine.Random.InitState(seed);
		Generate(generateEnvironment);
	}

    //clears game objects if they were generated
    public void DestroyEnvironment()
    {
        if (generated)
        {
            foreach (GameObject gameObject in gameObjects)
            {
                Destroy(gameObject);
            }
            points.Clear();
            pointsWithRadius.Clear();
            gameObjects.Clear();
            generated = false;
        }
    }

	public void Generate(bool generateEnvironment)
	{
        //clears game objects if they were generated
        DestroyEnvironment();

        //generates environment if generateEnvironment is true
        if (generateEnvironment)
		{
			GeneratePoints(new Vector2(width - 1, depth - 1));
			GenerateObjects();

			generated = true;
		}
	}

	//generates points where game objects will be with poisson disc sampling algorithm
	public void GeneratePoints(Vector2 sampleRegionSize)
	{
		float cellSize = radius / Mathf.Sqrt(2);

		int[,] grid = new int[Mathf.CeilToInt(sampleRegionSize.x / cellSize), Mathf.CeilToInt(sampleRegionSize.y / cellSize)];

		List<Vector2> spawnPoints = new List<Vector2>();

		spawnPoints.Add(new Vector2(1.0f, 1.0f));
		while (spawnPoints.Count > 0)
		{
			int spawnIndex = UnityEngine.Random.Range(0, spawnPoints.Count);
			Vector2 spawnCentre = spawnPoints[spawnIndex];
			bool candidateAccepted = false;
			float randRadius = UnityEngine.Random.Range(radius / 2, radius);
	

			for (int i = 0; i < SamplesBeforeRejection; i++)
			{
				float angle = UnityEngine.Random.value * Mathf.PI * 2;
				Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
				Vector2 candidate = spawnCentre + direction * UnityEngine.Random.Range(randRadius, 2 * randRadius);

				if (IsValid(candidate, sampleRegionSize, cellSize, randRadius, points, grid))
				{
					spawnPoints.Add(candidate);
					points.Add(candidate);
					Vector3 pointWithRadius = new Vector3(candidate.x, candidate.y, randRadius);
					pointsWithRadius.Add(pointWithRadius);
					grid[(int)(candidate.x / cellSize), (int)(candidate.y / cellSize)] = points.Count;
					candidateAccepted = true;
					break;
				}
			}
			if (!candidateAccepted)
			{
				spawnPoints.RemoveAt(spawnIndex);
			}

		}

	}
	//checks if generated point is not colliding with other points
	static bool IsValid(Vector2 candidate, Vector2 sampleRegionSize, float cellSize, float radius, List<Vector2> points, int[,] grid)
	{
		if (candidate.x >= 0 && candidate.x < sampleRegionSize.x && candidate.y >= 0 && candidate.y < sampleRegionSize.y)
		{
			int cellX = (int)(candidate.x / cellSize);
			int cellY = (int)(candidate.y / cellSize);
			int searchAreaSide = (int)(2f * radius) + 3;
			int searchAddition = (searchAreaSide - 1) / 2;
			int searchStartX = Mathf.Max(0, cellX - searchAddition);
			int searchEndX = Mathf.Min(cellX + searchAddition, grid.GetLength(0) - 1);
			int searchStartY = Mathf.Max(0, cellY - searchAddition);
			int searchEndY = Mathf.Min(cellY + searchAddition, grid.GetLength(1) - 1);

			for (int x = searchStartX; x <= searchEndX; x++)
			{
				for (int y = searchStartY; y <= searchEndY; y++)
				{
					int pointIndex = grid[x, y] - 1;
					if (pointIndex != -1)
					{
						float sqrDst = (candidate - points[pointIndex]).sqrMagnitude;
						if (sqrDst < radius * radius)
						{
							return false;
						}
					}
				}
			}
			return true;
		}
		return false;
	}

	//generates actual game objects
	public void GenerateObjects()
	{
		float scale;
		float rotation;
		float height;
		int terrainType;
		foreach (Vector3 point in pointsWithRadius)
		{
			terrainType = GetTerrainType((int)point.x, (int)point.y);
			if (terrainType != 3 && !(terrainType==2 && rockMap[(int)point.x, (int)point.y]>rockThreshold))
			{

				GameObject terrainObject = getGameObject(point.z, terrainType);
				if (terrainObject != null)
				{
					height = CalculateHeight(point.x, point.y);
					if (height == height)
					{
						scale = CalculateScale(point.z, terrainType);
						rotation = UnityEngine.Random.Range(0, 360);
						GameObject generatedObject = (GameObject)Instantiate(terrainObject, new Vector3(sizeUnit * point.x, height, sizeUnit * point.y), Quaternion.identity);
						generatedObject.transform.localScale = new Vector3(scale, scale, scale);
						generatedObject.transform.Rotate(0.0f, (float)rotation, 0.0f, Space.Self);
						generatedObject.transform.parent = gameObject.transform;
						gameObjects.Add(generatedObject);
					}
				}
			}
		}
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

	//calculates height of point
	public float CalculateHeight(float x, float z)
	{
		float Ax = (float)Math.Floor(x);
		float Az = (float)Math.Ceiling(z);
		float Ay = heightMap[(int)Ax, (int)Az];
		float Bx = (float)Math.Ceiling(x);
		float Bz = (float)Math.Ceiling(z);
		float By = heightMap[(int)Bx, (int)Bz];
		float Cx = (float)Math.Ceiling(x);
		float Cz = (float)Math.Floor(z);
		float Cy = heightMap[(int)Cx, (int)Cz];
		float Dx = (float)Math.Floor(x);
		float Dz = (float)Math.Floor(z);
		float Dy = heightMap[(int)Dx, (int)Dz];
		float Qa = (Dz - z) / (Dz - Az) * Ay + (z - Az) / (Dz - Az) * Dy;
		float Qb = (Cz - z) / (Cz - Bz) * By + (z - Bz) / (Cz - Bz) * Cy;
		float height = (Bx - x) / (Bx - Ax) * Qa + (x - Ax) / (Bx - Ax) * Qb;

		return height;
	}

	//calculates what size game object should have
	public float CalculateScale(float size, int terrainType)
	{

		if (terrainType == 2) return UnityEngine.Random.Range(1.5f, 2.5f);
		if (terrainType == 1) return dirtBiomScale(size);
		else return grassBiomScale(size);
	}


	public float dirtBiomScale(float size)
	{
		//tree
		if (size > radius * dirtBushPercentage && size < radius * dirtTreePercentage) return 1.5f* UnityEngine.Random.Range(0.4f, 0.8f);
		//bush
		if (size > radius * dirtRockPercentage && size < radius * dirtBushPercentage) return UnityEngine.Random.Range(2.5f, 3.5f);
		if (size > radius * 0.5f && size < radius * dirtRockPercentage) return UnityEngine.Random.Range(1.5f, 2.5f);
		return 1.0f;
	}
	public float grassBiomScale(float size)
	{
		//tree
		if (size > radius * grassBushPercentage && size < radius * grassTreePercentage) return 1.5f * UnityEngine.Random.Range(0.4f, 0.8f);
		//bush
		if (size > radius * grassRockPercentage && size < radius * grassBushPercentage) return UnityEngine.Random.Range(2.5f, 3.5f);
		//stone
		if (size > radius * 0.5f && size < radius * grassRockPercentage) return UnityEngine.Random.Range(1.5f, 2.5f);
		return 1.0f;
	}


	//based on terrain type and size returns proper game object
	public GameObject getGameObject(float size, int terrainType)
	{
		if (terrainType==2) return stoneBiom(size);
		if (terrainType == 1) return dirtBiom(size);
		else return grassBiom(size);
	}

	//returns game object of stone biom
	public GameObject stoneBiom(float size)
	{
		if (size > radius * 0.5f && size > radius * dirtRockPercentage) return rockTypes[0];
		return null;
	}

	//returns game object of coniferous forest
	public GameObject dirtBiom(float size)
	{
		if (size > radius * dirtBushPercentage && size < radius * dirtTreePercentage) return conTrees[UnityEngine.Random.Range(0, conTrees.Length)];
		if (size > radius * dirtRockPercentage && size < radius * dirtBushPercentage) return bush;
		if(size > radius * 0.5f && size < radius * dirtRockPercentage) return rockTypes[0];
		return null;
	}
	
	//return game object of mixed forest
	public GameObject grassBiom(float size)
	{
		if (size > radius * grassBushPercentage && size < radius * grassTreePercentage)
		{
			int treeType = UnityEngine.Random.Range(0, 2);
			if (treeType == 0) return decTrees[UnityEngine.Random.Range(0, decTrees.Length)];
			else return conTrees[UnityEngine.Random.Range(0, conTrees.Length)];
		}
		if (size > radius * grassRockPercentage && size < radius * grassBushPercentage) return bush;
		if (size > radius * 0.5f && size < radius * grassRockPercentage) return rockTypes[0];
		return null;
	}

}
