using UnityEngine;
using System;


/// <summary>
/// Script that emulates a 3D holographic display based on the viewer position
/// Courtesy of Davy Loots (Twitter: @davloots)
/// - For best effect - and if available - use a stereoscopic display and calculate the head 
///   position twice by simply offsetting the HeadPosition .03 to the left and to the right for
///   each of the views.
/// </summary>
class SimpleHolographicCamera : MonoBehaviour
{
	[Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
	public int playerIndex = 0;

	//[Tooltip("How high above the ground is the center of the display, in meters.")]
	//public float ScreenCenterY = 0.5f;

	[Tooltip("The position of display center, in Kinect world coordinates, in meters.")]
	public Vector3 screenCenterPos = new Vector3(0f, 1f, 0f);

	[Tooltip("Width of the display in meters.")]
	public float screenWidth = 1.6f; // 0.88f;

	[Tooltip("Height of the display in meters.")]
	public float screenHeight = 0.9f; // 0.50f;

	[Tooltip("Maximum distance from the user to the display wall, in meters.")]
	public float maxUserDistance = 3f;

	[Tooltip("UI-Text to display status messages.")]
	public UnityEngine.UI.Text statusText = null;

	private float left = -0.2F;
	private float right = 0.2F;
	private float bottom = -0.2F;
	private float top = 0.2F;

	private KinectManager kinectManager;

	private Vector3 jointHeadPos;
	private Vector3 headRelPosition;
	private bool headPosValid = false;

	private Vector3 initialCamPos = Vector3.zero;
	private Quaternion initialCamRot = Quaternion.identity;
	private Matrix4x4 initialCamPM = Matrix4x4.identity;
	private Vector3 initialRelPos = Vector3.zero;


	void Start()
	{
		kinectManager = KinectManager.Instance;
		//screenCenterPos = new Vector3 (0f, ScreenCenterY, 0f);

		Camera cam = GetComponent<Camera>();
		if (cam) 
		{
			initialCamPos = cam.transform.position;
			initialCamRot = cam.transform.rotation;
			initialCamPM = cam.projectionMatrix;
		}
	}

	void Update()
	{
		headPosValid = false;

		if (kinectManager && kinectManager.IsInitialized()) 
		{
			long userId = kinectManager.GetUserIdByIndex(playerIndex);

			if (kinectManager.IsUserTracked (userId) && kinectManager.IsJointTracked (userId, (int)KinectInterop.JointType.Head)) 
			{
				jointHeadPos = kinectManager.GetJointPosition (userId, (int)KinectInterop.JointType.Head);
				headRelPosition = jointHeadPos - screenCenterPos;
				headPosValid = true;

				if (initialRelPos == Vector3.zero) 
				{
					initialRelPos = headRelPosition;
				}

				if (statusText) 
				{
					string sStatusMsg = string.Format ("Head position: {0}\nRelative to screen: {1}", jointHeadPos, headRelPosition);
					statusText.text = sStatusMsg;
				}
			}
			else 
			{
				initialRelPos = Vector3.zero;
			}
		}

	}


    /// <summary>
    /// Updates the projection matrix and camera position to get the correct anamorph perspective
    /// </summary>
    void LateUpdate()
    {
		Camera cam = GetComponent<Camera>();

		if (cam) 
		{
			if (headPosValid) 
			{
				// set off-center projection
				left = cam.nearClipPlane * (-screenWidth / 2 - headRelPosition.x) / initialRelPos.z;
				right = cam.nearClipPlane * (screenWidth / 2 - headRelPosition.x) / initialRelPos.z;

				bottom = cam.nearClipPlane * (-screenHeight / 2 - headRelPosition.y) / initialRelPos.z;
				top = cam.nearClipPlane * (screenHeight / 2 - headRelPosition.y) / initialRelPos.z;

				cam.transform.position = new Vector3(headRelPosition.x, headRelPosition.y, -headRelPosition.z);
				cam.transform.LookAt(new Vector3(headRelPosition.x, headRelPosition.y, 0));

				Matrix4x4 m = PerspectiveOffCenter(left, right, bottom, top, cam.nearClipPlane, cam.farClipPlane);
				cam.projectionMatrix = m;
			}
			else
			{
				// set the initial camera settings
				cam.transform.position = initialCamPos;
				cam.transform.rotation = initialCamRot;
				cam.projectionMatrix = initialCamPM;
			}
		}
    }

    /// <summary>
    /// Calculates the camera projection matrix
    /// </summary>
    /// <returns>The off center matrix.</returns>
    /// <param name="left">Left.</param>
    /// <param name="right">Right.</param>
    /// <param name="bottom">Bottom.</param>
    /// <param name="top">Top.</param>
    /// <param name="near">Near.</param>
    /// <param name="far">Far.</param>
    private Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
    {
        float x = 2.0F * near / (right - left);
        float y = 2.0F * near / (top - bottom);
        float a = (right + left) / (right - left);
        float b = (top + bottom) / (top - bottom);
        float c = -(far + near) / (far - near);
        float d = -(2.0F * far * near) / (far - near);
        float e = -1.0F;

        Matrix4x4 m = new Matrix4x4();
        m[0, 0] = x;
        m[0, 1] = 0;
        m[0, 2] = a;
        m[0, 3] = 0;
        m[1, 0] = 0;
        m[1, 1] = y;
        m[1, 2] = b;
        m[1, 3] = 0;
        m[2, 0] = 0;
        m[2, 1] = 0;
        m[2, 2] = c;
        m[2, 3] = d;
        m[3, 0] = 0;
        m[3, 1] = 0;
        m[3, 2] = e;
        m[3, 3] = 0;

        return m;
    }

}

