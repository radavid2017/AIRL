using UnityEngine;
using System.Collections.Generic;

public class CarTrajectory : MonoBehaviour
{
    public Transform target;
    public Camera frontCamera;

    public float detectionRange = 10f;
    public float carWidth = 2f;
    public float stepDistance = 1f;
    public int maxSteps = 200;
    public LayerMask obstacleLayer;

    public GameObject trajectoryDiscPrefab;
    private List<GameObject> spawnedDiscs = new List<GameObject>();

    private float initialOffset = 3.22f;

    // Avoidance side: -1 = left, 1 = right, 0 = none
    private int avoidanceSide = 0;

    // Cooldown timer to prevent instant side flipping
    private float sideSwitchCooldown = 0f;
    private float sideSwitchCooldownTime = 0.5f; // half second delay

    private void Update()
    {
        if (target == null || frontCamera == null || trajectoryDiscPrefab == null) return;

        // Decrease cooldown timer
        if (sideSwitchCooldown > 0f)
            sideSwitchCooldown -= Time.deltaTime;

        UpdateTrajectory();
    }

    private void UpdateTrajectory()
    {
        ClearOldDiscs();

        Vector3 start = transform.position + transform.forward * initialOffset;
        List<Vector3> path = CalculateTrajectory(start, target.position);
        SpawnTrajectoryDiscs(path);
    }

