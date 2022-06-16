using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClipMapManager : MonoBehaviour
{   
    //Public Fields
    public Material seaMaterial;
    public Material trimSeaMaterial;

    public bool updateDynamically = false;

    [Range (1,15)]
    public int clipMapLevels = 1;

    [Range (1,255)]
    public int clipMapResolution = 5;
    [Range (0,10)]
    public float clipMapSize = 5;

    public Transform followTarget;

    public Transform playArea;

    //Wave parameters
    [Range (0,1)]
    public float _SineSpeed;
    
    [Range (0,1)]
    public float _Amplitude;

    [Header("Wave Attributes")]

    [Tooltip("Direction (x,y) | Steepness z (0,1) | Wavenumber w")]
    public Vector4 _WaveA;
    
    [Tooltip("Direction (x,y) | Steepness z (0,1) | Wavenumber w")]
    public Vector4 _WaveB;
    
    [Tooltip("Direction (x,y) | Steepness z (0,1) | Wavenumber w")]
    public Vector4 _WaveC;

    public const float totalPlayAreaLength = 10.0f;

    //Private Fields
    private Environment environment;
    private Timer timer;
    private float trackingOffsetTime;

    private enum MeshComponent {SQUARE, FILLER, TRIM, SEAM};
    private Vector3 localTranslate = new Vector3(0,0,0);
    private Vector3 nextLocalTranslate = new Vector3(0,0,0);
    private Quaternion[] trimRotations = new Quaternion[4];

    private float cellSize;
    private float nextCellSize;
    private MaterialPropertyBlock propBlock;

    private MaterialPropertyBlock trimPropBlock;
    
    public float getTotalPlayArea(){
        return totalPlayAreaLength;
    }
    public void init(Environment environment, Transform mainCamera){
        //Set material properties
        seaMaterial.SetFloat("totalPlayAreaLength", totalPlayAreaLength);
        seaMaterial.SetFloat("_SineSpeed", _SineSpeed);
        seaMaterial.SetFloat("_Amplitude", _Amplitude);
        seaMaterial.SetVector("_WaveA", _WaveA);
        seaMaterial.SetVector("_WaveB", _WaveB);
        seaMaterial.SetVector("_WaveC", _WaveC);

        trimSeaMaterial.SetFloat("totalPlayAreaLength", totalPlayAreaLength);
        trimSeaMaterial.SetFloat("_SineSpeed", _SineSpeed);
        trimSeaMaterial.SetFloat("_Amplitude", _Amplitude);
        trimSeaMaterial.SetVector("_WaveA", _WaveA);
        trimSeaMaterial.SetVector("_WaveB", _WaveB);
        trimSeaMaterial.SetVector("_WaveC", _WaveC);

        //Initialize trim rotations
        trimRotations[0] = Quaternion.AngleAxis(0, Vector3.up);
        trimRotations[1] = Quaternion.AngleAxis(90, Vector3.up);
        trimRotations[2] = Quaternion.AngleAxis(270, Vector3.up);
        trimRotations[3] = Quaternion.AngleAxis(180, Vector3.up);

        //Make property blocks
        propBlock = new MaterialPropertyBlock();
        trimPropBlock = new MaterialPropertyBlock();

        //Set environment
        this.environment = environment;
        this.timer = environment.GetTimer();
        //Set camera
        this.followTarget = mainCamera;
        //Make clipmap
        MakeClipMap();
    }

    void FixedUpdate()
    {   
        //Allow for dynamic updating of clipmap properties (Incredibly expensive)
        if(updateDynamically){
            foreach (Transform child in this.transform) {
                GameObject.Destroy(child.gameObject);
            }
            MakeClipMap();
        }
        //Set time value for simulation
        if(environment){
            trackingOffsetTime = timer.getPlayheadTrackingOffset();
        } else {
            trackingOffsetTime = Time.fixedTime;
        }
        //Translate the clipmap
        translateClipmap();
    }

    //Translates the clipmap mesh
    void translateClipmap(){
        //Assumes clipmap levels are ordered
        int nextRotIndex = 0;
        //Translate camera position from it's local position to our local space
        Vector3 cameraLocalPos = this.transform.InverseTransformPoint(followTarget.position);
        //For each clipmap level
        for (int clipMapLevel = 0; clipMapLevel < this.transform.childCount; clipMapLevel++) {
            Transform child = this.transform.GetChild(clipMapLevel);
            //Scaling coefficient for subsequent clipmap levels
            int clipMapCoefficient = (int) Mathf.Pow(2, clipMapLevel);

            //If on first level, calculate the translate, otherwise we know it from previous trim calculation
            if(clipMapLevel == 0){
                cellSize = (clipMapSize * clipMapCoefficient/ (float)clipMapResolution);
                localTranslate.x = Mathf.Floor(cameraLocalPos.x / cellSize) * cellSize;
                localTranslate.z = Mathf.Floor(cameraLocalPos.z / cellSize) * cellSize;
            } else {
                cellSize = nextCellSize;
                localTranslate = nextLocalTranslate;
            }

            //Each cell size is twice as much as the previous
            nextCellSize = cellSize * 2.0f;
            //Get snapped translation values relative to camera (followTarget)
            nextLocalTranslate.x = Mathf.Floor(cameraLocalPos.x / nextCellSize) * nextCellSize;
            nextLocalTranslate.z = Mathf.Floor(cameraLocalPos.z / nextCellSize) * nextCellSize;

            child.localPosition = localTranslate;

            //Calculate uv offset relative to play area (assumes both initialized at 0,0)
            float uvxOffset = (((child.localPosition.x) + ((totalPlayAreaLength / 2) - playArea.localPosition.x)) / totalPlayAreaLength) - 0.5f;
            float uvyOffset = (((child.localPosition.z) + ((totalPlayAreaLength / 2) - playArea.localPosition.z)) / totalPlayAreaLength) - 0.5f;
            
            //Set material values for each child (each submesh in the clipmap level)
            foreach (Transform cMapChild in child.transform){
                Renderer renderer = cMapChild.GetComponent<Renderer>();
                renderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat("uvxOffset", uvxOffset);
                propBlock.SetFloat("uvyOffset", uvyOffset);
                propBlock.SetFloat("trackingOffsetTime", trackingOffsetTime);
                renderer.SetPropertyBlock(propBlock);
            }
            
            //Translate trim (no trim on top level)
            if(clipMapLevel < (clipMapLevels - 1)){
                nextRotIndex = 0;
                //Check if translation will occur, using index as switch
                if(localTranslate.x - nextLocalTranslate.x >= cellSize - 0.0001) nextRotIndex+=2;
                if(localTranslate.z - nextLocalTranslate.z >= cellSize - 0.0001) nextRotIndex+=1;
                
                Transform trim = child.transform.Find("TRIM:" + clipMapLevel + " (0,0)");
                //Set trim rotation
                trim.localRotation = trimRotations[nextRotIndex];
                Renderer renderer = trim.GetComponent<Renderer>();
                renderer.GetPropertyBlock(trimPropBlock);
                
                //Set trim values for given rotation
                trimPropBlock.SetFloat("trackingOffsetTime", trackingOffsetTime);
                switch (nextRotIndex)
                {
                    case 0:
                        //No rotation
                        trim.localPosition = new Vector3(0, 0, 0);
                        trimPropBlock.SetFloat("trimAngle", 0);
                        trimPropBlock.SetFloat("uvxOffset", uvxOffset);
                        trimPropBlock.SetFloat("uvyOffset", uvyOffset);
                        break;
                    case 1:
                        //Rotation over Z axis
                        trim.localPosition = new Vector3(0, 0, cellSize);
                        trimPropBlock.SetFloat("trimAngle", 270 * Mathf.Deg2Rad);
                        trimPropBlock.SetFloat("uvxOffset", uvxOffset);
                        trimPropBlock.SetFloat("uvyOffset", uvyOffset + (cellSize / totalPlayAreaLength));
                        break;
                    case 2:
                        //Rotation over X axis
                        trim.localPosition = new Vector3(cellSize, 0, 0);
                        trimPropBlock.SetFloat("trimAngle", 90 * Mathf.Deg2Rad);
                        trimPropBlock.SetFloat("uvxOffset", uvxOffset + (cellSize / totalPlayAreaLength));
                        trimPropBlock.SetFloat("uvyOffset", uvyOffset);
                        break;
                    case 3:
                        //Rotation over X and Z axis
                        trim.localPosition = new Vector3(cellSize, 0, cellSize);
                        trimPropBlock.SetFloat("trimAngle", 180 * Mathf.Deg2Rad);
                        trimPropBlock.SetFloat("uvxOffset", uvxOffset + (cellSize / totalPlayAreaLength));
                        trimPropBlock.SetFloat("uvyOffset", uvyOffset + (cellSize / totalPlayAreaLength));
                        break;
                    default:
                        break;
                }
                renderer.SetPropertyBlock(trimPropBlock);
            }
        }
    }

    //Makes the clipmap mesh
    void MakeClipMap(){
        int clipMapCoefficient = (int) Mathf.Pow(2, (clipMapLevels - 1));
        //Space between vertices
        float cellSize = (clipMapSize * clipMapCoefficient/ (float)clipMapResolution);
        //Generate clipmap levels
        for( int l = 0; l < clipMapLevels; l++ ) {

            GameObject clipMapLevelObj = new GameObject();
            clipMapLevelObj.name = "ClipMapLevel: " + l;
            clipMapLevelObj.transform.SetParent(this.transform,false);

            //Make Squares
            for( int x = 0; x < 4; x++ ) {
                    for( int y = 0; y < 4; y++ ) {
                            //innermost level or not in the middle 2x2
                            if(l == 0 || (y == 0 || y == 3) || (x != 1 && x != 2)){
                                //draw a square tile;
                                MakeMeshComponent(MeshComponent.SQUARE, clipMapLevelObj, l, x , y);
                            }
                    }
            }


            //draw this level's filler;
            MakeMeshComponent(MeshComponent.FILLER, clipMapLevelObj, l, 0,0);
            
            if(l != clipMapLevels - 1){
                //draw this level's trim; (don't need top at level)
                MakeMeshComponent(MeshComponent.TRIM, clipMapLevelObj, l, 0,0);
            }

            if(l != 0){
                //draw this level's seam; (hugs inside of level, so no need for level 0)
                MakeMeshComponent(MeshComponent.SEAM, clipMapLevelObj, l, 0,0);
            }
        }
    }

    //Makes a given mesh component, at a given clipmap level, at given coordinates
    void MakeMeshComponent(MeshComponent meshType, GameObject parent, int clipMapLevel, int x, int y){
        Mesh mesh;

        //Define gameobject for mesh
        GameObject clipComp = new GameObject();
        clipComp.name = meshType.ToString() + ":" + clipMapLevel + " (" + x + "," + y + ")";
        clipComp.AddComponent<MeshFilter>();
        clipComp.AddComponent<MeshRenderer>();
        clipComp.transform.SetParent(parent.transform, false);

        //Get mesh and generate correct mesh type
        mesh = clipComp.GetComponent<MeshFilter>().mesh;
        switch (meshType)
        {
            case MeshComponent.SQUARE:
                makeSquareMesh(mesh, x, y, clipMapLevel);
                clipComp.GetComponent<MeshRenderer>().material = seaMaterial;
                break;

            case MeshComponent.FILLER:
                makeFillerMesh(mesh, clipMapLevel);
                clipComp.GetComponent<MeshRenderer>().material = seaMaterial;
                break;

            case MeshComponent.TRIM:
                makeTrimMesh(mesh, clipMapLevel);
                clipComp.GetComponent<MeshRenderer>().material = trimSeaMaterial;
                break;

            case MeshComponent.SEAM:
                makeSeamMesh(mesh, clipMapLevel);
                clipComp.GetComponent<MeshRenderer>().material = seaMaterial;
                break;

            default:
                Debug.Log("Error, Not a valid meshType");
                break;
        }
    }

    //Makes a Square mesh for the clipmap
    private void makeSquareMesh(Mesh mesh, int i, int j, int clipMapLevel){
        //Number of vertices per row/column (nxn)
        int vertexPerLine = clipMapResolution + 1;
        //Scaling coefficient for subsequent clipmap levels
        int clipMapCoefficient = (int) Mathf.Pow(2, clipMapLevel);
        //Space between vertices
        float cellSize = (clipMapSize * clipMapCoefficient/ (float)clipMapResolution);


        //Set array sizes
        Vector3[] vertices = new Vector3[vertexPerLine * vertexPerLine];
        Vector2[] uvs = new Vector2[vertexPerLine * vertexPerLine];
        int[] triangles = new int[6 * (vertexPerLine - 1) * (vertexPerLine - 1)];

        //Set vertex offset (ensures centered grid)
        float vertexOffset = (clipMapSize * 2) * clipMapCoefficient; 

        //Set x and y offsets to make room for filler
        float xoff = (i < 2) ? 0 : cellSize;
        float yoff = (j < 2) ? 0 : cellSize;
    
        int vert = 0;
        int tri = 0;
        //Define Vertices and triangles
        for(int x = 0; x <= clipMapResolution; x++){
            for (int z = 0; z <= clipMapResolution; z++){
                //Vertex location
                vertices[vert] = new Vector3(((x * cellSize) + (i * clipMapSize * clipMapCoefficient) - (vertexOffset) + xoff), 
                                               0, 
                                             ((z * cellSize) + (j * clipMapSize * clipMapCoefficient) - (vertexOffset) + yoff));
                //UV Definition
                uvs[vert] = new Vector2((((vertices[vert].x) + ((totalPlayAreaLength / 2))) / totalPlayAreaLength),
                                         ((vertices[vert].z) + ((totalPlayAreaLength / 2)))/ totalPlayAreaLength);

                //Define triangles
                if(x < clipMapResolution && z < clipMapResolution){
                    triangles[tri] = vert;
                    triangles[tri+1] = triangles[tri+4] = vert+1;
                    triangles[tri+2] = triangles[tri+3] = vert + vertexPerLine;
                    triangles[tri+5] = vert + vertexPerLine + 1;
                    tri+=6;
                }
                vert++;
            }
        }
        //Set mesh values
        UpdateMesh(mesh, vertices, uvs, triangles);
    }

    //Makes a filler mesh for the given clipmap level
    private void makeFillerMesh(Mesh mesh, int clipMapLevel){
        //'Length' in vertices per 'arm' of the filler
        int vertexPerLine = 2 * (clipMapResolution) + 2;
        //Scaling coefficient for subsequent clipmap levels
        int clipMapCoefficient = (int) Mathf.Pow(2, clipMapLevel);
        //Space between vertices
        float cellSize = (clipMapSize * clipMapCoefficient/ (float)clipMapResolution);


        //Set array sizes
        //Boundary represents only needing to fill to the center for the lowest clipmap level
        int boundary = (clipMapLevel == 0)? vertexPerLine:vertexPerLine/2;
        Vector3[] vertices = new Vector3[boundary * 8];
        Vector2[] uvs = new Vector2[boundary * 8];
        int[] triangles = new int[(24 * (boundary - 1))];

        int vert = 0;
        int tri = 0;

        //Precompute some loop values
        //Represents needing to space out one more cell for outer rings
        float ringBumpVal = (clipMapLevel == 0)? 0:cellSize;
        //'Toggle' that sets the additional spacing for subsequent clipmap levels
        int clipOn = (clipMapLevel == 0)?0:1;
        //Spacer that moves fillers out for subsequent clipmap levels
        float clipLevelAdjustment = clipOn * (clipMapCoefficient * clipMapSize);

        float playArea = (totalPlayAreaLength / 2);

        for(int x = 0; x < 2; x++){
            for (int z = 0; z < boundary; z++){
                //Vertex locations
                //Vertical arm +z vertex
                vertices[vert] = new Vector3((x * cellSize),
                                              0,
                                             (z * cellSize)+ clipLevelAdjustment + ringBumpVal);
                //Vertical arm -z vertex
                vertices[vert + (2 * boundary)] = new Vector3((x * cellSize),
                                                               0, 
                                                              (z * -cellSize) - clipLevelAdjustment - ringBumpVal + cellSize);
                //Horizontal arm +x vertex
                vertices[vert + (4 * boundary)] = new Vector3((z * cellSize) + clipLevelAdjustment + ringBumpVal,
                                                               0,
                                                              (x * cellSize));
                //Horizontal arm -x vertex
                vertices[vert + (6 * boundary)] = new Vector3((z * -cellSize) - clipLevelAdjustment - ringBumpVal + cellSize,
                                                               0,
                                                              (x * cellSize));
                //UV Definitions
                uvs[vert] = new Vector2(((vertices[vert].x + playArea) / totalPlayAreaLength),
                                         (vertices[vert].z + playArea) / totalPlayAreaLength);

                uvs[vert + (2 * boundary)] = new Vector2(((vertices[vert + (2 * boundary)].x + playArea) / totalPlayAreaLength),
                                                          (vertices[vert + (2 * boundary)].z + playArea) / totalPlayAreaLength);

                uvs[vert + (4 * boundary)] = new Vector2(((vertices[vert + (4 * boundary)].x + playArea) / totalPlayAreaLength),
                                                          (vertices[vert + (4 * boundary)].z + playArea) / totalPlayAreaLength);

                uvs[vert + (6 * boundary)] = new Vector2(((vertices[vert + (6 * boundary)].x + playArea) / totalPlayAreaLength),
                                                          (vertices[vert + (6 * boundary)].z + playArea) / totalPlayAreaLength);                                                                                  
                
                //Define triangles
                if(x == 0 && z < (boundary - 1)){
                    //Vertical arm +z triangles
                    triangles[tri] = vert;
                    triangles[tri+1] = triangles[tri+3] = vert + 1;
                    triangles[tri+2] = triangles[tri+5] = vert + boundary;
                    triangles[tri+4] = vert + boundary + 1;
                    //Vertical arm -z triangles
                    triangles[tri + (6 * (boundary - 1))] = triangles[tri+5 + (6 * (boundary - 1))] = vert + (2 * boundary);
                    triangles[tri+1 + (6 * (boundary - 1))] = vert + boundary + (2 * boundary);
                    triangles[tri+2 + (6 * (boundary - 1))] = triangles[tri+3 + (6 * (boundary - 1))] = vert + boundary + 1 + (2 * boundary);
                    triangles[tri+4 + (6 * (boundary - 1))] = vert + 1 + (2 * boundary);
                    //Horizontal arm +x triangles
                    triangles[tri + (12 * (boundary - 1))] = vert + (4 * boundary);
                    triangles[tri+1 + (12 * (boundary - 1))] = triangles[tri+4 + (12 * (boundary - 1))] = vert + boundary + (4 * boundary);
                    triangles[tri+2 + (12 * (boundary - 1))] = triangles[tri+3 + (12 * (boundary - 1))] = vert + 1 + (4 * boundary);
                    triangles[tri+5 + (12 * (boundary - 1))] = vert + boundary + 1 + (4 * boundary);
                    //Horizontal arm -x triangles
                    triangles[tri + (18 * (boundary - 1))] = triangles[tri+3 + (18 * (boundary - 1))] = vert + (6 * boundary);
                    triangles[tri+1 + (18 * (boundary - 1))] = vert + 1 + (6 * boundary);
                    triangles[tri+2 + (18 * (boundary - 1))] = triangles[tri+4 + (18 * (boundary - 1))] = vert + boundary + 1 + (6 * boundary);
                    triangles[tri+5 + (18 * (boundary - 1))] = vert + boundary + (6 * boundary);

                    tri += 6;
                }
                vert++;
            }
        }
        //Set mesh values
        UpdateMesh(mesh, vertices, uvs, triangles);
    }
    
    //Generates a trim mesh for the given clipmap level
    private void makeTrimMesh(Mesh mesh, int clipMapLevel){
        //'Length' in vertices per 'arm' of the filler
        int vertexPerLine = 4 * (clipMapResolution) + 3;
        //Scaling coefficient for subsequent clipmap levels
        int clipMapCoefficient = (int) Mathf.Pow(2, clipMapLevel);
        //Space between vertices
        float cellSize = (clipMapSize * clipMapCoefficient/ (float)clipMapResolution);

        //Set array sizes
        //Boundary represents only needing to fill to the center for the lowest clipmap level
        Vector3[] vertices = new Vector3[vertexPerLine * 4];
        Vector2[] uvs = new Vector2[vertexPerLine * 4];
        int[] triangles = new int[12 * (vertexPerLine - 1) - 6];

        int vert = 0;
        int tri = 0;

        //Spacer that moves fillers out for subsequent clipmap levels
        float trimAdjust = (clipMapCoefficient * (2 * clipMapSize));
        
        float playArea = (totalPlayAreaLength / 2);

        for(int x = 0; x < 2; x++){
            for (int z = 0; z < vertexPerLine; z++){
                //Vertex locations

                //Vertical arm vertex
                vertices[vert] = new Vector3((x * cellSize) + trimAdjust + cellSize,
                                              0, 
                                             (z * -cellSize) + trimAdjust + (2 * cellSize));
                
                //Horizontal arm +x vertex
                vertices[vert + (2 * vertexPerLine)] = new Vector3((z * -cellSize) + trimAdjust + (2 * cellSize),
                                                                    0,
                                                                   (x * cellSize) + trimAdjust + cellSize);

                //UV Definitions
                uvs[vert] = new Vector2(((vertices[vert].x + playArea) / totalPlayAreaLength),
                                         (vertices[vert].z + playArea) / totalPlayAreaLength);

                uvs[vert + (2 * vertexPerLine)] = new Vector2(((vertices[vert + (2 * vertexPerLine)].x + playArea) / totalPlayAreaLength),
                                                              (vertices[vert + (2 * vertexPerLine)].z + playArea)/ totalPlayAreaLength);
                
                //Define triangles
                if(x == 0 && z < (vertexPerLine - 1)){
                    //Vertical arm triangles
                    triangles[tri] = triangles[tri+4] = vert;
                    triangles[tri+1] = triangles[tri+3] = vert + vertexPerLine + 1;
                    triangles[tri+2] = vert + 1;
                    triangles[tri+5] = vert + vertexPerLine;
                    if(tri != 0){
                        //Horizontal arm triangles
                        triangles[tri + (6 * (vertexPerLine - 1)) - 6] = triangles[tri+5 + (6 * (vertexPerLine - 1)) - 6] = vert + vertexPerLine + 1 + (2 * vertexPerLine);
                        triangles[tri+1 + (6 * (vertexPerLine - 1)) - 6] = vert + vertexPerLine + (2 * vertexPerLine);
                        triangles[tri+2 + (6 * (vertexPerLine - 1)) - 6] = triangles[tri+3 + (6 * (vertexPerLine - 1)) - 6] = vert + (2 * vertexPerLine);
                        triangles[tri+4 + (6 * (vertexPerLine - 1)) - 6] = vert + 1 + (2 * vertexPerLine);
                    }
                    tri += 6;
                }
                vert++;
            }
        }
        //Set mesh values
        UpdateMesh(mesh, vertices, uvs, triangles);
    }
    
    //Generates a Seam mesh for the given clipmap level
    private void makeSeamMesh(Mesh mesh, int clipMapLevel){
        //'Length' in vertices per 'arm' of the filler
        int vertexPerLine = 4 * (clipMapResolution) + 3;
        //Scaling coefficient for subsequent clipmap levels (do -1 so that we generate inside seam)
        int clipMapCoefficient = (int) Mathf.Pow(2, (clipMapLevel - 1));
        //Space between vertices
        float cellSize = (clipMapSize * clipMapCoefficient/ (float)clipMapResolution);

        //Set array sizes
        Vector3[] vertices = new Vector3[vertexPerLine * 4];
        Vector3[] normals = new Vector3[vertexPerLine * 4];
        Vector2[] uvs = new Vector2[vertexPerLine * 4];
        int[] triangles = new int[12 * (vertexPerLine - 1) - 6];

        int vert = 0;
        int tri = 0;

        //Spacer that moves seam out for subsequent clipmap levels
        float seamAdjust = (clipMapCoefficient * (2 * clipMapSize));
        
        float playArea = (totalPlayAreaLength / 2);

        for (int z = 0; z < vertexPerLine; z++){
            //Vertex locations
            //Top vertex
            vertices[vert] = new Vector3((z * -cellSize) + seamAdjust + (2 * cellSize),
                                          0, 
                                          seamAdjust + (2 * cellSize));
            //Bottom vertex
            vertices[vert + vertexPerLine] = new Vector3((z * -cellSize) + seamAdjust + (2 * cellSize),
                                                          0,
                                                          -seamAdjust);
            //Left vertex
            vertices[vert + (2 * vertexPerLine)] = new Vector3(seamAdjust + (2 * cellSize),
                                                               0,
                                                              (z * -cellSize) + seamAdjust + (2 * cellSize));
            //Right vertex
            vertices[vert + (3 * vertexPerLine)] = new Vector3(-seamAdjust,
                                                               0,
                                                              (z * -cellSize) + seamAdjust + (2 * cellSize));

            //UV Definitions
            uvs[vert] = new Vector2(((vertices[vert].x + playArea) / totalPlayAreaLength),
                                     (vertices[vert].z + playArea) / totalPlayAreaLength);

            uvs[vert + vertexPerLine] = new Vector2(((vertices[vert + vertexPerLine].x + playArea) / totalPlayAreaLength),
                                                     (vertices[vert + vertexPerLine].z + playArea) / totalPlayAreaLength);  

            uvs[vert + (2 * vertexPerLine)] = new Vector2(((vertices[vert + (2 * vertexPerLine)].x + playArea) / totalPlayAreaLength),
                                                          (vertices[vert + (2 * vertexPerLine)].z + playArea) / totalPlayAreaLength); 

            uvs[vert + (3 * vertexPerLine)] = new Vector2(((vertices[vert + (3 * vertexPerLine)].x + playArea) / totalPlayAreaLength),
                                                          (vertices[vert + (3 * vertexPerLine)].z + playArea) / totalPlayAreaLength);

            //Normals up to avoid artifact (possibly not ideal)
            normals[vert] = 
            normals[vert + vertexPerLine] = 
            normals[vert + (2 * vertexPerLine)] = 
            normals[vert + (3 * vertexPerLine)] = new Vector3(0, 1, 0);
            
            //Define triangles
            if(z < (vertexPerLine - 1) && z > 0 && (z + 1) % 2 == 0){
                //Top triangles
                triangles[tri] = vert;
                triangles[tri+1] = vert + 1;
                triangles[tri+2] = vert - 1;

                //Bottom triangles
                triangles[tri + (3 * ((vertexPerLine - 1) / 2))] = vert + vertexPerLine;
                triangles[tri+1 + (3 * ((vertexPerLine - 1) / 2))] = vert - 1 + vertexPerLine;
                triangles[tri+2 + (3 * ((vertexPerLine - 1) / 2))] = vert + 1 + vertexPerLine; 

                //Left triangles
                triangles[tri + (3 * ((vertexPerLine - 1)))] = vert + (2 * vertexPerLine);
                triangles[tri+1 + (3 * ((vertexPerLine - 1)))] = vert - 1 + (2 * vertexPerLine);
                triangles[tri+2 + (3 * ((vertexPerLine - 1)))] = vert + 1 + (2 * vertexPerLine); 

                //Right triangles
                triangles[tri + (6 * ((vertexPerLine - 1)))] = vert + (3 * vertexPerLine);
                triangles[tri+1 + (6 * ((vertexPerLine - 1)))] = vert + 1 + (3 * vertexPerLine);
                triangles[tri+2 + (6 * ((vertexPerLine - 1)))] = vert - 1 + (3 * vertexPerLine);

                tri += 3;
            }
            vert++;
        }
        //Set mesh values
        UpdateMeshSeam(mesh, vertices, normals, uvs, triangles);
    }

    //Updates given mesh with the input verts, uvs, and triangles
    void UpdateMesh(Mesh mesh, Vector3[] vertices, Vector2[] uvs, int[] triangles){
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
    }

    //Updates given mesh with the input verts, normals,  uvs, and triangles (used for seam to avoid normal artifacts, may remove)
    void UpdateMeshSeam(Mesh mesh, Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] triangles){
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.uv = uvs;
    }
}

// Based on procedural mesh tutorial at 
//https://www.youtube.com/watch?v=ekScy_oQABY 
//& https://www.youtube.com/watch?v=dc8LjeaL3k4 (inspired by catlikecoding)