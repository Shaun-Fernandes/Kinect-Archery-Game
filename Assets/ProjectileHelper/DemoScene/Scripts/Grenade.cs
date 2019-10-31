using UnityEngine;
using System.Collections;

public class Grenade : MonoBehaviour 
{
	public float m_Gravity           = -9.81f;
	public Vector3 m_CurrentVelocity = new Vector3(1.0f, 0.0f, 0.0f);

	void Start () 
	{
	
	}
	
	void Update () 
	{
		float dt            = Time.deltaTime;
		Vector3 position    = transform.position;
		ProjectileHelper.UpdateProjectile(ref position, ref m_CurrentVelocity, m_Gravity, dt);
		transform.position  = position;
		
		if (position.y < 0.0f)
		{
			GameObject.Destroy(this.gameObject);
		}
	}
}
