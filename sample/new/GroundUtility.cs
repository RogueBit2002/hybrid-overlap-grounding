using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class GroundUtility
{
	public struct Info
	{
		public float distance;
		public Vector3 contactPoint;
		public Vector3 surfaceNormal;
		public Vector3 contactNormal;
		public Collider collider;
	}

	public static Info[] SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance)
	{
		return SphereCast(origin, radius, direction, maxDistance, null);
	}
	public static Info[] SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance, Collider[] collidersToIgnore)
	{
		var sphereHits = Physics.SphereCastAll(origin, radius, direction, maxDistance);

		List<Info> infos = new(sphereHits.Length);

		for(int i = 0; i < sphereHits.Length; i ++)
		{
			var sphereHit = sphereHits[i];

			if(collidersToIgnore?.Contains(sphereHit.collider) ?? false)
				continue;

			// When a collider overlaps the sphere at the start of the sweep it defaults values to zero. Ignore these
			if (sphereHit.distance == 0 && sphereHit.point == Vector3.zero)
				continue;

			// Almost vertical wall. Ignore these
			if(Vector3.Dot(direction.normalized, sphereHit.normal) > -0.01)
				continue;

			Info info = new()
			{
				distance = sphereHit.distance,
				collider = sphereHit.collider,
				contactPoint = sphereHit.point,
				contactNormal = sphereHit.normal,
				surfaceNormal = sphereHit.normal // Fallback if we can't find the real normal
			};

			infos.Add(info);

			// Flat surfaces don't need extra checking
			if(sphereHit.normal == -direction)
				continue;

			const float halfLength = 0.006f;
		
			Vector3 c = sphereHit.point - direction * halfLength;

			Vector3 stepDirection = Vector3.ProjectOnPlane(-sphereHit.normal, direction).normalized;

			// First ray aimed at sphereHit.point
			// Not valid if pointing away from spherecast 
			if(sphereHit.collider.Raycast(new Ray(c, direction), out var hit, halfLength * 2) && Vector3.Dot(stepDirection, hit.normal) <= 0)
			{
				info.surfaceNormal = hit.normal;
				infos[infos.Count-1] = info;
				continue;
			}

			// Multiple rays, first to hit wins
			const int rays = 4;
			const float spread = 0.01f;

			float maxColliderDepth = sphereHit.collider.bounds.extents.magnitude * 2;
			for(int j = 0; j < rays; j ++)
			{
				float t = (((float)j / (rays - 1)) - 0.5f) * 2; // Remaps rays to -1 to 1

				Vector3 o = c + t * stepDirection * spread;

				if(!sphereHit.collider.Raycast(new Ray(o, direction), out hit, maxColliderDepth))
					continue;

				info.surfaceNormal = hit.normal;
				infos[infos.Count-1] = info;
				break;
			}				

		}

		return infos.ToArray();
	}
}
