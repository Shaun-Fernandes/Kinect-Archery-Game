using UnityEngine;
using System.Collections;

public class HeadMover : MonoBehaviour 
{
	[Tooltip("The transform that needs to be followed by the avatar's head, usually the position reported by the HMD tracker.")]
	public Transform targetTransform;

	[Tooltip("The transform of the avatar's head.")]
	private Transform headTransform;

	[Tooltip("Whether the avatar's feet must stick to the ground.")]
	public bool groundedFeet = false;

	[Tooltip("The transform of the avatar's left toes, if grounding is enabled.")]
	private Transform leftToes;

	[Tooltip("The transform of the avatar's right toes, if grounding is enabled.")]
	private Transform rightToes;


	// grounder constants and variables
	//private const int raycastLayers = ~2;  // Ignore Raycast
	private const float maxFootDistanceGround = 0.02f;  // maximum distance from lower foot to the ground
	private const float maxFootDistanceTime = 0.02f; // 0.2f;  // maximum allowed time, the lower foot to be distant from the ground
	//private Transform leftFoot, rightFoot;

	private float fFootDistanceInitial = 0f;
	private float fFootDistance = 0f;
	private float fFootDistanceTime = 0f;


	void Start()
	{
		// if the target transform is not set, use the camera transform
		if (targetTransform == null && Camera.main != null) 
		{
			targetTransform = Camera.main.transform;
		}
	}


	void LateUpdate () 
	{
		// move the head and body to the target
		MoveHeadToTarget();
	}


	// moves the avatar's head to the target, and the rest of its body too
	private void MoveHeadToTarget()
	{
		if(headTransform == null)
		{
			Animator animatorComponent = GetComponent<Animator>();
			headTransform = animatorComponent ? animatorComponent.GetBoneTransform(HumanBodyBones.Head) : null;
		}

		if(!targetTransform || !headTransform)
			return;

		Transform trans = headTransform.transform;
		Vector3 posTrans = targetTransform.position;

		while(trans.parent != null)
		{
			Transform transParent = trans.parent;

			Vector3 dirParent = transParent.position - trans.position;
			posTrans += dirParent;

			trans = transParent;
		}

		if(groundedFeet)
		{
			// keep the current correction
			float fLastTgtY = posTrans.y;
			posTrans.y += fFootDistance;

			float fNewDistance = GetDistanceToGround();
			float fNewDistanceTime = Time.time;

//			Debug.Log(string.Format("PosY: {0:F2}, LastY: {1:F2},  TgrY: {2:F2}, NewDist: {3:F2}, Corr: {4:F2}, Time: {5:F2}", bodyRoot != null ? bodyRoot.position.y : transform.position.y,
//				fLastTgtY, targetPos.y, fNewDistance, fFootDistance, fNewDistanceTime));

			if(Mathf.Abs(fNewDistance) >= 0.01f && Mathf.Abs(fNewDistance - fFootDistanceInitial) >= maxFootDistanceGround)
			{
				if((fNewDistanceTime - fFootDistanceTime) >= maxFootDistanceTime)
				{
					fFootDistance += (fNewDistance - fFootDistanceInitial);
					fFootDistanceTime = fNewDistanceTime;

					posTrans.y = fLastTgtY + fFootDistance;

//					Debug.Log(string.Format("   >> change({0:F2})! - Corr: {1:F2}, LastY: {2:F2},  TgrY: {3:F2} at time {4:F2}", 
//								(fNewDistance - fFootDistanceInitial), fFootDistance, fLastTgtY, targetPos.y, fFootDistanceTime));
				}
			}
			else
			{
				fFootDistanceTime = fNewDistanceTime;
			}
		}

		// set root transform position
		if(trans)
		{
			trans.position = posTrans;
		}

//		Vector3 posDiff = targetTransform.position - headTransform.position;
//		transform.position += posDiff;

		//Debug.Log("PosTrans: " + posTrans + ", Transofrm: " + transform.position);
	}


	// returns the lower distance distance from left or right foot to the ground, or 1000f if no LF/RF transforms are found
	private float GetDistanceToGround()
	{
		if(leftToes == null && rightToes == null)
		{
			Animator animatorComponent = GetComponent<Animator>();

			if(animatorComponent)
			{
				leftToes = animatorComponent.GetBoneTransform(HumanBodyBones.LeftToes);
				rightToes = animatorComponent.GetBoneTransform(HumanBodyBones.RightToes);
			}
		}

		float fDistMin = 1000f;
		float fDistLeft = leftToes ? GetTransformDistanceToGround(leftToes) : fDistMin;
		float fDistRight = rightToes ? GetTransformDistanceToGround(rightToes) : fDistMin;
		fDistMin = Mathf.Abs(fDistLeft) < Mathf.Abs(fDistRight) ? fDistLeft : fDistRight;

		if(fDistMin == 1000f)
		{
			fDistMin = 0f; // fFootDistanceInitial;
		}

//		Debug.Log (string.Format ("LFootY: {0:F2}, Dist: {1:F2}, RFootY: {2:F2}, Dist: {3:F2}, Min: {4:F2}", leftToes ? leftToes.position.y : 0f, fDistLeft,
//						rightToes ? rightToes.position.y : 0f, fDistRight, fDistMin));

		return fDistMin;
	}


	// returns distance from the given transform to the underlying object.
	private float GetTransformDistanceToGround(Transform trans)
	{
		if(!trans)
			return 0f;

//		RaycastHit hit;
//		if(Physics.Raycast(trans.position, Vector3.down, out hit, 2f, raycastLayers))
//		{
//			return -hit.distance;
//		}
//		else if(Physics.Raycast(trans.position, Vector3.up, out hit, 2f, raycastLayers))
//		{
//			return hit.distance;
//		}
//		else
//		{
//			if (trans.position.y < 0)
//				return -trans.position.y;
//			else
//				return 1000f;
//		}

		return -trans.position.y;
	}

}