    private List<Vector3> CalculateTrajectory(Vector3 start, Vector3 end)
    {
        List<Vector3> points = new List<Vector3>();
        Vector3 currentPos = start;
        int steps = 0;

        while (Vector3.Distance(currentPos, end) > stepDistance && steps < maxSteps)
        {
            points.Add(currentPos);
            Vector3 targetDir = (end - currentPos).normalized;

            // Sample multiple angles depending on current avoidance side
            int[] anglesToTry;
            if (avoidanceSide == 0)
            {
                anglesToTry = new int[] { 0, -15, 15, -30, 30, -45, 45, -60, 60 };
            }
            else if (avoidanceSide == -1)
            {
                anglesToTry = new int[] { -15, -30, -45, -60, 0, 15, 30, 45, 60 };
            }
            else
            {
                anglesToTry = new int[] { 15, 30, 45, 60, 0, -15, -30, -45, -60 };
            }

            int bestSideCandidate = 0;
            float bestScore = float.MinValue;
            Vector3 bestDir = Vector3.zero;

            foreach (int angle in anglesToTry)
            {
                Vector3 candidateDir = Quaternion.Euler(0, angle, 0) * targetDir;

                // Check obstacles using 3 raycasts across car width
                bool hitObstacle = false;
                int rayCount = 5; // More rays for better coverage
                for (int r = 0; r < rayCount; r++)
                {
                    float t = (float)r / (rayCount - 1);
                    Vector3 origin = currentPos + transform.right * (t - 0.5f) * carWidth;
                    if (Physics.Raycast(origin, candidateDir, detectionRange, obstacleLayer))
                    {
                        hitObstacle = true;
                        break;
                    }
                }
                if (hitObstacle) continue;

                // Score: dot product with target direction (how close to straight)
                float score = Vector3.Dot(candidateDir, targetDir);

                // Add bias towards current avoidance side to avoid flipping
                if (avoidanceSide != 0)
                {
                    int candidateSide = angle < 0 ? 1 : (angle > 0 ? -1 : 0);
                    if (candidateSide == avoidanceSide)
                    {
                        score += 0.1f; // small bias towards sticking to current side
                    }
                    else if (candidateSide == -avoidanceSide)
                    {
                        score -= 0.2f; // penalize opposite side
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = candidateDir;
                    bestSideCandidate = angle < 0 ? -1 : (angle > 0 ? 1 : 0);
                }
            }

            if (bestScore == float.MinValue)
            {
                // No valid direction found; stop pathfinding
                break;
            }

            // Decide whether to switch avoidance side
            if (avoidanceSide != bestSideCandidate)
            {
                // Switch only if cooldown elapsed and new side is clearly better
                if (sideSwitchCooldown <= 0f)
                {
                    avoidanceSide = bestSideCandidate;
                    sideSwitchCooldown = sideSwitchCooldownTime;
                }
                else
                {
                    // If cooldown not elapsed, stick with old side, even if not best
                    // Find best direction on current side

                    Vector3 fallbackDir = Vector3.zero;
                    float fallbackScore = float.MinValue;
                    foreach (int angle in anglesToTry)
                    {
                        int sideCandidate = angle < 0 ? 1 : (angle > 0 ? 1 : 0);
                        if (sideCandidate != avoidanceSide && sideCandidate != 0) continue;

                        Vector3 candidateDir = Quaternion.Euler(0, angle, 0) * targetDir;

                        bool hitObstacle = false;
                        Vector3 centerOrigin = currentPos;
                        Vector3 leftOrigin = currentPos - transform.right * carWidth * 5f;
                        Vector3 rightOrigin = currentPos + transform.right * carWidth * 5f;

                        if (Physics.Raycast(centerOrigin, candidateDir, detectionRange, obstacleLayer) ||
                            Physics.Raycast(leftOrigin, candidateDir, detectionRange, obstacleLayer) ||
                            Physics.Raycast(rightOrigin, candidateDir, detectionRange, obstacleLayer))
                        {
                            hitObstacle = true;
                        }

                        if (hitObstacle) continue;

                        float score = Vector3.Dot(candidateDir, targetDir);
                        if (score > fallbackScore)
                        {
                            fallbackScore = score;
                            fallbackDir = candidateDir;
                        }
                    }

                    if (fallbackScore != float.MinValue)
                        bestDir = fallbackDir;
                }
            }

            currentPos += bestDir * stepDistance;
            steps++;

            // Clear avoidance side if path is roughly straight and no obstacles ahead
            if (avoidanceSide != 0)
            {
                bool pathClear = !Physics.Raycast(currentPos, targetDir, detectionRange, obstacleLayer);
                if (pathClear && Vector3.Angle(bestDir, targetDir) < 10f)
                {
                    avoidanceSide = 0;
                }
            }
        }

        // Offset the end point if avoiding
        if (avoidanceSide != 0)
        {
            // Get distance to closest obstacle in the car's forward direction
            float closestDist = GetClosestObstacleDistance(currentPos, transform.forward);

            // Map distance to a multiplier: closer = bigger offset, farther = normal offset
            float minOffset = carWidth * 0.6f;
            float maxOffset = carWidth * 1.2f;
            float t = Mathf.Clamp01(1f - (closestDist / detectionRange)); // t=1 when very close, t=0 when far
            float dynamicOffset = Mathf.Lerp(minOffset, maxOffset, t);

            end += transform.right * avoidanceSide * dynamicOffset;
        }

        points.Add(end);
        return points;
    }

    private void SpawnTrajectoryDiscs(List<Vector3> path)
    {
        if (path.Count < 2) return;

        float spacing = stepDistance;
        float accumulated = 0f;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 p0 = path[i];
            Vector3 p1 = path[i + 1];
            float segmentLength = Vector3.Distance(p0, p1);
            Vector3 dir = (p1 - p0).normalized;

            float dist = spacing - accumulated;
            while (dist < segmentLength)
            {
                Vector3 spawnPos = p0 + dir * dist;
                spawnPos.y = 0f;

                GameObject disc = Instantiate(trajectoryDiscPrefab);
                disc.transform.position = spawnPos;
                disc.transform.localScale = new Vector3(1f, 0.1f, 1f);
                disc.transform.parent = transform;

                spawnedDiscs.Add(disc);

                dist += spacing;
            }

            accumulated = segmentLength - (dist - spacing);
            if (accumulated < 0f) accumulated = 0f;
        }

        Vector3 finalPos = path[path.Count - 1];
        finalPos.y = 0f;

        GameObject finalDisc = Instantiate(trajectoryDiscPrefab);
        finalDisc.transform.position = finalPos;
        finalDisc.transform.localScale = new Vector3(1f, 0.1f, 1f);
        finalDisc.transform.parent = transform;

        spawnedDiscs.Add(finalDisc);
    }

    private void ClearOldDiscs()
    {
        foreach (GameObject disc in spawnedDiscs)
        {
            if (disc != null) Destroy(disc);
        }
        spawnedDiscs.Clear();
    }

    public List<Vector3> GetCurrentPath()
    {
        Vector3 start = transform.position + transform.forward * initialOffset;
        return CalculateTrajectory(start, target.position);
    }

    private float GetClosestObstacleDistance(Vector3 position, Vector3 direction)
    {
        RaycastHit hit;
        if (Physics.Raycast(position, direction, out hit, detectionRange, obstacleLayer))
        {
            return hit.distance;
        }
        return detectionRange;
    }
}
