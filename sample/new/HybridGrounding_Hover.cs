using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public partial class HybridGrounding
{
	private struct ReducedGrounding
	{
		public struct Contact
		{
			public Collider collider;
			public Vector3 point;
		}

		public bool any;
		public float distance;
		public Contact[] contacts;

		public static ReducedGrounding None = new() 
		{
			any = false,
			distance = 0,
			contacts = null
		};
	}

	[SerializeField] private float hoverDistance;
	[SerializeField] private float groundMagnetismDistance;
	[SerializeField] private float maxSlopeAngle = 45;
	[SerializeField] private float castRadius = 0.2f;
	[SerializeField] Vector3 drive;

	public bool IsGrounded { get; private set; }

	private new Rigidbody rigidbody;
	private Collider[] collidersToIgnore;

	private float previousDistance = 0;
	private void Awake()
	{
		rigidbody = GetComponent<Rigidbody>();
		collidersToIgnore = GetComponentsInChildren<Collider>();
	}

	private void FixedUpdate()
	{
		rigidbody.useGravity = false; // We'll be handling gravity ourself

		var grounding = GetReducedGrounding();

		IsGrounded = grounding.any;

		Vector3 hoverForce;
		if(grounding.any)
		{	
			float velocity = (grounding.distance - previousDistance) / Time.fixedDeltaTime;

			float force = -(grounding.distance) * drive.x - velocity * drive.y;
			force = Mathf.Clamp(force, -drive.z, drive.z);
			hoverForce = Vector3.up * force;
			previousDistance = grounding.distance;
		} else
		{
			previousDistance = groundMagnetismDistance;
			hoverForce = Vector3.zero;
		}

		rigidbody.AddForce(hoverForce, ForceMode.Force);

		bool applyGravity = !grounding.any;

		if(applyGravity)
			rigidbody.AddForce(Physics.gravity, ForceMode.Acceleration);

		if(grounding.contacts == null || grounding.contacts.Length == 0)
			return;

		Vector3 counter = -hoverForce;

		if(!applyGravity)
			counter += Physics.gravity * rigidbody.mass;

		for(int i = 0; i < grounding.contacts.Length; i++)
		{
			var contact = grounding.contacts[i];
			Vector3 fragment = counter / grounding.contacts.Length;
			
			if(contact.collider.attachedRigidbody != null)
				contact.collider.attachedRigidbody.AddForceAtPosition(fragment, contact.point);
			if(contact.collider.attachedArticulationBody != null)
				contact.collider.attachedArticulationBody.AddForceAtPosition(fragment, contact.point);
		}

	}


	private ReducedGrounding GetReducedGrounding()
	{
		// This makes the assumption that the character CANNOT rotate on the X or Z axis
		Vector3 origin = transform.position + Vector3.up * (hoverDistance + castRadius);
		Vector3 direction = Vector3.down;
		float maxDistance = hoverDistance + groundMagnetismDistance;
	
		var infos = GroundUtility.SphereCast(origin, castRadius, direction, maxDistance, collidersToIgnore);

		if(infos.Length == 0)
			return ReducedGrounding.None;

		bool any = false;
		float shortestDistance = float.MaxValue;

		var contacts = new List<ReducedGrounding.Contact>(infos.Length);

		for(int i = 0; i < infos.Length; i++)
		{
			var info = infos[i];
			float distance = info.distance - hoverDistance;	

			bool tooSteep = Vector3.Angle(Vector3.up, info.surfaceNormal) > maxSlopeAngle;

			if(tooSteep)
				continue;

			contacts.Add(new()
			{
				collider = info.collider,
				point = info.contactPoint
			});

			any = true;
			shortestDistance = Mathf.Min(shortestDistance, distance);
		}

		return new()
		{
			any = any,
			distance = shortestDistance,
			contacts = contacts.ToArray()
		};

	}
}
