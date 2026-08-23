using System;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class ScissorsMovementBehavior : MonoBehaviour
    {
        [SerializeField] MeshRenderer leftHalfMeshRenderer;
        [SerializeField] MeshRenderer rightHalfMeshRenderer;

        [Space]
        [SerializeField] float yOffset = 0.5f;
        [SerializeField] float yDuration = 0.3f;

        [Space]
        [SerializeField] float moveDuration = 0.3f;
        [SerializeField] Vector3 moveOffset = new Vector3(0, 0.26f, 0.19f);
        [SerializeField] float rotationDuration = 0.3f;
        [SerializeField] Vector3 moveRotation = new Vector3(-54, 0, 0);
        [SerializeField] Vector3 moveScale = Vector3.one;
        [SerializeField] float scaleDuration = 0.3f;

        [Space] 
        [SerializeField] float baseRotation = 25f;
        [SerializeField] float baseRotationDuration = 0.2f;

        [Space]
        [SerializeField] float targetRotation = -12f;
        [SerializeField] float targetRotationDuration = 0.2f;
#if UNITY_EDITOR
        [Space]
        [SerializeField] RopeBehavior simulateTarget;
        private Transform simulateStartParent;
        private Vector3 simulateStartLocalPosition;
        private Quaternion simulateStartLocalRotation;
        private Vector3 simulateStartLocalScale;
        private Quaternion simulateStartLeftHalfLocalRotation;
        private Quaternion simulateStartRightHalfLocalRotation;
        private bool hasSimulateStartState;
#endif

        private TweenCaseCollection tweenCaseCollection;
        private RopeBehavior linkedRopeBehavior;
        private bool isLinked;
        private bool isMovementRunning;


        public void Init()
        {
#if UNITY_EDITOR
            CacheSimulateStartStateIfNeeded();
#endif
        }

        private void Update()
        {
            if (!isLinked) return;

            if (linkedRopeBehavior == null)
            {
                tweenCaseCollection?.KillActive();

                Destroy(gameObject);
            }
        }

        public void StartMovement(RopeBehavior ropeBehavior)
        {
            if (isMovementRunning)
                return;

            isLinked = true;
            linkedRopeBehavior = ropeBehavior;

            if (linkedRopeBehavior == null)
            {
                tweenCaseCollection?.KillActive();
                Destroy(gameObject);
                return;
            }

            linkedRopeBehavior.OnRopeLinked();
            isMovementRunning = true;

            PlayMovementSequence(
                () => linkedRopeBehavior != null ? linkedRopeBehavior.transform.position + moveOffset : (Vector3?)null,
                () => linkedRopeBehavior?.OnRopeCut(),
                () =>
                {
                    isMovementRunning = false;
                    isLinked = false;
                    linkedRopeBehavior = null;
                },
                true);
        }

        private void PlayMovementSequence(Func<Vector3?> targetResolver, Action onCut, Action onFinished, bool destroyOnFinish)
        {
            tweenCaseCollection?.KillActive();
            tweenCaseCollection = new TweenCaseCollection();

            transform.SetParent(null);
            Vector3 localPosition = transform.localPosition;

            tweenCaseCollection += transform.DOLocalMoveY(localPosition.y + yOffset, yDuration).OnComplete(() =>
            {
                if (!this || !transform)
                    return;

                Vector3? resolvedTarget = targetResolver?.Invoke();
                if (!resolvedTarget.HasValue)
                {
                    tweenCaseCollection?.KillActive();
                    isMovementRunning = false;
                    isLinked = false;
                    linkedRopeBehavior = null;

                    if (destroyOnFinish)
                        Destroy(gameObject);

                    return;
                }

                tweenCaseCollection += transform.DORotate(Quaternion.Euler(moveRotation), rotationDuration);
                tweenCaseCollection += transform.DOScale(moveScale, scaleDuration);
                tweenCaseCollection += transform.DOMove(resolvedTarget.Value, moveDuration).OnComplete(() =>
                {
                    if (!this || !transform)
                        return;
                    tweenCaseCollection += leftHalfMeshRenderer.DORotate(Quaternion.Euler(0, baseRotation, 0), baseRotationDuration);
                    tweenCaseCollection += rightHalfMeshRenderer.DORotate(Quaternion.Euler(0, -baseRotation, 0), baseRotationDuration).OnComplete(() =>
                    {
                        if (!this || !transform)
                            return;
                        float cutDelay = Mathf.Max(0f, targetRotationDuration * 0.5f);
                        tweenCaseCollection += Tween.DelayedCall(cutDelay, () =>
                        {
                            if (!this)
                                return;

                            onCut?.Invoke();
                        });
                        
                        tweenCaseCollection += leftHalfMeshRenderer.DORotate(Quaternion.Euler(0, targetRotation, 0), targetRotationDuration);
                        tweenCaseCollection += rightHalfMeshRenderer.DORotate(Quaternion.Euler(0, -targetRotation, 0), targetRotationDuration).OnComplete(() =>
                        {
                            if (!this)
                                return;

                            onFinished?.Invoke();

                            if (destroyOnFinish)
                                Destroy(gameObject);
                        });
                    });
                });
            });
        }

#if UNITY_EDITOR
        public void SetSimulateTarget(RopeBehavior ropeBehavior)
        {
            simulateTarget = ropeBehavior;
        }

        public bool SimulateInRuntime()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ScissorsMovementBehavior] Simulate works only in Play Mode.", this);
                return false;
            }

            if (isLinked || isMovementRunning)
            {
                Debug.LogWarning("[ScissorsMovementBehavior] Scissors is already moving.", this);
                return false;
            }

            if (!simulateTarget)
            {
                Debug.LogWarning("[ScissorsMovementBehavior] No simulate target found. Wait for OnCreated runtime or assign one manually.", this);
                return false;
            }

            if (simulateTarget.IsLinked)
            {
                Debug.LogWarning("[ScissorsMovementBehavior] Simulate target rope is already cut.", this);
                return false;
            }

            CacheSimulateStartStateIfNeeded();
            ResetToSimulateStartState();
            tweenCaseCollection?.KillActive();
            isMovementRunning = true;

            PlayMovementSequence(
                () => simulateTarget ? simulateTarget.transform.position + moveOffset : (Vector3?)null,
                null,
                OnSimulateFinish,
                false);

            return true;
        }

        private void OnSimulateFinish()
        {
            ResetToSimulateStartState();
            isMovementRunning = false;
            isLinked = false;
            linkedRopeBehavior = null;
        }

        private void CacheSimulateStartStateIfNeeded()
        {
            if (hasSimulateStartState)
                return;

            simulateStartParent = transform.parent;
            simulateStartLocalPosition = transform.localPosition;
            simulateStartLocalRotation = transform.localRotation;
            simulateStartLocalScale = transform.localScale;
            simulateStartLeftHalfLocalRotation = leftHalfMeshRenderer.transform.localRotation;
            simulateStartRightHalfLocalRotation = rightHalfMeshRenderer.transform.localRotation;
            hasSimulateStartState = true;
        }

        private void ResetToSimulateStartState()
        {
            if (!hasSimulateStartState)
                return;

            transform.SetParent(simulateStartParent, false);
            transform.localPosition = simulateStartLocalPosition;
            transform.localRotation = simulateStartLocalRotation;
            transform.localScale = simulateStartLocalScale;
            leftHalfMeshRenderer.transform.localRotation = simulateStartLeftHalfLocalRotation;
            rightHalfMeshRenderer.transform.localRotation = simulateStartRightHalfLocalRotation;
        }
#endif

        private void OnDisable()
        {
            tweenCaseCollection?.KillActive();
        }
    }
}