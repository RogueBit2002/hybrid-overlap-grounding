using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace HybridOverlapGrounding
{
	public class ConditionalVelocityDepenetration : MonoBehaviour
    {
		public enum AxisSpace
		{
			World,
			Transform,
			Body
		}

		[SerializeField] private Vector3 specialAxis = Vector3.up;
		public Vector3 SpecialAxis { get => specialAxis; set => specialAxis = value; }

		
		[SerializeField] private AxisSpace space = AxisSpace.World;
		public AxisSpace Space { get => space; set => space = value; }

		[SerializeField] private bool registerChildColliders = false;


		private static Dictionary<int, ConditionalVelocityDepenetration> lookup = new();
		private static int activeCount = 0;

		private List<int> colliderIDs;


		private void Awake()
		{
			Collider[] colliders = registerChildColliders ? GetComponents<Collider>() : GetComponentsInChildren<Collider>();
			colliderIDs = new();

			for(int i = 0; i < colliders.Length; i++)
			{
				colliders[i].hasModifiableContacts = true;
				int id = colliders[i].GetInstanceID();

				if(lookup.ContainsKey(id))
				{
					Debug.LogWarning($"Duplicate collider registration: {colliders[i].name}");
					continue;
				}

				lookup.Add(id, this);
				colliderIDs.Add(id);
			}
		}
		
		private void OnDestroy()
		{
			for(int i = 0; i < colliderIDs.Count; i ++)
				lookup.Remove(colliderIDs[i]);

			colliderIDs.Clear();
		}

		private void OnEnable()
		{
			if (++activeCount == 1)
				Physics.ContactModifyEvent += ModifyContacts;
		}

		private void OnDisable()
		{
			if (--activeCount == 0)
				Physics.ContactModifyEvent -= ModifyContacts;
		}


		private static (Quaternion? bodyRotation, Func<Vector3, Vector3> getPointVelocity) ExtractBodyProperties(int bodyInstanceID)
		{
			if (bodyInstanceID == 0)
				return (null, _ => Vector3.zero);
			
			var o = Resources.EntityIdToObject(bodyInstanceID);

			if (o is Rigidbody rb)
				return (rb.rotation, rb.GetPointVelocity);

			Debug.LogError("ArticulationBody isn't supported yet!");

			return (null, _ => Vector3.zero);
			
		}
		private static void ModifyContacts(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
		{
			for(int i = 0; i < pairs.Length; i ++)
			{
				var pair = pairs[i];

				bool me = lookup.ContainsKey(pair.colliderInstanceID);
				bool other = lookup.ContainsKey(pair.otherColliderInstanceID);

				if(!me && !other)
					continue; // This collision does not involve CharacterFeet

				if (me && other)
					continue; // Two conditional colliders colliding. Not sure what to do here, so let's not modifiy the collision?

				ConditionalVelocityDepenetration comp = lookup[me ? pair.colliderInstanceID : pair.otherColliderInstanceID];

				if (!comp.isActiveAndEnabled)
					continue;

				Func<Vector3, Vector3> getOtherPointVelocity = ExtractBodyProperties(me ? pair.otherBodyInstanceID : pair.bodyInstanceID).getPointVelocity;
				Func<Vector3, Vector3> getMyPointVelocity;
				Quaternion bodyRotation;
				{
					var props = ExtractBodyProperties(me ? pair.bodyInstanceID : pair.otherBodyInstanceID);
					getMyPointVelocity = props.getPointVelocity;
					bodyRotation = props.bodyRotation.HasValue ? props.bodyRotation.Value : comp.transform.rotation;
				}
			
				Vector3 specialAxis = comp.space switch
				{
					AxisSpace.World => comp.specialAxis,
					AxisSpace.Body => bodyRotation * comp.specialAxis,
					AxisSpace.Transform => comp.transform.rotation * comp.specialAxis,
					_ => throw new NotImplementedException()
				};

				for(int j = 0; j < pair.contactCount; j ++)
				{
					Vector3 point = pair.GetPoint(j);
					Vector3 normal = me ? pair.GetNormal(j) : -pair.GetNormal(j);
					float separation = pair.GetSeparation(j);

					Vector3 myVelocity = getMyPointVelocity(point);
					Vector3 otherVelocity = getOtherPointVelocity(point);
					
					Vector3 relativeVelocity = myVelocity - otherVelocity;

					Vector3 axis = Vector3.Cross(normal, specialAxis).normalized;
					Vector3 surfaceAlignedNormal = Vector3.Cross(normal, axis).normalized;

					Vector3 dynamicNormal = normal;

					if (Vector3.Dot(specialAxis, normal) >= 0.99)
						dynamicNormal = specialAxis;
					else if (relativeVelocity.magnitude <= 0.001f)
						dynamicNormal = normal;
					else if (Vector3.Dot(normal, relativeVelocity) >= 0 || Vector3.Dot(surfaceAlignedNormal, relativeVelocity) > 0)
						dynamicNormal = normal;
					else if (Vector3.Dot(specialAxis, relativeVelocity) >= 0)
						dynamicNormal = Vector3.Cross(Vector3.ProjectOnPlane(relativeVelocity.normalized, axis), axis).normalized;
					else
					{
						float a = Vector3.Dot(specialAxis, -normal);
						float t = Mathf.InverseLerp(0, a, Vector3.Dot(specialAxis, relativeVelocity.normalized));

						dynamicNormal = Vector3.Slerp(specialAxis, normal, t);
					}

					// Prevent sinking in deeper
					float dynamicSeparation = Mathf.Min(0, Vector3.Dot(dynamicNormal, relativeVelocity) * Time.fixedDeltaTime);

					// Prevent over correction
					dynamicSeparation = Mathf.Max(Vector3.Dot(dynamicNormal, normal * separation), dynamicSeparation);

					pair.SetNormal(j, me ? dynamicNormal : -dynamicNormal);
					pair.SetSeparation(j, dynamicSeparation);
				}
			}
		}
	}
}
