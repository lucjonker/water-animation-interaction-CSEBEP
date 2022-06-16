using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct boatPointInfo 
{
    public Vector2 direction;
    public Vector2 uv;
    public float index;
};

[RequireComponent (typeof(ComputeShader), typeof(Material) , typeof(Material))]
public class WaterEffectManager : MonoBehaviour
{
    //Public Fields
    public ComputeShader computeShader;
    public Material seaMaterial;
    public Material trimSeaMaterial;
    public int texResolution;

    [Range (1, 25)]
    public int competitorPositionsToStore;
    public RenderTexture renderTexture;

    //Private Fields
    private Environment environment;
    private Timer timer;
    private float currentTime;

    private Competitor[] competitors;
    private boatPointInfo[,] competitorBoatInfo;
    private ComputeBuffer boatPointBuffer;

    private Pose lerpedPos;
    private Vector2 UV;
    private Vector3 rot;

    private int sThreads;
    private int texThreds;

    // Initializes the water effect manager
    public void init(Environment environment){
        //Set environment vars
        this.environment = environment;
        this.timer = environment.GetTimer();
        //Define data structures
        competitors = environment.GetCompetitorComponents();
        competitorBoatInfo = new boatPointInfo[competitors.Length, competitorPositionsToStore];
        boatPointBuffer = new ComputeBuffer(competitors.Length * competitorPositionsToStore, 
                                            sizeof (float) * 5);
        //Initialize texture
        renderTexture = new RenderTexture(texResolution,texResolution, 0, RenderTextureFormat.RFloat);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        //Set compute shader values for each kernel (0 is wakegen, 1 is clear)
        computeShader.SetTexture(0, "res", renderTexture);
        computeShader.SetTexture(1, "clear", renderTexture);
        computeShader.SetFloat("texResolution", renderTexture.width);
        computeShader.SetInt("competitorPositionsToStore", competitorPositionsToStore);
        computeShader.SetInt("numCompetitors", competitors.Length);
        computeShader.SetBuffer(0, "boatPointBuffer", boatPointBuffer);
        //Set material shader values
        seaMaterial.SetTexture("_RenderTexture", renderTexture);
        trimSeaMaterial.SetTexture("_RenderTexture", renderTexture);

        //Initialize values
        lerpedPos = new Pose();
        UV = new Vector2();
        rot = new Vector3();

        sThreads = (int)Mathf.Ceil((competitorPositionsToStore * competitors.Length) / 64f);
        texThreds = texResolution/32;
    }

    // Update is called once per frame
    void FixedUpdate(){
        //Get current time
        currentTime = timer.getPlayheadTrackingOffset();
    
        UpdateBoatPointInfo();

        //Dispatch clean kernel to clear rendertexture
        computeShader.Dispatch(1, texThreds, texThreds, 1);
        //Dispatch shader kernel to add wake values
        computeShader.Dispatch(0, sThreads, 1, 1);
    }

    private void UpdateBoatPointInfo()
    {
        for (int i = 0; i < competitors.Length; i++){
            for (int j = 0; j < competitorPositionsToStore;){
                //Get position relative to current time
                lerpedPos = competitors[i].lerpCompetitorPose(currentTime - (0.3f * j));
                //Get Uvs from historic position
                UV = competitors[i].CompetitorUvsFromPose(lerpedPos);
                rot = (lerpedPos.rotation * Vector3.forward);
                //Set boat info for compute shader
                competitorBoatInfo[i,j].direction.x = rot.x;
                competitorBoatInfo[i,j].direction.y = rot.z;
                competitorBoatInfo[i,j].uv = UV;
                competitorBoatInfo[i,j].index = ++j;
            }
        }
        boatPointBuffer.SetData(competitorBoatInfo);
    }

    void OnApplicationQuit() {
        boatPointBuffer.Release();
    }
}