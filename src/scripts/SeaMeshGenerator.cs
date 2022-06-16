using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent (typeof(MeshFilter), typeof(MeshRenderer), typeof(Material))]
public class SeaMeshGenerator : MonoBehaviour
{

    private Environment environment;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    public Material seaMaterial;
    
    //Mesh values
    Mesh mesh;
    Vector3[] vertices;
    Vector2[] uvs;
    int[] triangles;

    //Grid Settings
    public Vector3 gridOffset;
    const int resolution = 252;
    [Range (0,16)]
    public int LOD;

    public float gridSize = 10.0f;

    private float cellSize;


    // Start is called before the first frame update
    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
    }

    void Start() {
        this.meshFilter = GetComponent<MeshFilter>();
        this.meshRenderer = GetComponent<MeshRenderer>();
        cellSize = ((float)gridSize / (float)resolution);
        MakeProceduralGrid();
        UpdateMesh();
    }

    void MakeProceduralGrid(){
        //Determine LOD level
        int[] levelOfDetailIncrementValues = new int[] {1,2,3,4,6,7,9,12,14,18,21,28,36,42,63,84,126};
        int meshLODIncrement = levelOfDetailIncrementValues[LOD];
        int vertexPerLine = (resolution / meshLODIncrement) + 1;

        //Set array sizes
        vertices = new Vector3[vertexPerLine * vertexPerLine];
        uvs = new Vector2[vertexPerLine * vertexPerLine];
        triangles = new int[6 * (vertexPerLine - 1) * (vertexPerLine - 1)];

        //Set vertex offset (ensures centered grid)
        float vertexOffset = gridSize * 0.5f;

        //Populate vertices and triangles arrays
        int vert = 0;
        int tri = 0;

        //Define Vertices
        for(int x = 0; x <= resolution; x += meshLODIncrement){
            for (int z = 0; z <= resolution; z += meshLODIncrement){
                vertices[vert] = new Vector3(((x * cellSize) - vertexOffset) + gridOffset.x, gridOffset.y, ((z * cellSize) - vertexOffset) + gridOffset.z);
                uvs[vert] = new Vector2((x / (float)resolution),(z / (float)resolution));

                if(x < resolution && z < resolution){
                    triangles[tri] = vert;
                    triangles[tri+1] = triangles[tri+4] = vert+1;
                    triangles[tri+2] = triangles[tri+3] = vert + vertexPerLine;
                    triangles[tri+5] = vert + vertexPerLine + 1;
                    tri+=6;
                }
                vert++;
            }
        }
    }
    void UpdateMesh(){
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        // Set Material
        meshRenderer.material = seaMaterial;
    }
}

// Based on procedural mesh tutorial at https://www.youtube.com/watch?v=ekScy_oQABY & https://www.youtube.com/watch?v=dc8LjeaL3k4 (inspired by catlikecoding)
// LOD method based on https://www.youtube.com/watch?v=417kJGPKwDg&list=PLFt_AvWsXl0eBW2EiBtl_sxmDtSgZBxB3&index=6