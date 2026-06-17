using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Collections;
using System.Linq;

public partial class HybridGrounding
{
	[SerializeField] private Collider overlapCollider;
	
   	private void Start()
   	{
		overlapCollider.hasModifiableContacts = true;
   	}

	private void OnEnable()
	{
		Physics.ContactModifyEvent += ModifyContacts;
	}

	private void OnDisable()
	{
		Physics.ContactModifyEvent -= ModifyContacts;
	}

	private Func<Vector3, Vector3> ExtractGetPointVelocity(int instanceID)
	{
		if(instanceID == 0)
			return _ => Vector3.zero;

		var o = Resources.EntityIdToObject(instanceID);

		if(o is Rigidbody rb)
			return rb.GetPointVelocity;
		if(o is ArticulationBody ab)
			return ab.GetPointVelocity;

		return _ => Vector3.zero;
	}

	private void ModifyContacts(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
	{
		for(int i = 0; i < pairs.Length; i++)
		{
			var pair = pairs[i];

			bool me = pair.colliderInstanceID == overlapCollider.GetInstanceID();
			bool other = pair.otherColliderInstanceID == overlapCollider.GetInstanceID();

			if(!me && !other)
				continue; // We don't care about this collision

			if(me && other)
				continue; // Collision with ourself, shouldn't happen

			Vector3 specialAxis = me ? Vector3.up : Vector3.down; 

			var getPointVelocity = ExtractGetPointVelocity(pair.bodyInstanceID);
			var getOtherPointVelocity = ExtractGetPointVelocity(pair.otherBodyInstanceID);

			for(int j = 0; j < pair.contactCount; j++)
			{
				Vector3 point = pair.GetPoint(j);
				Vector3 normal = pair.GetNormal(j);
				float separation = pair.GetSeparation(j);

				Vector3 relativeVelocity = getPointVelocity(point) - getOtherPointVelocity(point);

				Vector3 axis = Vector3.Cross(normal, specialAxis).normalized;
				Vector3 axis2 = Vector3.Cross(axis, specialAxis).normalized;

				Vector3 dynamicNormal;

				if(Vector3.Dot(specialAxis, normal) > 0.99)
					dynamicNormal = specialAxis;
				if(relativeVelocity.magnitude < 0.001f)
					dynamicNormal = normal;
				else if(Vector3.Dot(specialAxis, relativeVelocity) >= 0)
					dynamicNormal = Vector3.Cross(Vector3.ProjectOnPlane(relativeVelocity.normalized, axis), axis).normalized;
				else if(Vector3.Dot(normal, relativeVelocity) >= 0 || Vector3.Dot(axis2, relativeVelocity) < 0)
					dynamicNormal = normal;
				else
				{
					float t = Mathf.InverseLerp(-1, 0, Vector3.Dot(specialAxis, relativeVelocity.normalized));
					dynamicNormal = Vector3.Slerp(normal, specialAxis, t);
				}

				float dynamicSeparation = Mathf.Min(0, Vector3.Dot(dynamicNormal, relativeVelocity) * Time.fixedDeltaTime);

				dynamicSeparation = Mathf.Max(Vector3.Dot(dynamicNormal, normal * separation), dynamicSeparation);

				pair.SetNormal(j, dynamicNormal);
				pair.SetSeparation(j, dynamicSeparation);
			}
		}
	}
}
