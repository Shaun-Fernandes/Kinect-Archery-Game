using UnityEngine;
using System.Collections;
//using Windows.Kinect;


public class JointPositionView : MonoBehaviour 
{
	[Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
	public int playerIndex = 0;

	[Tooltip("The Kinect joint we want to track.")]
	public KinectInterop.JointType trackedJoint = KinectInterop.JointType.SpineBase;

	[Tooltip("Whether the movement is relative to transform's initial position, or is in absolute coordinates.")]
	public bool relToInitialPos = false;
	
	[Tooltip("Whether the z-movement is inverted or not.")]
	public bool invertedZMovement = false;

	[Tooltip("Transform offset to the Kinect-reported position.")]
	public Vector3 transformOffset = Vector3.zero;

	[Tooltip("Whether the displayed position is in Kinect coordinates, or in world coordinates.")]
	public bool useKinectSpace = false;

	//public bool moveTransform = true;

	[Tooltip("Smooth factor used for the joint position smoothing.")]
	public float smoothFactor = 5f;

	[Tooltip("UI-Text to display the current joint position.")]
	public UnityEngine.UI.Text debugText;


	private Vector3 initialPosition = Vector3.zero;
	private long currentUserId = 0;
	private Vector3 initialUserOffset = Vector3.zero;

	private Vector3 vPosJoint = Vector3.zero;


	void Start()
	{
		initialPosition = transform.position;
	}
	
	void Update () 
	{
		KinectManager manager = KinectManager.Instance;
		
		if(manager && manager.IsInitialized())
		{
			int iJointIndex = (int)trackedJoint;

			if(manager.IsUserDetected(playerIndex))
			{
				long userId = manager.GetUserIdByIndex(playerIndex);
				
				if(manager.IsJointTracked(userId, iJointIndex))
				{
					if(useKinectSpace)
						vPosJoint = manager.GetJointKinectPosition(userId, iJointIndex);
					else
						vPosJoint = manager.GetJointPosition(userId, iJointIndex);

					vPosJoint.z = invertedZMovement ? -vPosJoint.z : vPosJoint.z;
					vPosJoint += transformOffset;

					if(userId != currentUserId)
					{
						currentUserId = userId;
						initialUserOffset = vPosJoint;
					}

					Vector3 vPosObject = relToInitialPos ? initialPosition + (vPosJoint - initialUserOffset) : vPosJoint;

					if(debugText)
					{
						debugText.text = string.Format("{0} - ({1:F3}, {2:F3}, {3:F3})", trackedJoint, 
						                                                       vPosObject.x, vPosObject.y, vPosObject.z);
					}

					//if(moveTransform)
					{
						if(smoothFactor != 0f)
							transform.position = Vector3.Lerp(transform.position, vPosObject, smoothFactor * Time.deltaTime);
						else
							transform.position = vPosObject;
					}
				}
				
			}
			
		}
	}
}
