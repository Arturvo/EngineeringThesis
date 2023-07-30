using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TerrainTool : MonoBehaviour
{
    [SerializeField] private Image[] buttons;
    [SerializeField] private Color SelectedColor, UnselectedColor;
    [Range(2f, 100f)]
    [SerializeField] private float effectDistance = 30;
    [Range(1f, 20f)]
    [SerializeField] private float strength = 5;
    [Range(0f, 5f)]
    [SerializeField] private float falloff = 1f;
    [Range(0.01f, 100f)]
    [SerializeField] private float density = 5;
    private MeshGeneratorScript generator;
    private int mode = 1, mode2 = 1;
    [Range(-1f, 1f)]
    [SerializeField]private float perlinStrength = 1;
    private MeshRenderer mrenderer;
    private Collider sphereCollider;
    private bool isAdding = false;
    private int widthChunks, depthChunks;
    private float sizeUnit;
    private float width, height, depth;
    private int Cwidth, Cheight, Cdepth;
    private int chunkSize;
    [SerializeField] private int toolType = 1; // 1-add/remove 2-flatten 3-build straight
    int terrainLayerMask;
    private float depthOffsetUnits;
    private float depthOffsetCubes;

    // EROSION PARAMETERS
    public int erosionUnitsRange = 2;
    [Range (1, 50)]
    public int numIterations = 3;
    public int seed;
    [Range (2, 8)]
    public int erosionRadius = 3;
    [Range (0, 1)]
    public float inertia = .05f; // At zero, water will instantly change direction to flow downhill. At 1, water will never change direction. 
    public float sedimentCapacityFactor = 4; // Multiplier for how much sediment a droplet can carry
    public float minSedimentCapacity = 0.5f; // Used to prevent carry capacity getting too close to zero on flatter terrain
    [Range (0, 2)]
    public float erodeSpeed = 0.3f;
    [Range (0, 2)]
    public float depositSpeed = 0.3f;
    [Range (0, 1)]
    public float evaporateSpeed = .01f;
    public float gravity = 4;
    [Range (1, 80)]
    public int maxDropletLifetime = 30;
    public float initialWaterVolume = 1;
    public float initialSpeed = 1;
    // EROSION PARAMETERS END

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

    public float GetFalloff(){
        return falloff;
    }
    public void SetFalloff(float newfalloff){
        falloff = newfalloff;
    }

    public float GetPerlinStrength(){
        return perlinStrength;
    }
    public void SetPerlinStrength(float newPerl){
        perlinStrength = newPerl;
    }

    public void SetnumIterations(float newVal){
        numIterations = (int)newVal;
    }
    public void SetmaxDropletLifetime(float newVal){
        maxDropletLifetime = (int)newVal;
    }
    public void SeterodeSpeed(float newVal){
        erodeSpeed = newVal;
    }
    public void SetdepositSpeed(float newVal){
        depositSpeed = newVal;
    }

    private void Awake() {
        mrenderer = GetComponent<MeshRenderer>();
        generator = FindObjectOfType<MeshGeneratorScript>();
        sphereCollider = GetComponent<SphereCollider>();
    }
    // Start is called before the first frame update

    public void Init()
    {
        terrainLayerMask = LayerMask.GetMask("Terrain");
        sizeUnit = generator.GetSizeUnit();
        depthOffsetUnits = generator.GetDigDepthLimit();
        depthOffsetCubes = depthOffsetUnits/sizeUnit;
        Cwidth = generator.GetWidth();
        Cdepth = generator.GetDepth();
        Cheight = generator.GetHeight();
        chunkSize = generator.GetChunkSize();
        width = Cwidth * sizeUnit;
        height = Cheight * sizeUnit;
        depth = Cdepth * sizeUnit;
        if (width % chunkSize == 0) widthChunks = Cwidth / chunkSize;
        else widthChunks = (int)Mathf.Ceil((float)Cwidth / chunkSize);
        if (depth % chunkSize == 0) depthChunks = Cdepth / chunkSize;
        else depthChunks = (int)Mathf.Ceil((float)Cdepth / chunkSize);
    }

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
        toolType = 1;
        mode = 1;
        SetButtonColors(0);
    }

    public void SetRemoving(){
        toolType = 1;
        mode = -1;
        SetButtonColors(1);
    }

    public void SetTool3(){
        toolType = 2;
        SetButtonColors(2);
    }

    public void SetTool4(){
        toolType = 3;
        mode2 = 1;
        SetButtonColors(3);
    }

    public void SetTool4mode2(){ 
        toolType = 3;
        mode2 = -1;
        SetButtonColors(4);
    }

    public void SetErosion(){ 
        toolType = 4;
        SetButtonColors(5);
    }


    public void KeepAdding(){
        sphereCollider.enabled = true;
        mrenderer.enabled = true;
        if(!isAdding) switch (toolType){
          case 1:
            StartCoroutine(AddTerrain());
            break;
          case 2:
            StartCoroutine(Noise());
            break;
          case 3:
            StartCoroutine(RaiseTerrain());
            break;
          case 4:
            StartCoroutine(Erode());
            break;
          default:
            break;
        }
        isAdding = true;
    }

    public void StopAdding(){
        sphereCollider.enabled = false;
        mrenderer.enabled = false;
        isAdding = false;
        StopAllCoroutines();
    }

    private void OnTriggerStay(Collider other) {
        if(other.gameObject.layer == 2){
            Destroy(other.gameObject);
        }
    }

    private IEnumerator AddTerrain(){
        List<int[]> updateChunks = new List<int[]>();
        //int chunkLen = generator.GetChunksLength();
        int[,,] currentValues = generator.GetValues();
        yield return new WaitForSeconds(0.1f);
        int Xindex = Mathf.RoundToInt(transform.position.x/sizeUnit);
        int Yindex = Mathf.RoundToInt(transform.position.y/sizeUnit - depthOffsetCubes);
        int Zindex = Mathf.RoundToInt(transform.position.z/sizeUnit);
        int effectUnits = Mathf.RoundToInt(effectDistance/sizeUnit);
        for(int x = -effectUnits -1; x < effectUnits +1; x += 1){
            int currX = Xindex + x;
            if(currX >= 0 && currX <= Cwidth-1){
                for(int z = -effectUnits -1; z < effectUnits +1; z += 1){
                    int currZ = Zindex + z;
                    if(currZ >= 0 && currZ <= Cdepth-1){
                        AddUpdateChunks(updateChunks, currX, currZ);
                        for(int y = -effectUnits -1; y < effectUnits +1; y += 1){
                            int currY = Yindex + y;
                            if(currY >= 0 && currY <= Cheight - 1){
                                float distance = Vector3.Distance(transform.position, new Vector3(currX*sizeUnit,currY*sizeUnit+depthOffsetUnits,currZ*sizeUnit));
                                if(distance <= effectDistance){

                                    currentValues[currX, currY, currZ] = Mathf.Clamp(currentValues[currX, currY, currZ]
                                    + Mathf.RoundToInt(mode * strength * 16 * (1 + (generator.Get3DPerlinValue(currX,currY,currZ,density)-0.5f) * perlinStrength)
                                    * ( Mathf.Pow(1 - distance/effectDistance, falloff))),-256, 256);

                                }
                            }
                        }
                    }
                }
            }
        }
        generator.Rerender(currentValues, updateChunks);
        //Debug.Log("Updating chunks: " + updateChunks.Count); //debug
        StartCoroutine(AddTerrain());
    }

    private IEnumerator RaiseTerrain(){
        List<int[]> updateChunks = new List<int[]>();
        //int chunkLen = generator.GetChunksLength();
        int[,,] currentValues = generator.GetValues();
        yield return new WaitForSeconds(0.1f);
        int Xindex = Mathf.RoundToInt(transform.position.x/sizeUnit);
        int Yindex = Mathf.RoundToInt(transform.position.y/sizeUnit - depthOffsetCubes);
        int Zindex = Mathf.RoundToInt(transform.position.z/sizeUnit);
        int effectUnits = Mathf.RoundToInt(effectDistance/sizeUnit);
        for(int x = -effectUnits -1; x < effectUnits +1; x += 1){
            int currX = Xindex + x;
            if(currX >= 0 && currX <= Cwidth-1){
                for(int z = -effectUnits -1; z < effectUnits +1; z += 1){
                    int currZ = Zindex + z;
                    if(currZ >= 0 && currZ <= Cdepth-1)
                    {
                        // chunks start
                        AddUpdateChunks(updateChunks, currX, currZ);
                        // chunks end
                        float distance2D = Vector3.Distance(new Vector3(0, 0, 0), new Vector3(x, 0, z));
                        if (distance2D <= effectUnits)
                        {
                            RaycastHit hit;
                            if (Physics.Raycast(new Vector3(currX * sizeUnit, transform.position.y + effectDistance, currZ * sizeUnit),
                                Vector3.down, out hit, 300f, terrainLayerMask))
                            {
                                float HitY = hit.point.y;
                                int midY = (int)((HitY - depthOffsetUnits) / sizeUnit);
                                float deltaHeight = mode2 * strength * 0.2f * (1 + (generator.Get3DPerlinValue(currX, midY, currZ, density) - 0.5f) * perlinStrength) * (Mathf.Pow(1 - distance2D / effectUnits, falloff));
                                ChangeHeight(currentValues, currX, midY, currZ, deltaHeight);
                                // currentValues[currX,midY,currZ] = Mathf.Clamp(currentValues[currX,midY,currZ] + Mathf.RoundToInt(mode2 * strength * 16 * (1 + (generator.Get3DPerlinValue(currX,midY,currZ,density)-0.5f) * perlinStrength)* ( Mathf.Pow(1 - distance2D/effectDistance, falloff))),-256,256);;

                            }
                        }
                    }
                }
            }
        }
        generator.Rerender(currentValues, updateChunks);
        //Debug.Log("Updating chunks: " + updateChunks.Count); //debug
        StartCoroutine(RaiseTerrain());
    }

    private void AddUpdateChunks(List<int[]> updateChunks, int currX, int currZ)
    {
        int[] inChunk;
        bool addToList;
        if (currX % chunkSize == 0)
        {
            inChunk = InWhichChunk(currX + 1, currZ);
            addToList = true;
            foreach (int[] coord in updateChunks)
            {
                if (AreCoordEqual(coord, inChunk))
                {
                    addToList = false;
                }
                if (!addToList) break;
            }
            if (addToList)
            {
                updateChunks.Add(inChunk);
            }
            if (currZ % chunkSize == 0)
            {
                inChunk = InWhichChunk(currX, currZ + 1);
                addToList = true;
                foreach (int[] coord in updateChunks)
                {
                    if (AreCoordEqual(coord, inChunk))
                    {
                        addToList = false;
                    }
                    if (!addToList) break;
                }
                if (addToList)
                {
                    updateChunks.Add(inChunk);
                }
                inChunk = InWhichChunk(currX + 1, currZ + 1);
                addToList = true;
                foreach (int[] coord in updateChunks)
                {
                    if (AreCoordEqual(coord, inChunk))
                    {
                        addToList = false;
                    }
                    if (!addToList) break;
                }
                if (addToList)
                {
                    updateChunks.Add(inChunk);
                }
            }
        }
        else if (currZ % chunkSize == 0)
        {
            inChunk = InWhichChunk(currX, currZ + 1);
            addToList = true;
            foreach (int[] coord in updateChunks)
            {
                if (AreCoordEqual(coord, inChunk))
                {
                    addToList = false;
                }
                if (!addToList) break;
            }
            if (addToList)
            {
                updateChunks.Add(inChunk);
            }
        }
        inChunk = InWhichChunk(currX, currZ);
        addToList = true;
        foreach (int[] coord in updateChunks)
        {
            if (AreCoordEqual(coord, inChunk))
            {
                addToList = false;
            }
            if (!addToList) break;
        }
        if (addToList)
        {
            updateChunks.Add(inChunk);
        }
    }

    private bool AreCoordEqual(int[] coord1, int[] coord2){
        return (coord1[0] == coord2[0] && coord1[1] == coord2[1]);
    }

    private int[] InWhichChunk(float x, float z, int ChunkLen){ // return coordinates of chunk to which point belongs in (if any)
        int chunkX = Mathf.RoundToInt(x / ChunkLen); // division without remainder
        int chunkZ = Mathf.RoundToInt(z / ChunkLen);
        //Debug.Log("Chunk x: " + chunkX.ToString() + " z: " + chunkZ.ToString()); //debug
        if(chunkX < widthChunks && chunkZ < depthChunks){
            return new int[2]{chunkX,chunkZ};
        }
        else{
            return new int[2]{-1,-1};
        }
    }

    private int[] InWhichChunk(int x, int z){ // return coordinates of chunk to which point belongs in (if any)
        int chunkZ = 0;
        int chunkX = 0;
        if (x != 0) chunkX = (x-1) / chunkSize; // division without remainder
        if (z != 0) chunkZ = (z-1) / chunkSize;
        //Debug.Log("Chunk x: " + chunkX.ToString() + " z: " + chunkZ.ToString()); //debug
        if(chunkX < widthChunks && chunkZ < depthChunks){
            return new int[2]{chunkX,chunkZ};
        }
        else{
            Debug.Log("asdasdsa");
            return new int[2]{-1,-1};
        }
    }

    private IEnumerator Noise(){
        List<int[]> updateChunks = new List<int[]>();
        //int chunkLen = generator.GetChunksLength();
        int[,,] currentValues = generator.GetValues();
        yield return new WaitForSeconds(0.2f);
        int Xindex = Mathf.RoundToInt(transform.position.x/sizeUnit);
        int Yindex = Mathf.RoundToInt(transform.position.y/sizeUnit - depthOffsetCubes);
        int Zindex = Mathf.RoundToInt(transform.position.z/sizeUnit);
        int effectUnits = Mathf.RoundToInt(effectDistance/sizeUnit);
        for(int x = -effectUnits -1; x < effectUnits +1; x += 1){
            int currX = Xindex + x;
            if(currX >= 0 && currX <= Cwidth-1){
                for(int z = -effectUnits -1; z < effectUnits +1; z += 1){
                    int currZ = Zindex + z;
                    if(currZ >= 0 && currZ <= Cdepth-1){
                        AddUpdateChunks(updateChunks, currX, currZ);
                        for(int y = -effectUnits -1; y < effectUnits +1; y += 1){
                            int currY = Yindex + y;
                            if(currY >= 0 && currY <= Cheight - 1){
                                float distance = Vector3.Distance(transform.position, new Vector3(currX*sizeUnit,currY*sizeUnit+depthOffsetUnits,currZ*sizeUnit));
                                if(distance <= effectDistance){

                                    currentValues[currX, currY, currZ] 
                                    = Mathf.Clamp(currentValues[currX, currY, currZ] + Mathf.RoundToInt((generator.Get3DPerlinValue(currX,currY,currZ,density)-0.5f) * strength * perlinStrength * 16 * ( Mathf.Pow(1 - distance/effectDistance, falloff))),-256, 256);

                                }
                            }
                        }
                    }
                }
            }
        }
        generator.Rerender(currentValues, updateChunks);
        //Debug.Log("Updating chunks: " + updateChunks.Count); //debug
        StartCoroutine(Noise());
    }

    private IEnumerator Erode(){
        yield return new WaitForSeconds(0.3f);
        List<int[]> updateChunks = new List<int[]>();
        int[,,] currentValues = generator.GetValues();
        float X = transform.position.x/sizeUnit;
        float Y = transform.position.y/sizeUnit - depthOffsetCubes;
        float Z = transform.position.z/sizeUnit;
        int Xindex = Mathf.RoundToInt(X);
        int Yindex = Mathf.RoundToInt(Y);
        int Zindex = Mathf.RoundToInt(Z);
        int effectUnits = Mathf.RoundToInt(effectDistance/sizeUnit);

        for (int iteration = 0; iteration < numIterations; iteration++) {
            //Debug.Log("iteration: " + iteration);
            // Create water droplet at random point on map
            float posX = X + Random.Range(-effectUnits, effectUnits);
            float posY = Y;
            float posZ = Z + Random.Range(-effectUnits, effectUnits);
            if (posX < 0 || posY < 0 || posZ < 0 || posX >= Cwidth-1 || posY >= Cheight-1 || posZ >= Cdepth-1 ) continue;
            float dirX = 0;
            float dirZ = 0;
            float speed = initialSpeed;
            float water = initialWaterVolume;
            float sediment = 0;

            for (int lifetime = 0; lifetime < maxDropletLifetime; lifetime++) {
                int nodeX = (int) posX;
                int nodeZ = (int) posZ;
                int nodeY = (int) posY;
                //Debug.Log("X: " + posX + " Y: " + posY + " Z: " + posZ);
                /////// int dropletIndex = nodeY * mapSize + nodeX;

                // Calculate droplet's offset inside the cell (0,0) = at NW node, (1,1) = at SE node
                float cellOffsetX = posX - nodeX;
                float cellOffsetZ = posZ - nodeZ;
                //float cellOffsetY = 0.5f; //posY - nodeY;

                // Calculate droplet's height and direction of flow with bilinear interpolation of surrounding heights
                HeightAndGradient heightAndGradient = CalculateHeightAndGradient(currentValues, posX, posY, posZ);

                // Update the droplet's direction and position (move position 1 unit regardless of speed)
                dirX = (dirX * inertia - heightAndGradient.gradientX * (1 - inertia));
                dirZ = (dirZ * inertia - heightAndGradient.gradientY * (1 - inertia));
                //Debug.Log("Xdir: " + dirX + " Zdir: " + dirZ);
                // Normalize direction
                float len = Mathf.Sqrt (dirX * dirX + dirZ * dirZ);
                if (len != 0) {
                    dirX /= len;
                    dirZ /= len;
                }
                posX += dirX;
                posZ += dirZ;

                // Stop simulating droplet if it's not moving or has flowed over edge of map
                if ((dirX == 0 && dirZ == 0) || posX < 0 || posX >= Cwidth || posZ < 0 || posZ >= Cdepth) {
                    //Debug.Log("not moving or out of bounds");
                    break;
                }

                // Find the droplet's new height and calculate the deltaHeight
                HeightAndGradient newGradient = CalculateHeightAndGradient(currentValues, posX, posY, posZ);
                float newHeight = newGradient.height;
                float deltaHeight = newHeight - heightAndGradient.height;
                //Debug.Log("delta height: " + deltaHeight);

                // Calculate the droplet's sediment capacity (higher when moving fast down a slope and contains lots of water)
                float sedimentCapacity = Mathf.Max (-deltaHeight * speed * water * sedimentCapacityFactor, minSedimentCapacity);
                //Debug.Log("sediment capacity: " + sedimentCapacity);

                // If carrying more sediment than capacity, or if flowing uphill:
                if (/*false sediment > sedimentCapacity ||*/ deltaHeight > 0) {
                    //Debug.Log("depositin");
                        int[] inChunk = InWhichChunk(nodeX, nodeZ);
                        bool addToList = true;
                        foreach(int[] coord in updateChunks){
                            if(AreCoordEqual(coord, inChunk)){
                                addToList = false;
                            }
                            if(!addToList) break;
                        }
                        AddUpdateChunks(updateChunks, nodeX, nodeZ+1);
                        AddUpdateChunks(updateChunks, nodeX+1, nodeZ);
                        AddUpdateChunks(updateChunks, nodeX+1, nodeZ+1);
                        if(addToList) updateChunks.Add(inChunk);
                    // If moving uphill (deltaHeight > 0) try fill up to the current height, otherwise deposit a fraction of the excess sediment
                    float amountToDeposit = (deltaHeight > 0) ? Mathf.Min (deltaHeight, sediment) : (sediment - sedimentCapacity) * depositSpeed;
                    //float amountToDeposit = Mathf.Min (deltaHeight, 5);
                    sediment -= amountToDeposit;

                    // Add the sediment to the four nodes of the current cell using bilinear interpolation
                    // Deposition is not distributed over a radius (like erosion) so that it can fill small pits
                    //Debug.Log("adding: " + amountToDeposit.ToString());
                    ChangeHeight(currentValues, nodeX, newGradient.SWcoordY, nodeZ, amountToDeposit * (1 - cellOffsetX) * (1 - cellOffsetZ));
                    ChangeHeight(currentValues, nodeX+1, newGradient.NWcoordY, nodeZ, amountToDeposit * cellOffsetX * (1 - cellOffsetZ));
                    ChangeHeight(currentValues, nodeX, newGradient.SEcoordY, nodeZ+1, amountToDeposit * (1 - cellOffsetX) * cellOffsetZ);
                    ChangeHeight(currentValues, nodeX+1, newGradient.NEcoordY, nodeZ+1, amountToDeposit * cellOffsetX * cellOffsetZ);
                } else {
                    // Erode a fraction of the droplet's current carry capacity.
                    // Clamp the erosion to the change in height so that it doesn't dig a hole in the terrain behind the droplet

                    //float amountToErode = Mathf.Min ((sedimentCapacity - sediment) * erodeSpeed, -deltaHeight);
                    float amountToErode = 1 * erodeSpeed;
                    if(deltaHeight < 0) amountToErode = Mathf.Min (1 * erodeSpeed, -deltaHeight);
                    for(int x = -erosionUnitsRange ; x < erosionUnitsRange +1; x += 1){
                        int currX = nodeX + x;
                        if(currX >= 0 && currX <= Cwidth-1){
                            for(int z = -erosionUnitsRange ; z < erosionUnitsRange +1; z += 1){
                                int currZ = nodeZ + z;
                                if(currZ >= 0 && currZ <= Cdepth-1){
                                    AddUpdateChunks(updateChunks, currX, currZ);

                                    float distance = new Vector3(x + cellOffsetX,0,z + cellOffsetZ).sqrMagnitude;
                                    int erosionSquared = erosionUnitsRange*erosionUnitsRange;
                                    if(distance <= erosionSquared){
                                        //totaldistance += (1.2f - distance/erosionSquared);
                                        float weighedErodeAmount = amountToErode * (1.2f - distance/erosionSquared) * 0.116f;
                                        float deltaSediment = (newGradient.SWcoordY < weighedErodeAmount) ? newGradient.SWcoordY : weighedErodeAmount;
                                        ChangeHeight(currentValues, currX, newGradient.SWcoordY, currZ, -deltaSediment);
                                        sediment += deltaSediment;
                                        //Debug.Log("removing: " + deltaSediment.ToString());
                                        //}
                                    }
                                }
                            }
                        }
                    }
                }

                // Update droplet's speed and water content
                speed = Mathf.Sqrt (speed * speed + deltaHeight * gravity);
                water *= (1 - evaporateSpeed);
            }
        }

        generator.Rerender(currentValues, updateChunks);
        //Debug.Log("Updating chunks: " + updateChunks.Count); //debug
        StartCoroutine(Erode());
    }

    HeightAndGradient CalculateHeightAndGradient (int[,,] values, float posX, float posY, float posZ) {
        int coordY = Mathf.Clamp((int) posY, 0, Cheight -2 );
        int coordX = Mathf.Clamp((int) posX, 0, Cwidth-2 );
        int coordZ = Mathf.Clamp((int) posZ, 0, Cdepth -2 );
        
        if(values[coordX,coordY,coordZ] >= 0){
            while(coordY < Cheight-2 && values[coordX,coordY+1,coordZ] > 0){
                coordY += 1;
            }
        }
        else if(values[coordX,coordY,coordZ] < 0){
            while(values[coordX,coordY,coordZ] < 0 && coordY > 0){
                coordY -= 1;
            }
        }
        //Debug.Log("terrain height at: " + coordY);
        
        // Calculate droplet's offset inside the cell (0,0) = at NW node, (1,1) = at SE node
        float x = posX - coordX;
        float z = posZ - coordZ;

        // Calculate heights of the four nodes of the droplet's cell
        int SWcoordY = coordY;
        int NWcoordY = coordY;
        int NEcoordY = coordY;
        int SEcoordY = coordY;

        if(values[coordX,NWcoordY,coordZ+1] >= 0){
            while(NWcoordY < Cheight-2 && values[coordX,NWcoordY+1,coordZ+1] > 0){
                NWcoordY += 1;
            }
        }
        else if(values[coordX,NWcoordY,coordZ+1] < 0){
            while(values[coordX,NWcoordY,coordZ+1] < 0 && NWcoordY > 0){
                NWcoordY -= 1;
            }
        }

        if(values[coordX+1,NEcoordY,coordZ+1] >= 0){
            while(NEcoordY < Cheight-2 && values[coordX+1,NEcoordY+1,coordZ+1] > 0){
                NEcoordY += 1;
            }
        }
        else if(values[coordX+1,NEcoordY,coordZ+1] < 0){
            while(values[coordX+1,NEcoordY,coordZ+1] < 0 && NEcoordY > 0){
                NEcoordY -= 1;
            }
        }

        if(values[coordX+1,SEcoordY,coordZ] >= 0){
            while(SEcoordY < Cheight-2 && values[coordX+1,SEcoordY+1,coordZ] > 0){
                SEcoordY += 1;
            }
        }
        else if(values[coordX+1,SEcoordY,coordZ] < 0){
            while(values[coordX+1,SEcoordY,coordZ] < 0 && SEcoordY > 0){
                SEcoordY -= 1;
            }
        }
        float heightSW = SWcoordY + Mathf.Abs(values[coordX, SWcoordY, coordZ]/( Mathf.Abs(values[coordX, SWcoordY, coordZ]) + Mathf.Abs(values[coordX,SWcoordY+1,coordZ]+0.01f) ));
        float heightNW = NWcoordY + Mathf.Abs(values[coordX, NWcoordY, coordZ+1]/(Mathf.Abs(values[coordX, NWcoordY, coordZ+1]) + Mathf.Abs(values[coordX, NWcoordY+1, coordZ+1]+0.01f)));
        float heightNE = NEcoordY + Mathf.Abs(values[coordX+1, NEcoordY, coordZ+1]/(Mathf.Abs(values[coordX+1, NEcoordY, coordZ+1]) + Mathf.Abs(values[coordX+1, NEcoordY+1, coordZ+1]+0.01f)));
        float heightSE = SEcoordY + Mathf.Abs(values[coordX+1, SEcoordY, coordZ]/(Mathf.Abs(values[coordX+1, SEcoordY, coordZ]) + Mathf.Abs(values[coordX+1, SEcoordY+1, coordZ]+0.01f)));
        if (heightSW < 0) Debug.Log("WTFFFFFFFFFFFFFFFFFFFFFF, heightSW is " + heightSW);
        if (heightNW < 0) Debug.Log("WTFFFFFFFFFFFFFFFFFFFFFF, heightNW is " + heightSW);
        if (heightNE < 0) Debug.Log("WTFFFFFFFFFFFFFFFFFFFFFF, heightNE is " + heightSW);
        if (heightSE < 0) Debug.Log("WTFFFFFFFFFFFFFFFFFFFFFF, heightSE is " + heightSW);

        // Calculate droplet's direction of flow with bilinear interpolation of height difference along the edges
        float gradientX = (heightNE - heightNW) * z + (heightSE - heightSW) * (1-z);
        float gradientY = (heightNW - heightSW) * (1 - x) + (heightNE - heightSE) * x;

        // Calculate height with bilinear interpolation of the heights of the nodes of the cell
        float height = heightNW * (1 - x) * z + heightNE * x * z + heightSW * (1 - x) * (1-z) + heightSE * x * (1-z);

        return new HeightAndGradient () { height = height, gradientX = gradientX, gradientY = gradientY, SWcoordY = SWcoordY, NWcoordY = NWcoordY, NEcoordY = NEcoordY, SEcoordY = SEcoordY };
    }

    struct HeightAndGradient {
        public float height;
        public float gradientX;
        public float gradientY;
        public int SWcoordY;
        public int NWcoordY;
        public int NEcoordY;
        public int SEcoordY;
    }

    private void ChangeHeight(int[,,] values, int x, int y, int z, float deltaUnits){
        //Debug.Log(deltaUnits);
        if(values[x, y, z] < 0 && values[x, y+1, z] < 0){
            while(values[x, y, z] < 0){
                y -= 1;
                if (y < 0) return;
            }
        }
        if(values[x, y, z] > 0 && values[x, y+1, z] > 0){
            while(values[x, y+1, z] > 0){
                y += 1;
                if (y >= Cheight-1) return;
            }
        }
        int steps = (int)deltaUnits;
        float surfacePos = deltaUnits - steps;
        surfacePos += Mathf.Abs(values[x, y, z]/(Mathf.Abs(values[x, y, z]) + Mathf.Abs(values[x,y+1,z])+0.01f));
        if(surfacePos < 0){
            steps -= 1;
            surfacePos = 1 + surfacePos;
        }
        else if(surfacePos >= 1){
            steps += 1;
            surfacePos -= 1;
        }
        if(deltaUnits > 0){
            int i = 0;
            for(i = 0; i < steps; i++){
                if(y+i+2 >= Cheight) return;
                values[x,y+i+2,z] = values[x,y+i+1,z];
                values[x,y+i+1,z] = values[x,y+i,z];
                values[x,y+i,z] = 256;
            }
            values[x,y+i+1,z] = Mathf.RoundToInt(-256 * (1-surfacePos));
            values[x,y+i,z] =  Mathf.RoundToInt( 256 * surfacePos);

        }
        else if(deltaUnits < 0){
            int i = 0;
            for(i = 0; i > steps; i--){
                if(y-i-2 < 0) return;
                values[x,y-i-2,z] = values[x,y-i-1,z];
                values[x,y-i-1,z] = values[x,y-i,z];
                values[x,y-i,z] = -256;
            }
            values[x,y+i+1,z] = Mathf.RoundToInt(-256 * (1-surfacePos));
            values[x,y+i,z] =  Mathf.RoundToInt( 256 * surfacePos);
        }
    }
}
