using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Duplicates the currently selected design and attaches it to the left controller
/// when toggled on. Detaches/removes the duplicate when toggled off.
/// Designed to be wired to a UI Toggle via OnValueChanged(bool).
/// </summary>
public class AttachDesignToController : MonoBehaviour
{
    [Header("Controller & Placement")]
    [Tooltip("Transform of the left VR controller (assign manually or via scene).")]
    public Transform leftController;

    [Tooltip("Distance in meters in front of the controller to place the duplicate.")]
    public float distance = 0.25f;

    [Tooltip("If true, match the controller's rotation when attaching.")]
    public bool alignRotation = true;

    [Tooltip("If true, parent the duplicate to the controller to follow it.")]
    public bool parentToController = true;

    [Header("Scale")]
    [Tooltip("Uniform scale percentage of the attached clone relative to the original design (100 = original size, 20 = one fifth)")]
    [Min(0.01f)] public float scalePercent = 100f;

    [Header("Selection")]
    [Tooltip("Parent that contains all whiteboard drawings/imports. When set and 'Attach Entire Board' is true, all eligible children are duplicated and attached.")]
    public Transform whiteboardRoot;

    [Tooltip("If true, attach all design objects under the whiteboard root instead of a single currentDesign.")]
    public bool attachEntireBoard = true;

    [Tooltip("If whiteboardRoot is not assigned, try to use SplineVRDraw.whiteboardPlane automatically.")]
    public bool autoDetectWhiteboardRoot = true;

    [Tooltip("Include MeshRenderers when attaching whole board (for OBJ/mesh imports). Leave off to avoid cloning the whiteboard surface mesh.")]
    public bool includeMeshesInWholeBoard = false;

    [Tooltip("The currently selected drawn or imported design (set by other systems).")]
    public GameObject currentDesign;

    [Header("Runtime (Read-Only)")]
    [SerializeField, Tooltip("The active attached duplicate instance.")]
    private GameObject attachedClone;

    [Header("Events")] 
    public UnityEvent OnAttach;
    public UnityEvent OnDetach;

    /// <summary>
    /// Hook this to a UI Toggle's OnValueChanged(bool) event.
    /// </summary>
    /// <param name="isOn">True to create/attach, False to remove/detach.</param>
    public void OnToggleChanged(bool isOn)
    {
        if (isOn)
            Attach();
        else
            Detach();
    }

    /// <summary>
    /// Convenience for hooking to a regular Button (not a Toggle).
    /// If nothing is attached, calls Attach(); otherwise calls Detach().
    /// </summary>
    public void Toggle()
    {
        if (attachedClone == null)
            Attach();
        else
            Detach();
    }

    /// <summary>
    /// Programmatically set the current selected design.
    /// </summary>
    public void SetCurrentDesign(GameObject design)
    {
        currentDesign = design;
    }

