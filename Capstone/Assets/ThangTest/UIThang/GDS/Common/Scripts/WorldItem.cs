using UnityEngine;
using GDS.Core;
using UnityEngine.EventSystems;
using System;
using System.Collections;


namespace GDS.Common.Scripts {


    /// <summary>
    /// A world item is created when you discard items from an inventory (behavior handled in another script). 
    /// It can be picked up by clicking on it.
    /// </summary>
    public class WorldItem : MonoBehaviour, IWorldItem, IPointerDownHandler/*, IPointerEnterHandler, IPointerExitHandler*/ {

        [Tooltip("Used when item does not have a prefab")]
        [SerializeField] GameObject DefaultPrefab;
        [SerializeField] ParticleSystem SpawnVFX;
        [SerializeField] ParticleSystem DespawnVFX;

        Item item;
        public Item Item { get => item; set => Init(value); }
        public GameObject GameObject => gameObject;
        public event Action<IWorldItem> OnClick = (_) => { };

        public void OnPointerDown(PointerEventData eventData) => OnClick.Invoke(this);

        public void Init(Item item) {
            this.item = item;
            GameObject prefab = DefaultPrefab;
            if (item.Base is IHasPrefab itemBase && itemBase.Prefab != null) { prefab = itemBase.Prefab; }

            var itemInstance = Instantiate(prefab, transform);
            var renderer = itemInstance.GetComponent<Renderer>();
            if (renderer is SpriteRenderer s) {
                itemInstance.AddComponent<HighlightSpriteOnHover>();
                s.sprite = item.Icon;
            } else {
                itemInstance.AddComponent<HighlightObjectOnHover>();
            }

            StartCoroutine(ScaleUp(transform, 0.15f));
            if (SpawnVFX == null) return;
            Instantiate(SpawnVFX, transform.position, Quaternion.identity);
        }


        public void Despawn() {
            float duration = 0.05f;
            StartCoroutine(ScaleDown(transform, duration));
            Destroy(gameObject, duration);
            if (DespawnVFX == null) return;
            Instantiate(DespawnVFX, transform.position, Quaternion.identity);
        }

        IEnumerator ScaleUp(Transform transform, float duration) {
            Vector3 startScale = Vector3.zero;
            Vector3 targetScale = Vector3.one;
            float elapsed = 0f;

            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = Easing.OutBack(t);
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
        }

        IEnumerator ScaleDown(Transform transform, float duration) {
            Vector3 startScale = Vector3.one;
            Vector3 targetScale = Vector3.zero;
            float elapsed = 0f;

            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = Easing.InCubic(t);
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
        }


    }
}