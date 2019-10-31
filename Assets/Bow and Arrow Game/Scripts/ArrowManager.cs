using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ArrowManager : MonoBehaviour {

    public static ArrowManager Instance;

	public static int score = 0;
	public int playerIndex = 0;
	public float TrajectoryAimingTimeInterval = 0.05f;
	private Int64 UserID;
	public GameObject arrowPrefab;
	public Text textBox;

	public GameObject goldenBow;
    public GameObject bowString;
    public GameObject stringAttachPoint;
    public GameObject stringStartPoint;

	public GameObject leftHand;
	public GameObject rightHand;
    public GameObject HandAttachPoint;

	private KinectInterop.HandState rightHandState;
	private KinectManager kinectManager;
	private Quaternion initialRotation = Quaternion.identity;
    private GameObject currentArrow;
	private LineRenderer lr;
	//private Rigidbody rb;
	private bool isAttached = false;
	private bool isPulled = false;

	//checking for closed fist
	//if(rightHandState == KinectInterop.HandState.Closed || rightHandState == KinectInterop.HandState.Unknown || rightHandState == KinectInterop.HandState.Lasso)

	//checking for open palm
	//if(rightHandState == KinectInterop.HandState.Open)

	private void Awake()
	{
		lr = GetComponent<LineRenderer>();
		score = 0;
		//creating instance of self
		if (Instance == null)
        {
            Instance = this;
		}
	}

	private void OnDestroy()
    {
        if(Instance == this)
        {
            Instance = null;
        }
    }
	
	// Update is called once per frame
	void Update ()
	{
		UpdateHandState();					//Chceks for fist or open palm
		AttachArrowToHand();				//create new arrow if necwssary and attach to hand
		//AttachArrowToBow()				//Arrow class will call this
		PullString();						//Pull the string if hand is in fist
		Fire();                             //Fire the arrow if hand releases
		textBox.text = score.ToString();
	}

	void UpdateHandState()
	{
		if (kinectManager)
		{
			rightHandState = kinectManager.GetRightHandState(UserID);
			if (UserID != kinectManager.GetUserIdByIndex(playerIndex))
			{
				UserID = kinectManager.GetUserIdByIndex(playerIndex);
				Debug.LogError(UserID);
			}
			//Debug.LogError("hand state updated: ");
			//Debug.LogError(rightHandState);
		}
		else
		{
			kinectManager = KinectManager.Instance;
			Debug.LogError("kinect manager found");
		}

	}

	//create a new arrow once the previous one was fired, and place it in hand
	void AttachArrowToHand()
	{
		if(currentArrow == null && arrowPrefab)
		{
			currentArrow = Instantiate(arrowPrefab) as GameObject ;
			currentArrow.transform.parent = rightHand.transform;
            currentArrow.transform.localPosition = HandAttachPoint.transform.localPosition;
            currentArrow.transform.localRotation = HandAttachPoint.transform.localRotation;
			currentArrow.transform.localScale = HandAttachPoint.transform.localScale;
        }
    }
	
	//This function is called by the Arrow class uppon the arrow entering the bows colider
    public void AttachArrowToBow()
    {
		if(!isAttached && currentArrow && bowString && stringAttachPoint)
		{
        	currentArrow.transform.parent = bowString.transform;
        	//currentArrow.transform.position = stringAttachPoint.transform.position;
        	currentArrow.transform.localPosition = stringAttachPoint.transform.localPosition;
        	currentArrow.transform.rotation = stringAttachPoint.transform.rotation;
        	isAttached = true;
		}
    }

	public void PullString()
	{
		if (isAttached && IsFist() )
		{
			float dist = (stringStartPoint.transform.position - rightHand.transform.position).magnitude;
			DrawAimingTrajectory(currentArrow.transform.forward * 30f * dist);
			bowString.transform.localPosition = stringStartPoint.transform.localPosition + new Vector3(dist * 5,0f,0f);
			//********* New Script to test!!! ***********
			float oldZ = goldenBow.transform.localEulerAngles.z;
			goldenBow.transform.up = rightHand.transform.position - leftHand.transform.position;
			//goldenBow.transform.localEulerAngles = new Vector3(goldenBow.transform.eulerAngles.x, goldenBow.transform.localEulerAngles.y, oldZ);
			//Debug.LogError("String pulled");
			isPulled = true;
		}
	}

	private void Fire()
	{
		if (isPulled && IsOpen() )
		{
			float dist = (stringStartPoint.transform.position - rightHand.transform.position).magnitude;
			currentArrow.transform.parent = null;
			currentArrow.GetComponent<Arrow>().FireArrow();
			Rigidbody rb = currentArrow.GetComponent<Rigidbody>();
			rb.velocity = currentArrow.transform.forward * 30f * dist;
			rb.useGravity = true;

			//reseting all variables for next arrow
			bowString.transform.localPosition = stringStartPoint.transform.localPosition;
			currentArrow = null;
			isAttached = false;
			isPulled = false;
		}
	}

	private void DrawAimingTrajectory(Vector3 startVelocity)
	{
		Vector3 startPosition = currentArrow.transform.position;
		float m_Gravity = Physics.gravity.y;
		int noOfPositions = 40;
		float distance = 0;
		lr.positionCount = noOfPositions + 1;
		lr.SetPosition(0, currentArrow.transform.position);
		//var res = rb.CalculateMovement(noOfPositions, 2, startVelocity);

		for (int i = 0; i < noOfPositions; i++)
		{
			float currentTime = TrajectoryAimingTimeInterval * i;
			Vector3 newPos = ProjectileHelper.ComputePositionAtTimeAhead(startPosition, startVelocity, m_Gravity, currentTime);
			//distance += Vector3.Distance(lr.GetPosition(i - 1), newPos);
			lr.SetPosition(i + 1, newPos);
			//lr.SetPosition(i+1, res[i]);
		}
		//lr.materials[0].mainTextureScale = new Vector3(distance, 1, 1);
	}

	private bool IsFist()
	{
		Debug.LogError("Hand is Fist");
		return (rightHandState == KinectInterop.HandState.Closed || rightHandState == KinectInterop.HandState.Unknown || rightHandState == KinectInterop.HandState.Lasso);
	}

	private bool IsOpen()
	{
		return (rightHandState == KinectInterop.HandState.Open);
	}

	private IEnumerator Wait(){
        yield return new WaitForSeconds(5);								
	}
}
