using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;

public struct displacedVert {
    public float height;
    public Vector3 normal;
}

[RequireComponent (typeof(ComputeShader), typeof(Material))]
public class WavePropagation : MonoBehaviour
{
    //Public Fields
    public ComputeShader computeShader;
    public Material seaMaterial;
    public float tempDampCoefficient = 1;
    public float waveSpeed = 1;
    public float deltaTime = 1;
    const int gridSize = 253;
    public float h = 2;
    public bool useComputeShader = false;

    public int dropCount = 1;
    public bool makeLineDisturbance = false;
    public float dispAmount = 0.1f;

    // public Texture2D heightMapTex;


    //Private Fields
    private int lineCounter = 0;
    private float A;
    private float B;
    private displacedVert[,] previousField;
    private displacedVert[,] currentField;
    private displacedVert[,] nextField;
    private ComputeBuffer currentHeightBuffer;
    private ComputeBuffer previousHeightBuffer;
    private ComputeBuffer nextHeightBuffer;
    private Shader waterShader;
    
    //Tempory start, using the given values for wavespeed, timestep, and h calculates the coefficients and instantiates the 'heightfield'
    void Start() {
        Time.timeScale = 0.5f;
        A = ((waveSpeed * waveSpeed) * (deltaTime * deltaTime) * (1.0f / (h * h)));
        B = 2 - 4*A;

        //so the code compiles (heightmaps, some kind of texture?)
        previousField = new displacedVert[gridSize,gridSize];
        initNormals(previousField);
        currentField = new displacedVert[gridSize,gridSize];
        initNormals(currentField);
        nextField = new displacedVert[gridSize,gridSize];
        initNormals(nextField);
        // //Adding a disturbance in the middle of the heightfield (how to do properly?)
        // disturbMesh(gridSize/2, gridSize/2);
        // disturbMesh(gridSize/4, gridSize/4);
        // disturbMesh(3*gridSize/4, 3*gridSize/4);
        // printContents(currentField);

        //Use current height buffer either way
        currentHeightBuffer = new ComputeBuffer(gridSize*gridSize, sizeof (float) * 4);
        currentHeightBuffer.SetData(currentField);
        
        //Set Water Shader values
        seaMaterial.SetInt("gridSize", gridSize);
        seaMaterial.SetBuffer("currentHeightBuffer", currentHeightBuffer);

        //Compute Shader Initialization code
        if(useComputeShader){
            //Instantiate previous height buffer
            previousHeightBuffer = new ComputeBuffer(gridSize*gridSize, sizeof (float) * 4);
            previousHeightBuffer.SetData(previousField);
            computeShader.SetBuffer(0, "previousHeightBuffer", previousHeightBuffer);
            //Instantiate current height buffer
            computeShader.SetBuffer(0, "currentHeightBuffer", currentHeightBuffer);
            //Instantiate next height buffer
            nextHeightBuffer = new ComputeBuffer(gridSize*gridSize, sizeof (float) * 4);
            nextHeightBuffer.SetData(nextField);
            computeShader.SetBuffer(0, "nextHeightBuffer", nextHeightBuffer);
            //Set coefficients and gridsize values
            computeShader.SetFloat("A", A);
            computeShader.SetFloat("B", B);
            computeShader.SetFloat("h", h);
            computeShader.SetFloat("tempDampCoefficient", tempDampCoefficient);
            computeShader.SetInt("gridSize", gridSize);
        }
        // heightMapTex = TextureGenerator.TextureFromHeightMap(previousField, currentField, nextField, gridSize);
    }
    void OnApplicationQuit() {
        currentHeightBuffer.Release();
        //Release compute buffers if compute shader was used
        if(previousHeightBuffer != null){
            previousHeightBuffer.Release();
            nextHeightBuffer.Release();
        }
    }

   //Adds correct initial normals
    void initNormals(displacedVert[,] field){
        for(int i = 0; i < gridSize; i++){
            for (int j = 0; j < gridSize; j++){
                field[i,j].normal = Vector3.up;
            }
        }
    }

