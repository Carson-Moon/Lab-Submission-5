using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public class Avoider : MonoBehaviour
{
    public bool canSeeUs = false;
    public bool isHiding = false;
    public NavMeshAgent agent;

    public float range;

    [Header("Avoidee Eye Contact")]
    public Transform avoidee; // Game object to avoid.
    public LayerMask checkMask;

    [Header("Poisson Disc Samples")]
    [SerializeField] List<Vector3> candidates = new List<Vector3>();
    [SerializeField] List<Vector3> notValid = new List<Vector3>();

    private void Start()
    {
        //CreatePoissonDisc();
    }

    private void Update() {

        canSeeUs = CanAvoideeSeeUs(transform.position);
        // Determine if the avoidee can see us.
        if(canSeeUs && !isHiding)
        {
            CreatePoissonDisc();
            isHiding = true;
        }

        if(!canSeeUs)
        {
            isHiding = false;
        }
    }

    // Create and sort our poisson disc points!
    private void CreatePoissonDisc(){
        // Grab our current position.
        Vector3 pos = transform.position;

        // The width and height for the sampler.
        float width = range;
        float height = range;

        // Get an offset to apply to our generated points. (This puts the avoider at the center of the samples.)
        Vector2 centerOffset = new Vector2(width / 2f, height / 2f);

        PoissonDiscSampler sampler = new PoissonDiscSampler(width, height, 1f);

        // Create and store our sample points.
        foreach(Vector2 sample in sampler.Samples())
        {
            // Shift our samples to respect our new center.
            Vector2 centeredSample = sample - centerOffset;
            Vector3 actualPos = new Vector3(centeredSample.x + pos.x, 1f, centeredSample.y + pos.z);

            // Determine if this point is hidden from the avoidee.
            if(CanAvoideeSeeUs(actualPos))
            {
                // Add this sample to not valid.
                notValid.Add(actualPos);
            }
            else
            {                
                // Add this sample to candidates.
                candidates.Add(actualPos);
            }
        }

        // Move to the closest candidate.
        Vector3 newPos = ClosestCandidate();

        // If our new position is 'null', restart the check!
        if(newPos == Vector3.zero)
        {
            CreatePoissonDisc();
        }
        // If not, move to the new position!
        else
        {
            print(newPos);
            agent.destination = newPos;

            // Clear our previous points.
            candidates.Clear();
            notValid.Clear();
        }
    }

    // Find and return the closest candidate.
    private Vector3 ClosestCandidate(){
        if(candidates.Count == 0){
            return Vector3.zero;
        }

        Vector3 closestPoint = Vector3.zero;
        float closestDist = 999;
        float dist;

        // Loop through our candidates.
        foreach(Vector3 point in candidates)
        {
            dist = Vector3.Distance(transform.position, point);
            if(dist < closestDist)
            {
                closestPoint = point;
                closestDist = dist;
            }
        }

        return closestPoint;
    }

    // Determine if this point is hidden from the avoidee.
    private bool CanAvoideeSeeUs(Vector3 point){
        RaycastHit hit;

        // Get direction from this point to the avoidee.
        Vector3 dir = avoidee.position - point;

        // Send a raycast in that direction.
        if(Physics.Raycast(point, dir, out hit, 100, checkMask))
        {
            // If we hit the avoidee, return true.
            if(hit.transform.gameObject.layer == 6) // Avoidee Layer
            {
                return true;
            }
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        // Eye contact.
        Gizmos.DrawLine(transform.position, avoidee.position);

        Gizmos.color = Color.green;

        // Candidates
        foreach(Vector3 candidate in candidates){
            Gizmos.DrawLine(transform.position, candidate);
        }

        Gizmos.color = Color.red;

        // Not Valid
        foreach(Vector3 point in notValid){
            Gizmos.DrawLine(transform.position, point);
        }
    }
}

public class PoissonDiscSampler
{
    private const int k = 30;  // Maximum number of attempts before marking a sample as inactive.

    private readonly Rect rect;
    private readonly float radius2;  // radius squared
    private readonly float cellSize;
    private Vector2[,] grid;
    private List<Vector2> activeSamples = new List<Vector2>();

    /// Create a sampler with the following parameters:
    ///
    /// width:  each sample's x coordinate will be between [0, width]
    /// height: each sample's y coordinate will be between [0, height]
    /// radius: each sample will be at least `radius` units away from any other sample, and at most 2 * `radius`.
    public PoissonDiscSampler(float width, float height, float radius)
    {
        rect = new Rect(0, 0, width, height);
        radius2 = radius * radius;
        cellSize = radius / Mathf.Sqrt(2);
        grid = new Vector2[Mathf.CeilToInt(height / cellSize),
                           Mathf.CeilToInt(width / cellSize)];
    }

    /// Return a lazy sequence of samples. You typically want to call this in a foreach loop, like so:
    ///   foreach (Vector2 sample in sampler.Samples()) { ... }
    public IEnumerable<Vector2> Samples()
    {
        // First sample is choosen randomly
        yield return AddSample(new Vector2(Random.value * rect.width, Random.value * rect.height));

        while (activeSamples.Count > 0)
        {

            // Pick a random active sample
            int i = (int)Random.value * activeSamples.Count;
            Vector2 sample = activeSamples[i];

            // Try `k` random candidates between [radius, 2 * radius] from that sample.
            bool found = false;
            for (int j = 0; j < k; ++j)
            {

                float angle = 2 * Mathf.PI * Random.value;
                float r = Mathf.Sqrt(Random.value * 3 * radius2 + radius2); // See: http://stackoverflow.com/questions/9048095/create-random-number-within-an-annulus/9048443#9048443
                Vector2 candidate = sample + r * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // Accept candidates if it's inside the rect and farther than 2 * radius to any existing sample.
                if (rect.Contains(candidate) && IsFarEnough(candidate))
                {
                    found = true;
                    yield return AddSample(candidate);
                    break;
                }
            }

            // If we couldn't find a valid candidate after k attempts, remove this sample from the active samples queue
            if (!found)
            {
                activeSamples[i] = activeSamples[activeSamples.Count - 1];
                activeSamples.RemoveAt(activeSamples.Count - 1);
            }
        }
    }

    private bool IsFarEnough(Vector2 sample)
    {
        GridPos pos = new GridPos(sample, cellSize);

        int xmin = Mathf.Max(pos.x - 2, 0);
        int ymin = Mathf.Max(pos.y - 2, 0);
        int xmax = Mathf.Min(pos.x + 2, grid.GetLength(0) - 1);
        int ymax = Mathf.Min(pos.y + 2, grid.GetLength(1) - 1);

        for (int y = ymin; y <= ymax; y++)
        {
            for (int x = xmin; x <= xmax; x++)
            {
                Vector2 s = grid[x, y];
                if (s != Vector2.zero)
                {
                    Vector2 d = s - sample;
                    if (d.x * d.x + d.y * d.y < radius2) return false;
                }
            }
        }

        return true;

        // Note: we use the zero vector to denote an unfilled cell in the grid. This means that if we were
        // to randomly pick (0, 0) as a sample, it would be ignored for the purposes of proximity-testing
        // and we might end up with another sample too close from (0, 0). This is a very minor issue.
    }

    /// Adds the sample to the active samples queue and the grid before returning it
    private Vector2 AddSample(Vector2 sample)
    {
        activeSamples.Add(sample);
        GridPos pos = new GridPos(sample, cellSize);
        grid[pos.x, pos.y] = sample;
        return sample;
    }

    /// Helper struct to calculate the x and y indices of a sample in the grid
    private struct GridPos
    {
        public int x;
        public int y;

        public GridPos(Vector2 sample, float cellSize)
        {
            x = (int)(sample.x / cellSize);
            y = (int)(sample.y / cellSize);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(Avoider)), CanEditMultipleObjects]
public class AvoiderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Use the base layout of the avoider script.
        base.OnInspectorGUI();

        RequirementChecks();
    }

    // Requirements for avoider to work that are checked to ensure they are functioning.
    private void RequirementChecks()
    {
        // If NavMeshAgent does not exist, throw a warning in the inspector.
        if (Selection.activeGameObject.GetComponent<NavMeshAgent>() == null)
        {
            EditorGUILayout.HelpBox("Make object a Nav Mesh Agent and bake the mesh!", MessageType.Warning);
        }

        // If the object to avoid has not been assigned, throw a warning that displays in the inspector.
        if (Selection.activeGameObject.GetComponent<Avoider>().avoidee == null)
        {
            EditorGUILayout.HelpBox("Make sure you assign an object to avoid!", MessageType.Warning);
        }

        // If the range the avoider will maintain from the avoidee is less than or equal to 0, throw a warning,
        // otherwise the avoider will not be avoiding.
        if (Selection.activeGameObject.GetComponent<Avoider>().range <= 0)
        {
            EditorGUILayout.HelpBox("Assign a range that is above 0.", MessageType.Warning);
        }
    }
}
#endif
