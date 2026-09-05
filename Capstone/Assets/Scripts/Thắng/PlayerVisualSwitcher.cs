using UnityEngine;
using Capstone.Game.MapSystem;
using AaMapIcon = AAMAP.MapIcon;

[DisallowMultipleComponent]
public class PlayerVisualSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class VisualProfile
    {
        public string displayName = "Visual";
        public GameObject root;
        public Animator animator;
        public RuntimeAnimatorController controller;
        [Min(0.05f)] public float animatorSpeed = 1f;
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localEulerAngles = Vector3.zero;
        public Vector3 localScale = Vector3.one;
        public bool enableJump = true;
        public bool enableCrouch = true;
        public bool enableRoll = false;
    }

    [Header("References")]
    [SerializeField] private BasicPlayerMovement movement;
    [SerializeField] private VisualProfile[] visuals = new VisualProfile[0];

    [Header("Input")]
    [SerializeField] private bool allowRuntimeSwitch = true;
    [SerializeField] private KeyCode switchKey = KeyCode.G;
    [SerializeField] private int defaultVisualIndex;

    private int activeVisualIndex = -1;

    public int ActiveVisualIndex => activeVisualIndex;
    public string ActiveVisualName => IsValidIndex(activeVisualIndex) ? visuals[activeVisualIndex].displayName : string.Empty;

    private void Reset()
    {
        movement = GetComponent<BasicPlayerMovement>();
    }

    private void Awake()
    {
        if (movement == null)
        {
            movement = GetComponent<BasicPlayerMovement>();
        }

        defaultVisualIndex = Mathf.Clamp(defaultVisualIndex, 0, Mathf.Max(0, visuals.Length - 1));
        SwitchTo(defaultVisualIndex, true);
    }

    private void Update()
    {
        if (!allowRuntimeSwitch || visuals == null || visuals.Length <= 1)
        {
            return;
        }

        if (switchKey != KeyCode.None && Input.GetKeyDown(switchKey) && !IsGameplayInputBlocked())
        {
            SwitchToNext();
        }
    }

    public void SwitchToNext()
    {
        if (visuals == null || visuals.Length == 0)
        {
            return;
        }

        int nextIndex = activeVisualIndex < 0 ? 0 : (activeVisualIndex + 1) % visuals.Length;
        SwitchTo(nextIndex, true);
    }

    public void SwitchTo(int visualIndex, bool restartCurrentState)
    {
        if (!IsValidIndex(visualIndex))
        {
            return;
        }

        activeVisualIndex = visualIndex;
        VisualProfile activeVisual = visuals[activeVisualIndex];
        ApplyVisualAlignment(activeVisual);

        for (int i = 0; i < visuals.Length; i++)
        {
            SetVisualVisible(i, i == activeVisualIndex);
        }

        Animator activeAnimator = ResolveAnimator(activeVisual);
        if (activeAnimator != null)
        {
            if (activeVisual.controller != null)
            {
                activeAnimator.runtimeAnimatorController = activeVisual.controller;
            }

            activeAnimator.enabled = true;
            activeAnimator.speed = Mathf.Max(0.05f, activeVisual.animatorSpeed);
            activeAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            activeAnimator.applyRootMotion = movement != null && movement.useRootMotion;
        }

        if (movement != null)
        {
            movement.SetAvailableActions(activeVisual.enableJump, activeVisual.enableCrouch, activeVisual.enableRoll);
            movement.SetPlayerAnimator(activeAnimator, restartCurrentState);
        }

        DisableInactiveAnimators(activeAnimator);
        RefreshPlayerMapMarker();
    }

    private void RefreshPlayerMapMarker()
    {
        MapMarkerManager markerManager = Object.FindFirstObjectByType<MapMarkerManager>();
        if (markerManager == null)
        {
            return;
        }

        markerManager.EnsureRuntimeMarker(gameObject, MapMarkerType.Player, "player", "Player");
    }
    private void ApplyVisualAlignment(VisualProfile visual)
    {
        if (visual == null || visual.root == null || visual.root == gameObject)
        {
            return;
        }

        Transform visualTransform = visual.root.transform;
        visualTransform.localPosition = visual.localPosition;
        visualTransform.localRotation = Quaternion.Euler(visual.localEulerAngles);
        visualTransform.localScale = visual.localScale == Vector3.zero ? Vector3.one : visual.localScale;
    }

    private void SetVisualVisible(int index, bool visible)
    {
        VisualProfile visual = visuals[index];
        if (visual == null || visual.root == null)
        {
            return;
        }

        bool visualRootIsOwner = visual.root == gameObject;
        if (!visualRootIsOwner)
        {
            visual.root.SetActive(visible);
        }

        Renderer[] renderers = visual.root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsRendererOwnedByAnotherVisual(renderer.transform, index) || IsMapIconRenderer(renderer))
            {
                continue;
            }

            renderer.enabled = visible;
        }

        Animator visualAnimator = ResolveAnimator(visual);
        if (visualAnimator != null && visualAnimator != (movement != null ? movement.animator : null))
        {
            visualAnimator.enabled = visible;
        }
    }


    private static bool IsMapIconRenderer(Renderer renderer)
    {
        return renderer != null && renderer.GetComponentInParent<AaMapIcon>(true) != null;
    }
    private bool IsRendererOwnedByAnotherVisual(Transform rendererTransform, int ownerIndex)
    {
        for (int i = 0; i < visuals.Length; i++)
        {
            if (i == ownerIndex || visuals[i] == null || visuals[i].root == null || visuals[i].root == gameObject)
            {
                continue;
            }

            if (rendererTransform.IsChildOf(visuals[i].root.transform))
            {
                return true;
            }
        }

        return false;
    }

    private Animator ResolveAnimator(VisualProfile visual)
    {
        if (visual == null)
        {
            return null;
        }

        if (visual.animator != null)
        {
            return visual.animator;
        }

        return visual.root != null ? visual.root.GetComponentInChildren<Animator>(true) : null;
    }

    private void DisableInactiveAnimators(Animator activeAnimator)
    {
        if (visuals == null)
        {
            return;
        }

        for (int i = 0; i < visuals.Length; i++)
        {
            if (i == activeVisualIndex || visuals[i] == null)
            {
                continue;
            }

            Animator visualAnimator = ResolveAnimator(visuals[i]);
            if (visualAnimator != null && visualAnimator != activeAnimator)
            {
                visualAnimator.enabled = false;
            }
        }
    }

    private bool IsValidIndex(int index)
    {
        return visuals != null && index >= 0 && index < visuals.Length && visuals[index] != null;
    }

    private bool IsGameplayInputBlocked()
    {
        return Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked;
    }
}




