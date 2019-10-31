using UnityEngine;
//using Windows.Kinect;

using System;
using System.Collections;

public class PointmanController : MonoBehaviour 
{
	[Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
	public int playerIndex = 0;

	[Tooltip("Whether the pointman is allowed to move vertically or not.")]
	public bool verticalMovement = true;

	[Tooltip("Whether the pointman is facing the player or not.")]
	public bool mirroredMovement = false;

	[Tooltip("Rate at which the pointman will move through the scene.")]
	public float moveRate = 1f;

	[Tooltip("Kinect origin position.")]
	public Vector3 originPosition;

	[Tooltip("Whether the z-movement is inverted or not.")]
	public bool invertedZMovement = false;
	
	[Tooltip("Camera that may be used to overlay the mesh over the color background.")]
	public Camera foregroundCamera;

	public GameObject bodyJoint;
	public GameObject skeletonLine;

	private GameObject[] bones;
	private GameObject[] lines;

	private Vector3 initialPosition;
	private Quaternion initialRotation;
	private Vector3 initialPosOffset = Vector3.zero;
	private Int64 initialPosUserID = 0;
	private bool initialPosSetY = true;

//	private readonly int[] ColliderJoints = { 
//		(int)KinectInterop.JointType.Head, 
//		(int)KinectInterop.JointType.HandLeft, (int)KinectInterop.JointType.HandRight, 
//		(int)KinectInterop.JointType.HandTipLeft, (int)KinectInterop.JointType.HandTipRight,
//		(int)KinectInterop.JointType.FootLeft, (int)KinectInterop.JointType.FootRight
//	};


	/// <summary>
	/// Gets the bone transform.
	/// </summary>
	/// <returns>The bone transform.</returns>
	/// <param name="index">Index.</param>
	public Transform GetBoneTransform(int index)
	{
		if(bones != null && index >= 0 && index < bones.Length && bones[index] != null)
		{
			return bones[index].transform;
		}

		return null;
	}

	
	void Start () 
	{
		//store bones in a list for easier access
		bones = new GameObject[KinectInterop.Constants.MaxJointCount];
		
		// array holding the skeleton lines
		lines = new GameObject[bones.Length];
		
		initialPosition = transform.position;
		initialRotation = transform.rotation;
		//transform.rotation = Quaternion.identity;
	}
	

	void Update () 
	{
		KinectManager manager = KinectManager.Instance;
		
		// get 1st player
		Int64 userID = manager ? manager.GetUserIdByIndex(playerIndex) : 0;
		
		if(userID <= 0)
		{
			initialPosUserID = 0;
			initialPosOffset = Vector3.zero;
			initialPosSetY = true;

			// reset the pointman position and rotation
			if(transform.position != initialPosition)
			{
				transform.position = initialPosition;
			}
			
			if(transform.rotation != initialRotation)
			{
				transform.rotation = initialRotation;
			}

			for(int i = 0; i < bones.Length; i++) 
			{
				if(bones[i] != null)
				{
					bones[i].gameObject.SetActive(false);
					
					bones[i].transform.localPosition = Vector3.zero;
					bones[i].transform.localRotation = Quaternion.identity;
				}

				if(lines[i] != null)
				{
					lines[i].gameObject.SetActive(false);
				}
			}

			return;
		}
		
		// set the position in space
		Vector3 posPointMan = GetJointPosition(manager, userID, (int)KinectInterop.JointType.SpineBase);

		Vector3 posPointManWorld = new Vector3(posPointMan.x, posPointMan.y, invertedZMovement ? -posPointMan.z : posPointMan.z) + originPosition;
		Vector3 posPointManHips = new Vector3(posPointMan.x, posPointMan.y, !mirroredMovement ? -posPointMan.z : posPointMan.z) + originPosition;

		// store the initial position
		if(initialPosUserID != userID)
		{
			initialPosUserID = userID;
			initialPosOffset = posPointManWorld;
		}
		
		if(!verticalMovement && !initialPosSetY)
		{
			float fFootPosY = 0f;
			if(manager.IsJointTracked(userID, (int)KinectInterop.JointType.FootLeft))
			{
				fFootPosY = GetJointPosition(manager, userID, (int)KinectInterop.JointType.FootLeft).y;
			}
			else if(manager.IsJointTracked(userID, (int)KinectInterop.JointType.FootRight))
			{
				fFootPosY = GetJointPosition(manager, userID, (int)KinectInterop.JointType.FootRight).y;
			}
			
			initialPosOffset.y = posPointManWorld.y - (fFootPosY + originPosition.y);
			initialPosSetY = true;
		}

		Vector3 relPosUser = (posPointManWorld - initialPosOffset);
		//relPosUser.z = invertedZMovement ? -relPosUser.z : relPosUser.z;

		transform.position = initialPosOffset + 
			(verticalMovement ? relPosUser * moveRate : new Vector3(relPosUser.x, 0, relPosUser.z) * moveRate);

		if(manager.IsJointTracked(userID, (int)KinectInterop.JointType.Head))
		{
//			float fHeadPosY = GetJointPosition(manager, userID, (int)KinectInterop.JointType.Head).y + originPosition.y;
//			float halfHeight = Mathf.Abs(fHeadPosY - posPointManWorld.y);
//
//			CapsuleCollider collider = GetComponent<CapsuleCollider>();
//			if(collider != null)
//			{
//				collider.height = 2f * halfHeight;
//			}
		}

		// update the local positions of the bones
		for(int i = 0; i < bones.Length; i++) 
		{
			if(bones[i] == null && bodyJoint != null)
			{
				bones[i] = Instantiate(bodyJoint) as GameObject;
				bones[i].transform.parent = transform;
				bones[i].name = ((KinectInterop.JointType)i).ToString();

//				if(Array.IndexOf(ColliderJoints, i) >= 0)
//				{
//					bones[i].GetComponent<SphereCollider>().radius = 1f;
//				}
			}

			if(bones[i] != null)
			{
				int joint = !mirroredMovement ? i : (int)KinectInterop.GetMirrorJoint((KinectInterop.JointType)i);
				if(joint < 0)
					continue;
				
				if(manager.IsJointTracked(userID, joint))
				{
					bones[i].gameObject.SetActive(true);
					
					Vector3 posJoint = GetJointPosition(manager, userID, joint);
					posJoint.z = !mirroredMovement ? -posJoint.z : posJoint.z;

					if (posJoint == Vector3.zero) 
					{
						bones[i].gameObject.SetActive(false);
						if(lines[i] != null)
						{
							lines[i].gameObject.SetActive(false);
						}

						continue;
					}

					posJoint += originPosition;
					posJoint -= posPointManHips;
					
					if(mirroredMovement)
					{
						posJoint.x = -posJoint.x;
						posJoint.z = -posJoint.z;
					}

					Quaternion rotJoint = manager.GetJointOrientation(userID, joint, !mirroredMovement);
					rotJoint = initialRotation * rotJoint;
					
					bones[i].transform.localPosition = posJoint;
					bones[i].transform.rotation = rotJoint;
					
					if(lines[i] == null && i > 0 && skeletonLine != null) 
					{
						lines[i] = Instantiate(skeletonLine) as GameObject;
						lines[i].transform.parent = transform;
						lines[i].name = ((KinectInterop.JointType)i).ToString() + "_Line";
					}

					if(lines[i] != null && i > 0)
					{
						int jParent = (int)manager.GetParentJoint((KinectInterop.JointType)joint);

						if (manager.IsJointTracked (userID, jParent)) 
						{
							lines[i].gameObject.SetActive (true);

							Vector3 posJoint2 = GetJointPosition (manager, userID, jParent);
							posJoint2.z = !mirroredMovement ? -posJoint2.z : posJoint2.z;

							if (posJoint2 == Vector3.zero) 
							{
								lines[i].gameObject.SetActive (false);
								continue;
							}

							posJoint2 += originPosition;
							posJoint2 -= posPointManHips;

							if (mirroredMovement) 
							{
								posJoint2.x = -posJoint2.x;
								posJoint2.z = -posJoint2.z;
							}

							Vector3 dirFromParent = posJoint - posJoint2;

							lines [i].transform.localPosition = posJoint2 + dirFromParent / 2f;
							lines [i].transform.up = transform.rotation * dirFromParent.normalized;

							Vector3 lineScale = lines [i].transform.localScale;
							lines [i].transform.localScale = new Vector3 (lineScale.x, dirFromParent.magnitude / 2f, lineScale.z);
						} 
						else 
						{
							lines[i].gameObject.SetActive(false);
						}
					}

				}
				else
				{
					if(bones[i] != null)
					{
						bones[i].gameObject.SetActive(false);
					}
					
					if(lines[i] != null)
					{
						lines[i].gameObject.SetActive(false);
					}
				}
			}	
		}
	}


	// returns the world- or camera-overlay joint position 
	private Vector3 GetJointPosition(KinectManager manager, long userID, int iJoint)
	{
		if (foregroundCamera) 
		{
			Rect backgroundRect = foregroundCamera.pixelRect;
			PortraitBackground portraitBack = PortraitBackground.Instance;

			if (portraitBack && portraitBack.enabled) 
			{
				backgroundRect = portraitBack.GetBackgroundRect ();
			}

			return manager.GetJointPosColorOverlay(userID, iJoint, foregroundCamera, backgroundRect);
		} 
		else
		{
			return manager.GetJointPosition(userID, iJoint);
		}
	}

}