    /// <summary>
    /// Creates a duplicate of the current design and places it in front of the left controller.
    /// Optionally aligns rotation and parents to the controller.
    /// </summary>
    public void Attach()
    {
        // Clean up any previous clone
        if (attachedClone != null)
        {
            Destroy(attachedClone);
            attachedClone = null;
        }

        if (leftController == null)
        {
            Debug.LogWarning("AttachDesignToController: Left controller is not assigned.");
            return;
        }

        // If configured, duplicate the entire whiteboard contents
        if (attachEntireBoard)
        {
            Transform root = GetWhiteboardRoot();
            if (root != null)
            {
                GameObject group = new GameObject("Whiteboard_AttachedClone");

                int clonedCount = 0;
                foreach (Transform child in root)
                {
                    if (!IsDesignRoot(child))
                        continue;

                    GameObject clone = Instantiate(child.gameObject);
                    clone.name = child.gameObject.name + "_AttachedClone";
                    clone.transform.SetParent(group.transform, worldPositionStays: true);
                    clonedCount++;
                }

                if (clonedCount == 0)
                {
                    Debug.LogWarning("AttachDesignToController: No eligible whiteboard children found to attach.");
                    Destroy(group);
                    // Fallback to single currentDesign if provided
                    if (currentDesign == null)
                        return;
                    // else continue below to single-object path
                }
                else
                {
                    // Parent/group handling
                    if (parentToController)
                    {
                        group.transform.SetParent(leftController, worldPositionStays: true);
                        ConvertLineRenderersToLocal(group);
                    }

                    if (!group.activeSelf) group.SetActive(true);
                    attachedClone = group;

                    // Mark as an attached clone so exporters can ignore it
                    TryMarkAsAttachedClone(attachedClone);

                    // Align rotation if requested (applies to group root)
                    if (alignRotation)
                        group.transform.rotation = leftController.rotation;

                    // Apply scale before placement
                    ApplyScale(attachedClone);

                    PositionCloneAtDistance(group);
                    OnAttach?.Invoke();
                    return;
                }
            }
        }

        if (currentDesign == null)
        {
            Debug.LogWarning("AttachDesignToController: No currentDesign set to duplicate and attachEntireBoard is disabled or empty.");
            return;
        }

        // Compute initial rotation for the clone
        Quaternion targetRotation = alignRotation ? leftController.rotation : currentDesign.transform.rotation;

        // Instantiate first; final position is adjusted below so that
        // distance=0 puts the geometry on the controller
        attachedClone = Instantiate(currentDesign, currentDesign.transform.position, targetRotation);
        attachedClone.name = currentDesign.name + "_AttachedClone";

        // Optionally parent to controller (keep world transform)
        if (parentToController)
        {
            attachedClone.transform.SetParent(leftController, worldPositionStays: true);

            // If the clone uses LineRenderers in world space, convert to local so it follows the parent
            ConvertLineRenderersToLocal(attachedClone);
        }

        // Ensure the clone is active
        if (!attachedClone.activeSelf)
            attachedClone.SetActive(true);

        // Mark as an attached clone so exporters can ignore it
        TryMarkAsAttachedClone(attachedClone);

        // Apply scale before placement
        ApplyScale(attachedClone);

        // Place the clone so that the geometry center is at
        // controller.position + forward * distance (so 0 = on controller)
        PositionCloneAtDistance(attachedClone);

        OnAttach?.Invoke();
    }

    /// <summary>
    /// Removes the attached duplicate (if any).
    /// </summary>
    public void Detach()
    {
        if (attachedClone != null)
        {
            Destroy(attachedClone);
            attachedClone = null;
        }

        OnDetach?.Invoke();
    }

    private bool IsDesignRoot(Transform t)
    {
        // Consider an object part of the design if it or its children contain
        // LineRenderer (drawn shapes), world-space TMP text, and optionally MeshRenderers.
        return HasDesignComponentRecursive(t);
    }

    private bool HasDesignComponentRecursive(Transform t)
    {
        if (t == null) return false;
        if (t.GetComponent<LineRenderer>() != null) return true;
        if (t.GetComponent<TextMeshPro>() != null) return true;
        if (includeMeshesInWholeBoard && t.GetComponent<MeshRenderer>() != null) return true;

        for (int i = 0; i < t.childCount; i++)
        {
            if (HasDesignComponentRecursive(t.GetChild(i))) return true;
        }
        return false;
    }

    private Transform GetWhiteboardRoot()
    {
        if (whiteboardRoot != null) return whiteboardRoot;
        if (!autoDetectWhiteboardRoot) return null;

        #if UNITY_2023_1_OR_NEWER
        var drawer = FindFirstObjectByType<SplineVRDraw>();
        #else
        var drawer = FindObjectOfType<SplineVRDraw>();
        #endif
        if (drawer != null && drawer.whiteboardPlane != null)
            return drawer.whiteboardPlane;
        return null;
    }

    private static System.Type _markerTypeCache;
    private static System.Type GetMarkerType()
    {
        if (_markerTypeCache == null)
        {
            _markerTypeCache = System.Type.GetType("AttachedCloneMarker, Assembly-CSharp");
        }
        return _markerTypeCache;
    }

    private void TryMarkAsAttachedClone(GameObject go)
    {
        var type = GetMarkerType();
        if (type == null || go == null) return;
        if (go.GetComponent(type) == null)
            go.AddComponent(type);
    }

    /// <summary>
    /// Repositions the existing attached clone to respect the current
    /// distance/rotation settings. Safe to call at runtime after tweaking fields.
    /// </summary>
    public void RefreshAttachmentPose()
    {
        if (attachedClone == null || leftController == null) return;
        if (alignRotation)
        {
            attachedClone.transform.rotation = leftController.rotation;
        }
        PositionCloneAtDistance(attachedClone);
    }

