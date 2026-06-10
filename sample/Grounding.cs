using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HybridOverlapGrounding
{
    public static class Grounding
    {
		public struct GroundInfo
		{
			public float distance;
			public Vector3 contactPoint;
			public Vector3 surfaceNormal;
			public Vector3 contactNormal;
			public Collider collider;
		}

		public static GroundInfo[] ForSphere(Vector3 origin, float radius, Vector3 direction, float maxDistance)
		{
			return ForSphere(origin, radius, direction, maxDistance, null);
		}
		public static GroundInfo[] ForSphere(Vector3 origin, float radius, Vector3 direction, float maxDistance, Collider[] collidersToIgnore)
		{
			GroundInfo[] DoBroadPhase()
			{
				var broadphaseHits = Physics.SphereCastAll(origin, radius, direction, maxDistance);

				List<GroundInfo> info = new(broadphaseHits.Length);

				for (int i = 0; i < broadphaseHits.Length; i++)
				{
					var hit = broadphaseHits[i];
					
					if (collidersToIgnore?.Contains(hit.collider) ?? false)
						continue;

					// Sometimes a collider already overlaps the sphere, which causes a weird zero hit. It's best to ignore those for grounding
					if (hit.distance == 0 && hit.point == Vector3.zero)
						continue;

					// Almost vertical wall
					if (Vector3.Dot(direction.normalized, hit.normal) > -0.01)
						continue;

					info.Add(new()
					{
						distance = hit.distance,
						collider = hit.collider,
						contactPoint = hit.point,
						contactNormal = hit.normal,
					});
				}
				return info.ToArray();
			}


			void DoNarrowPhase(GroundInfo[] infos)
			{
				for (int i = 0; i < infos.Length; i++)
				{

					// Check if normal is same as direction (meaning flat surface)
					if (infos[i].contactNormal == -direction)
					{
						infos[i].surfaceNormal = -direction;
						continue;
					}


					// Check if ray at contact point provides information
					const float halfLength = 0.003f;

					Vector3 c = infos[i].contactPoint - direction * halfLength;

					if (infos[i].collider.Raycast(new Ray(c, direction), out var hit, halfLength * 2))
					{
						infos[i].surfaceNormal = hit.normal;
						continue;
					}

					const int rayCount = 4;
					const float raySpread = 0.01f;

					List<RaycastHit> hits = new(rayCount);
					Vector3 offsetDirection = Vector3.ProjectOnPlane(-infos[i].contactNormal, direction); //Vector3.ProjectOnPlane(infos[i].contactPoint - origin, direction).normalized;
					
					Vector3 axis = Vector3.Cross(direction, infos[i].contactNormal);
					
					bool found = false;
					for(int j = 0; j < rayCount; j ++)
					{
						float t = (((float)j / (rayCount - 1)) - 0.5f) * 2;

						Vector3 o = c + t * offsetDirection * raySpread;
						Vector3 colliderPoint = infos[i].collider.ClosestPoint(o);

						o -= direction * halfLength;

						if (!infos[i].collider.Raycast(new Ray(o, direction), out hit, 2 * halfLength))
							continue;

						// The ray did not hit the target point
						if (Vector3.Distance(hit.point, colliderPoint) >= 0.001)
							continue;

						infos[i].surfaceNormal = hit.normal;
						found = true;
						break;
					}

					if (found)
						continue;

					infos[i].surfaceNormal = infos[i].contactNormal;
					/*
					// Sample surrounding points to estimate surface normal
					const int rayCount = 4;
					const float raySpread = 0.01f;

					List<RaycastHit> hits = new(rayCount);
					Vector3 offsetDirection = Vector3.ProjectOnPlane(infos[i].contactPoint - origin, direction).normalized;

					Vector3 axis = Vector3.Cross(direction, infos[i].contactNormal);
					Vector3 contactAlignedAxis = Vector3.Cross(axis, infos[i].contactNormal).normalized;

					for (int j = 0; j < rayCount; j++)
					{
						float t = (((float)j / (rayCount - 1)) - 0.5f) * 2;

						if (!infos[i].collider.Raycast(new Ray(c + t * contactAlignedAxis * raySpread, direction), out hit, 2 * heightOffset))
							continue;

						hits.Add(hit);
					}

					// Worst case scenario
					if (hits.Count == 0)
					{
						infos[i].surfaceNormal = infos[i].contactNormal;
						continue;
					}

					Vector3 avg = Vector3.zero;
					for (int j = 0; j < hits.Count; j++) avg += hits[j].normal;
					avg /= hits.Count;

					infos[i].surfaceNormal = avg.normalized;*/
				}
			}

			var infos = DoBroadPhase();
			DoNarrowPhase(infos);

			return infos;
		}
		
    }
}