    //Prints contents of the 2D array in the console (for debug purposes)
    void printContents(displacedVert[,] field){
        StringBuilder sb = new StringBuilder();
        for(int i = 0; i < gridSize; i++){
            for (int j = 0; j < gridSize; j++){
                sb.Append(field[i,j].height);
                sb.Append(field[i,j].normal);
                sb.Append(' ');	
            }
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    //Frame-rate independent update
    private void FixedUpdate() {
        for(int i = 0; i < dropCount; i++){
            disturbMesh((int)((gridSize - 1) * UnityEngine.Random.value), (int)((gridSize - 1) * UnityEngine.Random.value));
        }

        if(makeLineDisturbance){
            if(lineCounter < gridSize/2){
                disturbMesh((gridSize/4) + lineCounter,(gridSize/4) + lineCounter);
                lineCounter++;
            }
        } else {
            lineCounter = 0;
        }

        if(useComputeShader){
            //Calculate number of threads to ask for (not sure about the best way to do this)
            int numThreads = Mathf.Max(gridSize, gridSize/32);
            //Dispatch compute shader
            computeShader.Dispatch(0, numThreads, numThreads, 1);
            //Read calculated nextfield data
            nextHeightBuffer.GetData(nextField);
            //Set arrays for next computation step
            previousField = currentField.Clone() as displacedVert[,];
            currentField = nextField.Clone() as displacedVert[,];
            //Set buffer values for next computation step
            previousHeightBuffer.SetData(previousField);
            currentHeightBuffer.SetData(currentField);

            // printContents(currentField);
        }else{
            generateWaveHeightField();

            // printContents(currentField);
        }
        // TextureGenerator.UpdateTextureColors(previousField, currentField, nextField, gridSize, heightMapTex);
        // Debug.Log(heightMapTex.GetPixel(gridSize/2, gridSize/2).ToString());
    }

    void generateWaveHeightField() {
        //For loop version of wave value calculation
        for(int i = 1; i <  gridSize - 1; i++){
            for (int j = 1; j < gridSize - 1; j++){
                nextField[i, j].height = (A * (currentField[i-1, j].height + currentField[i+1, j].height + currentField[i, j-1].height + currentField[i, j+1].height)) 
                               + ((B * currentField[i,j].height) - previousField[i,j].height);
                //Basic wave damping
                nextField[i,j].height *= tempDampCoefficient;

                //Normal Calculation
                float xheight = (float)(currentField[i-1, j].height - currentField[i+1, j].height);
                float zheight = (float)(currentField[i, j-1].height - currentField[i, j+1].height);
                nextField[i,j].normal = new Vector3(xheight,(2.0f * h),zheight);
            }
        }
        previousField = currentField.Clone() as displacedVert[,];
        currentField = nextField.Clone() as displacedVert[,];
        currentHeightBuffer.SetData(currentField);
    }

    //Temp method to add a basic disturbance in the water
    void disturbMesh(int x, int y){
        if(x==0 || y == 0 || x == gridSize || y == gridSize) return;
        if(useComputeShader){
            currentField[x,y].height += dispAmount;            
        } else {
            currentField[x,y].height -= dispAmount;
        }
    }
}

public static class TextureGenerator {
    public static Texture2D TextureFromHeightMap(displacedVert[,] previousHM, displacedVert[,] currentHM, displacedVert[,] nextHM, int gridSize){
        Texture2D heightTexture = new Texture2D (gridSize, gridSize);
        heightTexture.filterMode = FilterMode.Point;
        heightTexture.wrapMode = TextureWrapMode.Clamp;

        Color[] heightColors = new Color[gridSize * gridSize];
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                // heightColors[(y*gridSize) + x] = Color.Lerp(Color.black, Color.white, previousHM[x,y].height);
                heightColors[(y*gridSize) + x] = new Color((previousHM[x,y].height),(currentHM[x,y].height),(nextHM[x,y].height));
            }
        }

        heightTexture.SetPixels(heightColors);
        heightTexture.Apply();

        return heightTexture;
    }

    public static void UpdateTextureColors(displacedVert[,] previousHM, displacedVert[,] currentHM, displacedVert[,] nextHM, int gridSize, Texture2D heightTexture){
        Color[] heightColors = new Color[gridSize * gridSize];
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                // heightColors[(y*gridSize) + x] = Color.Lerp(Color.black, Color.white, heightMap[x,y].height);
                heightColors[(y*gridSize) + x] = new Color((previousHM[x,y].height),(currentHM[x,y].height),(nextHM[x,y].height));
            }
        }
        heightTexture.SetPixels(heightColors);
        heightTexture.Apply();
    }
}