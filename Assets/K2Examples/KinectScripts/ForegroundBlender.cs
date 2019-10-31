using UnityEngine;
using System.Collections;

public class ForegroundBlender : MonoBehaviour 
{
	[Tooltip("Background texture that will be rendered 'behind' the detected users.")]
	public Texture backgroundTexture;

	[Tooltip("Whether to flip the background texture on X.")]
	public bool flipTextureX = false;

	[Tooltip("Whether to flip the background texture on Y.")]
	public bool flipTextureY = false;

	[Tooltip("Whether to swap the background and foreground.")]
	public bool swapTextures = false;

	private Material foregroundBlendMat;
	private KinectManager kinectManager;
	private BackgroundRemovalManager backManager;
	private long lastDepthFrameTime;


	// The single instance of ForegroundBlender
	private static ForegroundBlender instance;


	/// <summary>
	/// Gets the single ForegroundBlender instance.
	/// </summary>
	/// <value>The ForegroundBlender instance.</value>
	public static ForegroundBlender Instance
	{
		get
		{
			return instance;
		}
	}


	void Awake()
	{
		instance = this;
	}


	void Start () 
	{
		kinectManager = KinectManager.Instance;

		if(kinectManager && kinectManager.IsInitialized())
		{
			if(!backgroundTexture)
			{
				// by default get the color texture
				backgroundTexture = kinectManager.GetUsersClrTex();
			}

			Shader foregoundBlendShader = Shader.Find("Custom/ForegroundBlendShader");
			if(foregoundBlendShader != null)
			{
				foregroundBlendMat = new Material(foregoundBlendShader);

				foregroundBlendMat.SetInt("_ColorFlipH", flipTextureX ? 1 : 0);
				foregroundBlendMat.SetInt("_ColorFlipV", flipTextureY ? 1 : 0);
				foregroundBlendMat.SetInt("_SwapTextures", swapTextures ? 1 : 0);

				foregroundBlendMat.SetTexture("_ColorTex", backgroundTexture);
			}
		}
	}

	void OnDestroy()
	{
	}

	void Update () 
	{
		if(foregroundBlendMat && backgroundTexture && 
			kinectManager && kinectManager.IsInitialized())
		{
			if (!backManager) 
			{
				backManager = BackgroundRemovalManager.Instance;
			}

			Texture alphaBodyTex = backManager ? backManager.GetAlphaBodyTex () : null;
			KinectInterop.SensorData sensorData = kinectManager.GetSensorData();

			if(backManager && backManager.IsBackgroundRemovalInitialized() && 
				alphaBodyTex && backgroundTexture && lastDepthFrameTime != sensorData.lastDepthFrameTime)
			{
				lastDepthFrameTime = sensorData.lastDepthFrameTime;
				foregroundBlendMat.SetTexture("_BodyTex", alphaBodyTex);
			}
		}
	}

	void OnRenderImage (RenderTexture source, RenderTexture destination)
	{
		if(foregroundBlendMat != null)
		{
			Graphics.Blit(source, destination, foregroundBlendMat);
		}
	}

}
