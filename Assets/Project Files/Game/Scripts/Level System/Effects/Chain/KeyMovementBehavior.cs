using System;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class KeyMovementBehavior : MonoBehaviour
    {
        [SerializeField] float yOffset = 0.5f;
        [SerializeField] float yDuration = 0.3f;
        [SerializeField] float pushScale = 1.3f;

        [Space]
        [SerializeField] float moveDuration = 0.3f;
        [SerializeField] Vector3 moveOffset;
        [SerializeField] Ease.Type moveEasing;

        [Space]
        [SerializeField] Vector3 baseRotation;
        [SerializeField] float baseRotationDuration = 0.2f;
        [SerializeField] Vector3 targetRotation;
        [SerializeField] float targetRotationDuration = 0.2f;
        [SerializeField] int turnRotation = 0;
#if UNITY_EDITOR
        [Space]
        [SerializeField] Component simulateTarget;
        private Vector3 simulateStartWorldPosition;
        private Quaternion simulateStartWorldRotation;
#endif

        private TweenCaseCollection tweenCaseCollection;

        private IKeyMovementAction keyAction;
        private bool isLinked;
        private bool isMovementRunning;
        
        private void Update()
        {
            if (!isLinked) return;

            if(keyAction == null || !keyAction.IsTargetAvailable)
            {
                tweenCaseCollection?.KillActive();

                Destroy(gameObject);
            }
        }

        public void StartMovement(IKeyMovementAction keyMovementAction)
        {
            if (isMovementRunning)
                return;

            isLinked = true;
            keyAction = keyMovementAction;

            if (keyAction is not { IsTargetAvailable: true })
            {
                tweenCaseCollection?.KillActive();
                Destroy(gameObject);
                return;
            }

            keyAction.OnKeyLinked();
            isMovementRunning = true;

            PlayMovementSequence(() =>
                keyAction is { IsTargetAvailable: true }
                    ? keyAction.TargetPosition
                    : (Vector3?)null,
                () => keyAction?.OnKeyReached(),
                () => keyAction?.OnKeyFinished(),
                true);
        }

        private void PlayMovementSequence(Func<Vector3?> targetResolver, Action onReached, Action onFinished, bool destroyOnFinish)
        {
            tweenCaseCollection?.KillActive();
            tweenCaseCollection = new TweenCaseCollection();

            transform.SetParent(null);
            Vector3 localPosition = transform.localPosition;

            tweenCaseCollection += transform.DOPushScale(pushScale, 1f, yDuration, yDuration * 0.5f);
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
                    keyAction = null;

                    if (destroyOnFinish)
                    {
                        Destroy(gameObject);
                    }
                    return;
                }

                Vector3 lockPosition = resolvedTarget.Value + moveOffset;
                tweenCaseCollection += transform.DORotate(Quaternion.Euler(baseRotation), baseRotationDuration);
                tweenCaseCollection += transform.DOBezierMove(lockPosition, 1.5f, 0, 0, moveDuration).SetEasing(moveEasing).OnComplete(() =>
                {
                    if (!this || !transform)
                        return;

                    onReached?.Invoke();

                    tweenCaseCollection += transform.DORotate(Quaternion.Euler(targetRotation), targetRotationDuration, turnRotation, 0.1f).OnComplete(() =>
                    {
                        if (!this || tweenCaseCollection == null)
                            return;

                        tweenCaseCollection += Tween.DelayedCall(0.2f, () =>
                        {
                            if (!this)
                                return;

                            onFinished?.Invoke();
                            isMovementRunning = false;
                            isLinked = false;
                            keyAction = null;

                            if (destroyOnFinish)
                            {
                                Destroy(gameObject);
                            }
                        });
                    });
                });
            });
        }

#if UNITY_EDITOR
        public void SetSimulateTarget(IKeyMovementAction target)
        {
            simulateTarget = target as Component;
        }

        public bool SimulateInRuntime()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[KeyMovementBehavior] Simulate works only in Play Mode.", this);
                return false;
            }

            if (isLinked || isMovementRunning)
            {
                Debug.LogWarning("[KeyMovementBehavior] This key is already moving.", this);
                return false;
            }

            if (simulateTarget is not IKeyMovementAction movementAction)
            {
                Debug.LogWarning("[KeyMovementBehavior] No simulate target found. Wait for OnCreated runtime or assign one manually.", this);
                return false;
            }

            if (!movementAction.IsTargetAvailable)
            {
                Debug.LogWarning("[KeyMovementBehavior] Simulate target is not available.", this);
                return false;
            }

            tweenCaseCollection?.KillActive();
            simulateStartWorldPosition = transform.position;
            simulateStartWorldRotation = transform.rotation;
            isMovementRunning = true;
            PlayMovementSequence(() =>
                movementAction.IsTargetAvailable
                    ? movementAction.TargetPosition
                    : (Vector3?)null,
                null,
                OnSimulateFinish,
                false);
            return true;
        }
        
        private void OnSimulateFinish()
        {
            transform.position = simulateStartWorldPosition;
            transform.rotation = simulateStartWorldRotation;
        }
#endif

        private void OnDestroy()
        {
            tweenCaseCollection?.KillActive();
        }
    }
}