    private void PositionCloneAtDistance(GameObject clone)
    {
        if (clone == null || leftController == null) return;

        // Target anchor position in front of the controller
        Vector3 anchor = leftController.position + leftController.forward * distance;

        // Find the current geometry center (world) of the clone
        Vector3 center = ComputeGeometryCenterWorld(clone, out bool found);
        if (!found)
        {
            // Fallback: use clone pivot
            center = clone.transform.position;
        }

        Vector3 delta = anchor - center;
        ShiftGeometryWorld(clone, delta);
    }

    private void ApplyScale(GameObject clone)
    {
        float factor = Mathf.Max(0.0001f, scalePercent * 0.01f);
        if (Mathf.Approximately(factor, 1f)) return;

        // Snapshot center as pivot for world-space LineRenderers
        Vector3 pivot = ComputeGeometryCenterWorld(clone, out bool found);
        if (!found) pivot = clone.transform.position;

        // Scale the transform hierarchy (meshes, local-space LRs)
        clone.transform.localScale = clone.transform.localScale * factor;

        // Additionally scale any LineRenderer using world space (transform scale does not affect their points)
        var lrs = clone.GetComponentsInChildren<LineRenderer>(includeInactive: true);
        foreach (var lr in lrs)
        {
            if (lr == null || !lr.useWorldSpace) continue;
            int count = lr.positionCount;
            if (count <= 0) continue;
            var pts = new Vector3[count];
            lr.GetPositions(pts);
            for (int i = 0; i < count; i++)
            {
                pts[i] = pivot + (pts[i] - pivot) * factor;
            }
            lr.SetPositions(pts);
        }
    }

    private Vector3 ComputeGeometryCenterWorld(GameObject root, out bool found)
    {
        found = false;
        Bounds combined = new Bounds();

        // Mesh/Renderer bounds
        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (var r in renderers)
        {
            if (!found)
            {
                combined = r.bounds;
                found = true;
            }
            else
            {
                combined.Encapsulate(r.bounds);
            }
        }

        // LineRenderer bounds via explicit point sampling (more reliable than r.bounds)
        var lrs = root.GetComponentsInChildren<LineRenderer>(includeInactive: true);
        foreach (var lr in lrs)
        {
            int count = lr != null ? lr.positionCount : 0;
            if (count <= 0) continue;

            var pts = new Vector3[count];
            lr.GetPositions(pts);
            for (int i = 0; i < count; i++)
            {
                Vector3 world = lr.useWorldSpace ? pts[i] : lr.transform.TransformPoint(pts[i]);
                if (!found)
                {
                    combined = new Bounds(world, Vector3.zero);
                    found = true;
                }
                else
                {
                    combined.Encapsulate(world);
                }
            }
        }

        return found ? combined.center : root.transform.position;
    }

    private void ShiftGeometryWorld(GameObject root, Vector3 delta)
    {
        if (delta == Vector3.zero) return;

        // Move the root transform
        root.transform.position += delta;

        // If any LineRenderers use world space, shift their points so
        // they move with the transform change
        var lrs = root.GetComponentsInChildren<LineRenderer>(includeInactive: true);
        foreach (var lr in lrs)
        {
            if (lr == null || !lr.useWorldSpace) continue;
            int count = lr.positionCount;
            if (count <= 0) continue;
            var pts = new Vector3[count];
            lr.GetPositions(pts);
            for (int i = 0; i < count; i++) pts[i] += delta;
            lr.SetPositions(pts);
        }
    }

    private void OnDisable()
    {
        // Clean up if this component is disabled while attached
        if (attachedClone != null)
        {
            Destroy(attachedClone);
            attachedClone = null;
        }
    }

    private void ConvertLineRenderersToLocal(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<LineRenderer>(includeInactive: true);
        foreach (var lr in renderers)
        {
            if (lr == null || !lr.useWorldSpace) continue;
            int count = lr.positionCount;
            if (count <= 0) continue;
            var world = new Vector3[count];
            lr.GetPositions(world);
            for (int i = 0; i < count; i++)
            {
                world[i] = lr.transform.InverseTransformPoint(world[i]);
            }
            lr.useWorldSpace = false;
            lr.SetPositions(world);
        }
    }
}
