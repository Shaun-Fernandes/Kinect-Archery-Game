using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class GrenadeThrower : MonoBehaviour 
{
	[Header("Grenade Thrower")]
	public float m_GrenadeRange 				= 30.0f;
	public float m_Gravity      				= -9.80665f;
	
	[Header("Input Settings")]
	public float m_TurningSpeed 				= 90.0f;
	public float m_RangeSpeed   				= 20.0f;
	
	[Header("Aiming Trajectory Settings")]
	public int m_NumTrajectoryPositions 		= 20;
	public GameObject m_TrajectoryObject 		= null;
	public float TrajectoryAimingTimeInterval 	= 0.3f;
	public GameObject m_GrenadeObject 		  	= null;
	
	private float m_CurrentDesiredAngle = 0.0f;
	private float m_CurrentDesiredSpeed = 0.0f;
	private GameObject[] m_TrajectoryObjects;
	
	void Start () 
	{
		m_CurrentDesiredAngle = 0.0f;
		m_CurrentDesiredSpeed = 12.0f;
		m_TrajectoryObjects   = new GameObject[m_NumTrajectoryPositions];
		for (int i = 0; i < m_NumTrajectoryPositions; i++)
		{
			m_TrajectoryObjects[i] = GameObject.Instantiate(m_TrajectoryObject);
		}
	}
	
	void DrawAimingTrajectory()
	{
		Vector3 direction     = new Vector3(1.0f, 0.0f, 0.0f);
		Vector3 bankingAxis   = new Vector3(0.0f, 1.0f, 0.0f);
		direction             = Quaternion.AngleAxis(m_CurrentDesiredAngle, bankingAxis) * direction;
		Vector3 horizonAxis   = Vector3.Cross(direction, bankingAxis);
		direction             = Quaternion.AngleAxis(45.0f, horizonAxis) * direction;
		
		Vector3 startVelocity = direction * m_CurrentDesiredSpeed;
		Vector3 startPosition = new Vector3(0.0f, 0.0f, 0.0f);

		for (int i = 0; i < m_NumTrajectoryPositions; i++)
		{
			float currentTime          = TrajectoryAimingTimeInterval * i;
			Vector3 trajectoryPosition = ProjectileHelper.ComputePositionAtTimeAhead
				(startPosition, startVelocity, m_Gravity, currentTime);
			m_TrajectoryObjects[i].transform.position = trajectoryPosition;
			
			// orientate the previous object to point to the current object
			if (i > 0)
			{
				Vector3 directionToNext = m_TrajectoryObjects[i].transform.position - m_TrajectoryObjects[i-1].transform.position;
				directionToNext.Normalize();
				m_TrajectoryObjects[i-1].transform.forward = directionToNext;
			}
		}
	}
	
	void UpdateFiring()
	{
		if ( m_GrenadeObject == null)
			return;
			
		if (Input.GetButtonDown("Fire1"))
		{
			Vector3 direction     = new Vector3(1.0f, 0.0f, 0.0f);
			Vector3 bankingAxis   = new Vector3(0.0f, 1.0f, 0.0f);
			direction             = Quaternion.AngleAxis(m_CurrentDesiredAngle, bankingAxis) * direction;
			Vector3 horizonAxis   = Vector3.Cross(direction, bankingAxis);
			direction             = Quaternion.AngleAxis(45.0f, horizonAxis) * direction;
			Vector3 startVelocity = direction * m_CurrentDesiredSpeed;
		
			// spawn a grenade
			GameObject temp 		= GameObject.Instantiate(m_GrenadeObject);
			temp.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
			Grenade grenade 		= temp.GetComponent<Grenade>();
			grenade.m_Gravity 		= m_Gravity;
			grenade.m_CurrentVelocity = startVelocity;
			grenade.enabled 		= true;
		}
	}
	
	void Update () 
	{
		float timeToLand        = 0.0f;
		float calulatedMaxSpeed = ProjectileHelper.ComputeSpeedToReachMaxFlatRange(m_GrenadeRange, m_Gravity, out timeToLand);
		float horizontalInput 	= Input.GetAxis("Horizontal");
		float verticalInput   	= Input.GetAxis("Vertical");
		float dt              	= Time.deltaTime;
		m_CurrentDesiredAngle 	= Mathf.Clamp(m_CurrentDesiredAngle + horizontalInput * m_TurningSpeed * dt, -60.0f, 60.0f);
		m_CurrentDesiredSpeed 	= Mathf.Clamp(m_CurrentDesiredSpeed + verticalInput * m_RangeSpeed * dt, 0.5f, calulatedMaxSpeed);
		
		DrawAimingTrajectory();
		UpdateFiring();
	}
}
