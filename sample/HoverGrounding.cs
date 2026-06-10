using System;
using System.Collections.Generic;
using UnityEngine;

namespace HybridOverlapGrounding
{
	[RequireComponent(typeof(Rigidbody))]
    public class HoverGrounding : MonoBehaviour
    {
		[Serializable]
		private struct Drive
		{
			[Min(0)] public float spring;
			[Min(0)] public float damper;
			[Min(0)] public float max;
		}

		private new Rigidbody rigidbody;

		[SerializeField] private Drive drive;
		[SerializeField] private float desiredDistanceToGround = 0.2f;
		[SerializeField, Min(0)] private float groundMagnetismDistance = 0.1f;
		[SerializeField] private float groundedThreshold = 0.05f;
		[SerializeField] private float maxSlopeAngle = 45;
		[SerializeField] private float simulatedMass = 50;
		[SerializeField] private float offset;
		[SerializeField] private float radius = 0.2f;
		[SerializeField] private Collider[] collidersToIgnore;
		[SerializeField] private bool useGravity;

		private void Awake()
		{
			rigidbody = GetComponent<Rigidbody>();
		}
		public bool IsGrounded { get; private set; }


		private float previousDistanceToGround;
		private bool previousAny;
		private void FixedUpdate()
		{
			rigidbody.useGravity = false; // We'll be handling gravity ourself

			ReducedGrounding grounding = GetGrounding();

			float force;

			if (grounding.any && !grounding.tooSteep)
			{
				float clampedDistance = Mathf.Min(grounding.distance, groundMagnetismDistance);
				float clampedPreviousDistance = Mathf.Min(previousDistanceToGround, groundMagnetismDistance);

				float velocity = previousAny ?
					(clampedDistance - clampedPreviousDistance) / Time.fixedDeltaTime
					: 0;
				float normalizedVelocity = velocity / desiredDistanceToGround;

				force = -(clampedDistance / desiredDistanceToGround) * drive.spring - normalizedVelocity * drive.damper;
				force = Mathf.Clamp(force, -drive.max, drive.max);
			}
			else
				force = 0;

			rigidbody.AddForce(Vector3.up * force, ForceMode.Force);

			IsGrounded = grounding.any && grounding.distance <= groundedThreshold && !grounding.tooSteep;
			previousDistanceToGround = grounding.distance;
			previousAny = grounding.any;

			if (!IsGrounded && useGravity)
				rigidbody.AddForce(Physics.gravity * simulatedMass, ForceMode.Force);

			if (grounding.contacts == null || grounding.contacts.Length == 0)
				return;

			Vector3 counter = Vector3.down * force;

			if (IsGrounded && useGravity)
				counter += Physics.gravity * simulatedMass; // Emulate pushing down on the ground

			for (int i = 0; i < grounding.contacts.Length; i++)
			{
				Vector3 fragment = counter / grounding.contacts.Length;

				if (grounding.contacts[i].rigidbody == null)
					continue;

				grounding.contacts[i].rigidbody.AddForceAtPosition(fragment, grounding.contacts[i].point, ForceMode.Force);
			}
		}

		private struct ReducedGrounding
		{
			public bool any;
			public float distance;
			public bool tooSteep;
			public (Rigidbody rigidbody, Vector3 point)[] contacts;
		}

		private ReducedGrounding GetGrounding()
		{
			Vector3 origin = rigidbody.position + rigidbody.rotation * Vector3.up * (offset + radius);
			Vector3 direction = rigidbody.rotation * Vector3.down;
			float maxDistance = desiredDistanceToGround * 2;

			var infos = Grounding.ForSphere(origin, radius, direction, maxDistance, collidersToIgnore);

			if (infos.Length == 0)
				return new()
				{
					any = false,
					distance = 0,
					tooSteep = true,
					contacts = null
				};

			List<(Rigidbody rigidbody, Vector3 point)> contacts = new(infos.Length);

			float distance = desiredDistanceToGround;
			bool allTooSteep = true;
			
			for (int i = 0; i < infos.Length; i++)
			{
				var info = infos[i];
				float d = info.distance - desiredDistanceToGround;

				distance = Mathf.Min(d, distance);

				if (d <= groundedThreshold)
					contacts.Add((info.collider.attachedRigidbody, info.contactPoint));

				bool tooSteep = Vector3.Angle(Vector3.up, info.surfaceNormal) > maxSlopeAngle;

				if (d <= groundedThreshold && !tooSteep)
					allTooSteep = false;
			}

			return new()
			{
				any = true,
				contacts = contacts.ToArray(),
				tooSteep = allTooSteep,
				distance = distance
			};

		}


		private void OnDrawGizmos()
		{
			Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.Translate(Vector3.up * (offset + radius));

			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(Vector3.zero, radius);

			Gizmos.color = Color.brown;
			Gizmos.DrawWireSphere(Vector3.down * desiredDistanceToGround, radius);

			Gizmos.color = Color.white;
			Gizmos.DrawWireSphere(Vector3.down * (desiredDistanceToGround + groundMagnetismDistance), radius);

		}
	}
}
