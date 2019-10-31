using System.Collections;
using System.Collections.Generic;
using Dweiss;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LineRenderer))]
public class AutoFireScript : MonoBehaviour {

	public static AutoFireScript Instance;

	public Text textBox;
	public static int score = 0;
	public GameObject goldenBow;
	public GameObject arrowPrefab;
	public GameObject bowString;
	public GameObject leftHand;
	public GameObject rightHand;
	public GameObject stringAttachPoint;
	public GameObject stringStartPoint;
	public float dist = 1.5f;
	public float TrajectoryAimingTimeInterval = 0.05f;
	public bool clickToFire;
	
	private Quaternion initialRotation = Quaternion.identity;
	private GameObject currentArrow;
	private LineRenderer lr;
	private Rigidbody rb;
	private bool isAttached = false;
	private bool isPulled = false;

	private void Awake()
	{
		lr = GetComponent<LineRenderer>();
		score = 0;
		if (!clickToFire)
		{
			Time.timeScale = 0.05f;
		}
		//creating instance of self
		if (Instance == null)
		{
			Instance = this;
		}
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	// Update is called once per frame
	void FixedUpdate()
	{
		AttachArrowToBow();					//Instantiates new arrow and attaches it to bow
		PullString();                       //Pull the string if hand is in fist
		Fire();                             //Fire the arrow if hand releases
		textBox.text = score.ToString();
	}


	public void AttachArrowToBow()
	{
		if (currentArrow == null && arrowPrefab && !isAttached && bowString && stringAttachPoint)
		{
			currentArrow = Instantiate(arrowPrefab) as GameObject;
			rb = currentArrow.GetComponent<Rigidbody>();
			currentArrow.transform.parent = bowString.transform;
			//currentArrow.transform.position = stringAttachPoint.transform.position;
			currentArrow.transform.localPosition = stringAttachPoint.transform.localPosition;
			currentArrow.transform.rotation = stringAttachPoint.transform.rotation;
			currentArrow.transform.localScale = stringAttachPoint.transform.localScale;
			currentArrow.GetComponent<Arrow>().isAttached = true;
			isAttached = true;
		}
	}

	public void PullString()
	{
		if (isAttached)
		{
			DrawAimingTrajectory(currentArrow.transform.forward * 30f * dist);
			bowString.transform.localPosition = stringStartPoint.transform.localPosition + new Vector3(dist * 5, 0f, 0f);
			//********* New Script to test!!! ***********
			//goldenBow.transform.rotation = Quaternion.LookRotation(rightHand.transform.position - leftHand.transform.position, -(leftHand.transform.forward));
			//this works.... sorta... 
			float oldZ = goldenBow.transform.localEulerAngles.z;
			goldenBow.transform.up = rightHand.transform.position - leftHand.transform.position;
			//goldenBow.transform.localEulerAngles = new Vector3(goldenBow.transform.eulerAngles.x, goldenBow.transform.localEulerAngles.y, oldZ);
			//Quaternion lookrotation = Quaternion.LookRotation(rightHand.transform.position - leftHand.transform.position);
			//goldenBow.transform.eulerAngles = new Vector3(lookrotation.x, goldenBow.transform.eulerAngles.y, lookrotation.z);
			isPulled = true;
		}
	}

	private void Fire()
	{
		if (isPulled && clickToFire && Input.GetMouseButtonDown(0))
		{
			Debug.LogError("Arrow being fired");
			currentArrow.transform.parent = null;
			currentArrow.GetComponent<Arrow>().FireArrow();
			rb.velocity = currentArrow.transform.forward * 30f * dist;
			rb.useGravity = true;

			//reseting all variables for next arrow
			bowString.transform.localPosition = stringStartPoint.transform.localPosition;
			currentArrow = null;
			isAttached = false;
			isPulled = false;
			//waiting before the next arrow can be fired
			//yield return new WaitForSeconds(1);
			//System.Threading.Thread.Sleep(1000);
		}
		else if(isPulled && !clickToFire)
		{
			Debug.LogError("Arrow being fired");
			currentArrow.transform.parent = null;
			currentArrow.GetComponent<Arrow>().FireArrow();
			Rigidbody rb = currentArrow.GetComponent<Rigidbody>();
			rb.velocity = currentArrow.transform.forward * 30f * dist;

			bowString.transform.localPosition = stringStartPoint.transform.localPosition;
			currentArrow = null;
			isAttached = false;
			isPulled = false;
			rb = null;
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

		for (int i=0; i<noOfPositions; i++)
		{
			float currentTime = TrajectoryAimingTimeInterval * i;
			Vector3 newPos = ProjectileHelper.ComputePositionAtTimeAhead(startPosition, startVelocity, m_Gravity, currentTime);
			//distance += Vector3.Distance(lr.GetPosition(i - 1), newPos);
			lr.SetPosition(i+1, newPos);
			//lr.SetPosition(i+1, res[i]);
		}
		//lr.materials[0].mainTextureScale = new Vector3(distance, 1, 1);
	}
}
