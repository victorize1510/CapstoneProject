using System;
using System.Collections.Generic;
using Capstone.Game.MapSystem;
using UnityEngine;

[DisallowMultipleComponent]
public class DummyEnemy : MonoBehaviour
{
    private static readonly HashSet<DummyEnemy> activeEnemies = new HashSet<DummyEnemy>();

    [Header("Health")]
    public float maxHealth = 100f;
    public bool autoRevive = true;
    public float reviveDelay = 2.5f;

    [Header("Feedback")]
    public Color idleColor = Color.white;
    public Color hitColor = new Color(1f, 0.35f, 0.2f, 1f);
    public Color deadColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    public float hitFlashTime = 0.12f;
    public bool writeHealthToName = true;

    private Renderer cachedRenderer;
    private Collider[] colliders;
    private string baseName;
    private float health;
    private float reviveAt;
    private float flashUntil;
    private bool alive = true;
    private MapMarker mapMarker;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeRegistry()
    {
        activeEnemies.Clear();
    }

    public static IReadOnlyCollection<DummyEnemy> ActiveEnemies
    {
        get { return activeEnemies; }
    }

    public event Action<GameObject> Defeated;

    public bool IsAlive
    {
        get { return alive; }
    }

    public Vector3 TargetPosition
    {
        get
        {
            Bounds bounds = GetTargetBounds();
            return bounds.center;
        }
    }

    public Vector3 ClosestPoint(Vector3 worldPosition)
    {
        Vector3 bestPoint = TargetPosition;
        float bestSqrDistance = float.PositiveInfinity;

        if (colliders != null)
        {
            foreach (Collider targetCollider in colliders)
            {
                if (targetCollider == null || !targetCollider.enabled)
                {
                    continue;
                }

                Vector3 point = targetCollider.ClosestPoint(worldPosition);
                float sqrDistance = (point - worldPosition).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestPoint = point;
                }
            }
        }

        if (!float.IsInfinity(bestSqrDistance))
        {
            return bestPoint;
        }

        return GetTargetBounds().ClosestPoint(worldPosition);
    }

    private void Awake()
    {
        cachedRenderer = GetComponentInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        baseName = gameObject.name;
        health = Mathf.Max(1f, maxHealth);
        ApplyColor(idleColor);
        RefreshName();
    }

    private void OnEnable()
    {
        activeEnemies.Add(this);
        EnsureMapMarker();
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
    }

    private void OnDestroy()
    {
        activeEnemies.Remove(this);
    }

    private void Update()
    {
        if (!alive)
        {
            if (autoRevive && Time.time >= reviveAt)
            {
                Revive();
            }

            return;
        }

        if (flashUntil > 0f && Time.time >= flashUntil)
        {
            flashUntil = 0f;
            ApplyColor(idleColor);
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(float damage, GameObject source)
    {
        if (!alive)
        {
            return;
        }

        health = Mathf.Max(0f, health - Mathf.Max(0f, damage));
        RefreshName();

        if (health <= 0f)
        {
            Die(source);
            return;
        }

        flashUntil = Time.time + hitFlashTime;
        ApplyColor(hitColor);
    }

    public void Revive()
    {
        alive = true;
        health = Mathf.Max(1f, maxHealth);
        reviveAt = 0f;
        flashUntil = 0f;
        SetCollidersEnabled(true);
        ApplyColor(idleColor);
        RefreshName();
    }

    private void Die(GameObject source)
    {
        alive = false;
        reviveAt = Time.time + reviveDelay;
        SetCollidersEnabled(autoRevive);
        ApplyColor(deadColor);
        RefreshName();
        Defeated?.Invoke(source);
    }

    private Bounds GetTargetBounds()
    {
        if (cachedRenderer != null)
        {
            return cachedRenderer.bounds;
        }

        if (colliders != null && colliders.Length > 0 && colliders[0] != null)
        {
            return colliders[0].bounds;
        }

        return new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
    }

    private void SetCollidersEnabled(bool enabledValue)
    {
        if (colliders == null)
        {
            return;
        }

        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = enabledValue;
            }
        }
    }

    private void ApplyColor(Color color)
    {
        if (cachedRenderer == null)
        {
            return;
        }

        cachedRenderer.material.color = color;
    }

    private void RefreshName()
    {
        if (!writeHealthToName)
        {
            return;
        }

        gameObject.name = alive
            ? baseName + " (" + Mathf.CeilToInt(health) + " HP)"
            : baseName + " (KO)";
    }

    private void EnsureMapMarker()
    {
        if (mapMarker == null)
        {
            mapMarker = GetComponent<MapMarker>();
        }

        if (mapMarker == null)
        {
            mapMarker = gameObject.AddComponent<MapIcon>();
        }

        string id = string.IsNullOrWhiteSpace(baseName) ? gameObject.name : baseName;
        mapMarker.ConfigureRuntime(MapMarkerType.Enemy, id, id, null, default, true, true);
    }
}
