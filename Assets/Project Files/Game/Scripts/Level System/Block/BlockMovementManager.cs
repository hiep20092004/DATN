using System;
using System.Collections.Generic;
using Lofelt.NiceVibrations;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class BlockMovementManager
    {
        private static readonly int GROUND_LAYER_MASK = 1 << GameLayer.LAYER_GROUND;
        private MovementConfigData configData;

        private float currentWaterRotation;
        private float currentAmplitude;
        private float currentFrequency;
        private float defaultWaterRotate;
        private float defaultAmplitude;
        private float defaultFrequency;

        private Vector3 lastPosition;
        private Camera cachedMainCamera;
        private bool hasWaterShaderProperties;
        private float cachedWaterFillAmount;

        private float currentModelY;
        private float targetModelY;
        private bool isModelYTransitioning;

        private Transform mainLiftTransform;
        private float mainLiftBaseOffset;

        private LevelRepresentation levelRepresentation;

        public LevelBlockBehavior BlockBehavior { get; private set; }

        private Vector3 touchOffset;

        // Finger-intent position (post effect overrides, board-clamped), refreshed every physics tick.
        // On release we snap toward this instead of the rigidbody position, so a fast flick still
        // lands on the cell the finger lifted over even if the physics body is lagging behind.
        private Vector3 lastDesiredPosition;

        private Vector3 snapTargetPosition;
        private float snapMoveSpeed;
        private LevelBlockBehavior snapBlock;
        private Rigidbody snapRigidbody;
        private bool releaseAfterSnap;

        private float snapPreviousDistanceSq;
        private int snapStallTicks;
        private bool snapRetargeted;

        // Path to the finger cell can be blocked by walls/blocks; after this many ticks without
        // progress, fall back to the nearest cell from the current physical position.
        private const int SNAP_STALL_TICK_LIMIT = 5;
        private const float SNAP_STALL_PROGRESS_EPSILON = 0.0025f;

        /// <summary>Fired after a picked block finishes snapping to grid on release.</summary>
        public Action<LevelBlockBehavior, Vector2Int> BlockSnapCompleted;

        private bool isBlockPicked;
        public bool IsBlockPicked => isBlockPicked;

        private List<LinkedObjectData> linkedObjects = new List<LinkedObjectData>();
        public List<LinkedObjectData> LinkedObjects => linkedObjects;
        
        public BlockMovementManager(BlockConfig config = null)
        {
            SetConfig(config);
        }

        public void SetConfig(BlockConfig config)
        {
            configData = MovementConfigData.From(config);
        }

        public void SetLevelRepresentation(LevelRepresentation levelRepresentation)
        {
            this.levelRepresentation = levelRepresentation;
            cachedMainCamera = Camera.main;
        }

        private void InitializeDefaultWaterValues()
        {
            Material waterMaterial = BlockBehavior?.InstanceWaterMaterial;
            hasWaterShaderProperties = waterMaterial
                                       && waterMaterial.HasProperty(ShaderId.FILL_AMOUNT_SHADER_ID)
                                       && waterMaterial.HasProperty(ShaderId.AMPLITUDE)
                                       && waterMaterial.HasProperty(ShaderId.FREQUENCY)
                                       && waterMaterial.HasProperty(ShaderId.ROTALE_ANGLE);
            if (!hasWaterShaderProperties) return;

            defaultWaterRotate = waterMaterial.GetFloat(ShaderId.ROTALE_ANGLE);
            defaultAmplitude = waterMaterial.GetFloat(ShaderId.AMPLITUDE);
            defaultFrequency = waterMaterial.GetFloat(ShaderId.FREQUENCY);

            cachedWaterFillAmount = waterMaterial.GetFloat(ShaderId.FILL_AMOUNT_SHADER_ID);

            currentWaterRotation = defaultWaterRotate;
            currentAmplitude = defaultAmplitude;
            currentFrequency = defaultFrequency;
        }

        public void FixedUpdate()
        {
            UpdateModelYTransition();

            if (snapRigidbody)
            {
                UpdateSnapMovement();
            }
            
            if (!isBlockPicked)
                return;

            if (!cachedMainCamera)
                cachedMainCamera = Camera.main;
            if (!cachedMainCamera)
                return;

            Ray ray = cachedMainCamera.ScreenPointToRay(InputController.MousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, configData.RaycastDistance, GROUND_LAYER_MASK))
            {
                UpdateBlockMovement(hit.point);
            }
        }

        private Vector3 GetWorldPosFromMouse()
        {
            if (!cachedMainCamera)
                cachedMainCamera = Camera.main;
            if (!cachedMainCamera)
                return BlockBehavior ? BlockBehavior.transform.position : Vector3.zero;

            Ray ray = cachedMainCamera.ScreenPointToRay(InputController.MousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, configData.RaycastDistance, GROUND_LAYER_MASK))
            {
                return hit.point;
            }

            return BlockBehavior.transform.position;
        }

        public void PickObject(LevelBlockBehavior levelBlockBehavior)
        {
            if (isBlockPicked) return;

            // A previous release may still be snapping to grid. Finish it now — while
            // BlockBehavior and linkedObjects still point at that block — so the prior block
            // finalizes cleanly and we never drive two dynamic bodies at once (overlapping
            // velocity-driven blocks shove or tunnel through each other).
            FinishPendingSnap();

            // Finalizing the snap can collect/destroy blocks (gate collection), including the
            // one being picked — re-validate after, not before.
            if (!levelBlockBehavior || levelBlockBehavior.IsFullFill) return;

            isBlockPicked = true;
            BlockBehavior = levelBlockBehavior;

            BlockBehavior.SetKinematicMode(false);
            linkedObjects.Clear();
            if (levelBlockBehavior.MoveMultiplyObjects())
            {
                LinkObjects(levelBlockBehavior.GetLinkedBlocks());
            }

            mainLiftTransform = ResolveLiftTransform(BlockBehavior, out mainLiftBaseOffset);
            currentModelY = configData.ReleasedModelY;
            targetModelY = configData.PickedModelY;
            isModelYTransitioning = true;

            Vector3 movementPosition = GetMovementPosition();
            touchOffset = movementPosition - GetWorldPosFromMouse();
            touchOffset.y = 0f;

            lastPosition = movementPosition;
            lastDesiredPosition = movementPosition;
            InitializeDefaultWaterValues();
        }

        public void Enable(LevelBlockBehavior levelBlockBehavior)
        {
            if (isBlockPicked) return;

            // Same guard as PickObject: settle any in-flight release before driving a new block.
            FinishPendingSnap();

            if (!levelBlockBehavior) return;

            isBlockPicked = true;
            touchOffset = Vector3.zero;
            BlockBehavior = levelBlockBehavior;
            BlockBehavior.SetKinematicMode(false);
            lastDesiredPosition = GetMovementPosition();
        }

        private void LinkObjects(IReadOnlyList<LevelBlockBehavior> levelBlockBehaviors)
        {
            if (!isBlockPicked || levelBlockBehaviors == null) return;

            for (int b = 0; b < levelBlockBehaviors.Count; b++)
            {
                LevelBlockBehavior block = levelBlockBehaviors[b];
                if (!block || block == BlockBehavior)
                    continue;

                bool isAlreadyLinked = false;
                for (int i = 0; i < linkedObjects.Count; i++)
                {
                    if (linkedObjects[i].Block == block)
                    {
                        isAlreadyLinked = true; 
                        break; 
                    }
                }
                if (isAlreadyLinked)
                    continue;

                Transform liftTransform = ResolveLiftTransform(block, out float liftBaseOffset);
                linkedObjects.Add(new LinkedObjectData(block, liftTransform, liftBaseOffset));
                block.SetKinematicMode(false);
            }
        }

        public Vector2Int SnapToClosestPosition(bool snapToCurrentPosition = false)
        {
            if (!isBlockPicked) return Vector2Int.zero;
            if (!BlockBehavior || !BlockBehavior.BlockRigidbody)
            {
                LevelBlockBehavior block = BlockBehavior;
                ClearPickedObject();
                if (block)
                    BlockSnapCompleted?.Invoke(block, Vector2Int.zero);
                return Vector2Int.zero;
            }

            isBlockPicked = false;

            ResetWaterRotation();

            targetModelY = configData.ReleasedModelY;
            isModelYTransitioning = true;
            
            snapBlock = BlockBehavior;
            snapRigidbody = null;
            snapRigidbody = BlockBehavior.BlockRigidbody;
            snapRigidbody.linearVelocity = Vector3.zero;

            // Manual release snaps toward the finger's intended cell, not the lagging physics
            // position: a fast flick can leave the rigidbody cells behind the finger, and the snap
            // phase is velocity-driven so walls/blocks still constrain the catch-up path.
            // Gate auto-collect instead snaps in place at the block's current cell — that is the
            // exact cell NearGate validated as fillable, so chasing the finger (which may already
            // be past the gate on a fast drag) would land the block off the gate and stop it
            // without collecting. Snapping in place guarantees the post-snap collect fills.
            Vector3 snapSource = snapToCurrentPosition ? snapRigidbody.position : lastDesiredPosition;
            int x = Mathf.RoundToInt(snapSource.x);
            int z = Mathf.RoundToInt(snapSource.z);

            // Combine groups move via a shared rigidbody on a pivot that starts at world origin —
            // its coordinates are deltas, not board cells, so the board clamp must not apply
            // (clamping would drag the group back toward its original position). Off-board targets
            // for single blocks resolve via the stall fallback in UpdateSnapMovement anyway, but
            // clamping keeps the common case direct.
            bool usesOwnRigidbody = snapRigidbody.gameObject == BlockBehavior.gameObject;
            if (usesOwnRigidbody && levelRepresentation != null)
            {
                x = Mathf.Clamp(x, 0, levelRepresentation.Size.x - 1);
                z = Mathf.Clamp(z, 0, levelRepresentation.Size.y - 1);
            }
            snapTargetPosition = new Vector3(x, 0, z);

            SetSnapSpeedFor(Vector3.Distance(snapRigidbody.position, snapTargetPosition));
            snapPreviousDistanceSq = float.MaxValue;
            snapStallTicks = 0;
            snapRetargeted = false;

            releaseAfterSnap = false;

            return new Vector2Int(x, z);
        }

        public void ReleaseObject()
        {
            if (snapRigidbody)
            {
                releaseAfterSnap = true;
                return;
            }

            ClearPickedObject();
        }

        private void ClearPickedObject()
        {
            LevelBlockBehavior blockBehavior = BlockBehavior;

            if (blockBehavior)
            {
                blockBehavior.StopBlockPhysics();
            }

            if (!linkedObjects.IsNullOrEmpty())
            {
                foreach (LinkedObjectData linkedObject in linkedObjects)
                {
                    if (!linkedObject.Block || linkedObject.Block == blockBehavior)
                        continue;
                    linkedObject.Block.StopBlockPhysics();
                }
            }

            if (blockBehavior && isModelYTransitioning)
            {
                ApplyModelY(mainLiftTransform, configData.ReleasedModelY, mainLiftBaseOffset);
                if (!linkedObjects.IsNullOrEmpty())
                {
                    foreach (LinkedObjectData lo in linkedObjects)
                    {
                        if (lo.Block) ApplyModelY(lo.LiftTransform, configData.ReleasedModelY, lo.LiftBaseOffset);
                    }
                }
            }

            if (BlockBehavior?.BlockState == BlockState.Normal)
            {
                LevelController.Instance.TryToCollectBlock(blockBehavior);
                if (!linkedObjects.IsNullOrEmpty())
                {
                    foreach (var linkedObject in linkedObjects)
                    {
                        LevelController.Instance.TryToCollectBlock(linkedObject.Block);
                    }
                }
            }
            
            isModelYTransitioning = false;
            mainLiftTransform = null;
            ResetWaterRotation();
            linkedObjects.Clear();
            BlockBehavior = null;
            touchOffset = Vector3.zero;
            isBlockPicked = false;
            snapBlock = null;
            snapRigidbody = null;
            releaseAfterSnap = false;
            snapStallTicks = 0;
            snapRetargeted = false;
            snapPreviousDistanceSq = float.MaxValue;
        }

        private void ResetWaterRotation()
        {
            if (!hasWaterShaderProperties) return;
            Material waterMat = BlockBehavior?.InstanceWaterMaterial;
            if (!waterMat) return;

            currentWaterRotation = defaultWaterRotate;
            currentAmplitude = defaultAmplitude;
            currentFrequency = defaultFrequency;

            waterMat.SetFloat(ShaderId.ROTALE_ANGLE, defaultWaterRotate);
            waterMat.SetFloat(ShaderId.AMPLITUDE, defaultAmplitude);
            waterMat.SetFloat(ShaderId.FREQUENCY, defaultFrequency);
        }

        public void UpdateBlockMovement(Vector3 mouseWorldPos)
        {
            if (!BlockBehavior || !BlockBehavior.BlockRigidbody)
                return;

            Rigidbody rigidbody = BlockBehavior.BlockRigidbody;
            Transform movementTransform = rigidbody.transform;

            float xClamp = Mathf.Clamp(mouseWorldPos.x, 0f, levelRepresentation.Size.x - 1);
            float zClamp = Mathf.Clamp(mouseWorldPos.z, 0f, levelRepresentation.Size.y - 1);
            mouseWorldPos = new Vector3(xClamp, 0, zClamp);

            Vector3 desiredPos = mouseWorldPos + touchOffset;
            Vector3 direction = desiredPos - movementTransform.position;
            direction.y = 0f;

            foreach (BlockEffectBehavior effect in BlockBehavior.Effects)
            {
                if (!effect.IsActive) continue;
                effect.OverrideMovementDirection(ref direction);
            }

            foreach (LinkedObjectData linkedObject in linkedObjects)
            {
                if (!linkedObject.Block) continue;
                foreach (BlockEffectBehavior effect in linkedObject.Block.Effects)
                {
                    if (!effect.IsActive) continue;
                    effect.OverrideMovementDirection(ref direction);
                }
            }

            // Direction is the full remaining offset (post effect-axis overrides), so this is the
            // finger-intent position used as the snap target on release.
            lastDesiredPosition = movementTransform.position + direction;

            // Cover the whole remaining offset this tick when close (no exponential trailing),
            // capped at MovementSpeed when far. Velocity-driven so collisions still resolve.
            float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            rigidbody.linearVelocity = Vector3.ClampMagnitude(direction / dt, configData.MovementSpeed);

            UpdateWaterRotation();
        }

        private void UpdateModelYTransition()
        {
            if (!isModelYTransitioning) return;
            if (!BlockBehavior && !snapBlock) 
            {
                isModelYTransitioning = false;
                return;
            }

            float lerpSpeed = configData.ModelYLerpSpeed;
            currentModelY = Mathf.Lerp(currentModelY, targetModelY, lerpSpeed * Time.fixedDeltaTime);

            if (Mathf.Abs(currentModelY - targetModelY) < 0.001f)
            {
                currentModelY = targetModelY;
                isModelYTransitioning = false;
            }

            ApplyModelY(mainLiftTransform, currentModelY, mainLiftBaseOffset);
            foreach (var linkedObject in linkedObjects)
            {
                if (!linkedObject.Block) continue;
                ApplyModelY(linkedObject.LiftTransform, currentModelY, linkedObject.LiftBaseOffset);
            }
        }

        private void ApplyModelY(Transform liftTransform, float y, float liftBaseOffset)
        {
            if (!liftTransform)
                return;

            var pos = liftTransform.position;
            pos.y = y + liftBaseOffset;
            liftTransform.position = pos;
        }

        // Resolves the transform the pick lift animates (block model, or an effect's own visual) and the
        // offset that maps absolute model-Y onto it so the released state lands on the transform's own rest
        // height (0 for a normal block model, >0 for a container visual sitting above it). Called once at
        // pick while the transform is at rest; the result is cached for the rest of the drag.
        private Transform ResolveLiftTransform(LevelBlockBehavior block, out float baseOffset)
        {
            Transform liftTransform = block ? block.ModelLiftTransform : null;
            baseOffset = (liftTransform ? liftTransform.position.y : 0f) - configData.ReleasedModelY;
            return liftTransform;
        }

        private void SetSnapSpeedFor(float distance)
        {
            float snapDuration = Mathf.Max(configData.MovementSnapDuration, 0.001f);
            // Long catch-up distances (fast flick) are capped at MovementSpeed so the snap phase
            // never moves faster than the drag itself could.
            snapMoveSpeed = Mathf.Min(distance / snapDuration, configData.MovementSpeed);
        }

        private void UpdateSnapMovement()
        {
            if (!snapBlock || !snapRigidbody)
            {
                ClearPickedObject();
                return;
            }

            Rigidbody blockRigidbody = snapRigidbody;
            Vector3 currentPosition = blockRigidbody.position;
            Vector3 toTarget = snapTargetPosition - currentPosition;
            toTarget.y = 0f;

            float snapStopDistance = Mathf.Max(configData.MovementSnapStopDistance, 0.01f);
            bool forceComplete = false;

            if (toTarget.sqrMagnitude > snapStopDistance * snapStopDistance)
            {
                // The finger cell may be physically unreachable (wall/block in the way).
                // No progress for a few ticks → re-target to the nearest cell from where the
                // block actually is; if that stalls too, finish in place instead of hanging.
                if (snapPreviousDistanceSq - toTarget.sqrMagnitude < SNAP_STALL_PROGRESS_EPSILON)
                {
                    snapStallTicks++;
                    if (snapStallTicks >= SNAP_STALL_TICK_LIMIT)
                    {
                        if (snapRetargeted)
                        {
                            forceComplete = true;
                            snapTargetPosition = new Vector3(
                                Mathf.RoundToInt(currentPosition.x), 0,
                                Mathf.RoundToInt(currentPosition.z));
                        }
                        else
                        {
                            snapRetargeted = true;
                            snapStallTicks = 0;
                            snapPreviousDistanceSq = float.MaxValue;
                            snapTargetPosition = new Vector3(
                                Mathf.RoundToInt(currentPosition.x), 0,
                                Mathf.RoundToInt(currentPosition.z));
                            SetSnapSpeedFor(Vector3.Distance(currentPosition, snapTargetPosition));
                            return;
                        }
                    }
                }
                else
                {
                    snapStallTicks = 0;
                }
                snapPreviousDistanceSq = toTarget.sqrMagnitude;

                if (!forceComplete)
                {
                    float distance = toTarget.magnitude;
                    float maxStepSpeed = distance / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
                    float effectiveSpeed = Mathf.Min(snapMoveSpeed, maxStepSpeed);
                    blockRigidbody.linearVelocity = (toTarget / distance) * effectiveSpeed;
                    return;
                }
            }

            CompleteSnap();
        }

        /// <summary>
        /// Lands the snapping block on its grid target, fires release notifications, and finalizes
        /// it. Called both when the snap converges naturally and when a new pick interrupts it, so
        /// the in-flight block always settles cleanly instead of being orphaned mid-snap.
        /// </summary>
        private void CompleteSnap()
        {
            if (!snapRigidbody)
                return;

            snapRigidbody.position = snapTargetPosition;
            snapRigidbody.linearVelocity = Vector3.zero;
            snapRigidbody.angularVelocity = Vector3.zero;

            LevelBlockBehavior completedBlock = snapBlock;
            bool notifyRelease = releaseAfterSnap;
            Vector2Int completedGrid = new Vector2Int(
                Mathf.RoundToInt(snapTargetPosition.x),
                Mathf.RoundToInt(snapTargetPosition.z));

            snapRigidbody = null;
            snapBlock = null;
            Physics.SyncTransforms();

            if (notifyRelease && completedBlock)
                BlockSnapCompleted?.Invoke(completedBlock, completedGrid);

            if (notifyRelease)
                ClearPickedObject();
        }

        // Force the currently snapping block (if any) to land immediately, keeping at most one
        // block under physics control at a time. CompleteSnap teleports, and this early in the
        // snap the finger cell can still be far away or physically unreachable (wall/block in
        // the way) — teleporting there would skip collision resolution entirely and leave the
        // block tunneled through or overlapping others. Land on the nearest cell from the
        // current physical position instead (≤0.5 cell hop, same fallback the stall logic uses).
        private void FinishPendingSnap()
        {
            if (!snapRigidbody)
                return;

            Vector3 currentPosition = snapRigidbody.position;
            snapTargetPosition = new Vector3(
                Mathf.RoundToInt(currentPosition.x), 0f,
                Mathf.RoundToInt(currentPosition.z));
            CompleteSnap();
        }

        private void UpdateWaterRotation()
        {
            if (!hasWaterShaderProperties) return;
            if (!BlockBehavior || !BlockBehavior.IsWaterVisible) return;
            Material waterMat = BlockBehavior.InstanceWaterMaterial;
            if (!waterMat) return;

            if (cachedWaterFillAmount <= 0) return;

            float dt = Time.fixedDeltaTime;
            Vector3 blockPos = GetMovementPosition();
            Vector3 actualMovement = blockPos - lastPosition;
            actualMovement.y = 0;
            float actualSpeed = actualMovement.magnitude / dt;
            float movementX = actualMovement.x / dt;

            lastPosition = blockPos;

            float previousRotation = currentWaterRotation;
            float previousAmplitude = currentAmplitude;
            float previousFrequency = currentFrequency;

            if (actualSpeed > configData.MovementActiveSpeedThreshold)
            {
                float targetRotation;
                float targetAmplitude = configData.WaterAmplitudeOnRotate;
                float targetFrequency = configData.WaterFrequencyOnRotate;

                if (movementX > configData.HorizontalDirectionThreshold)
                {
                    float normalizedSpeed = Mathf.Clamp01(actualSpeed / configData.MovementSpeed);
                    targetRotation = Mathf.Lerp(defaultWaterRotate, configData.WaterRotateToRightMax, normalizedSpeed);
                }
                else if (movementX < -configData.HorizontalDirectionThreshold)
                {
                    float normalizedSpeed = Mathf.Clamp01(actualSpeed / configData.MovementSpeed);
                    targetRotation = Mathf.Lerp(defaultWaterRotate, configData.WaterRotateToLeftMax, normalizedSpeed);
                }
                else
                {
                    targetRotation = defaultWaterRotate;
                    targetAmplitude = defaultAmplitude;
                    targetFrequency = defaultFrequency;
                }

                currentWaterRotation = Mathf.MoveTowards(currentWaterRotation, targetRotation,
                    configData.WaterRotationSpeed * dt);
                currentAmplitude = Mathf.MoveTowards(currentAmplitude, targetAmplitude,
                    configData.WaterAmplitudeLerpSpeed * dt);
                currentFrequency = Mathf.MoveTowards(currentFrequency, targetFrequency,
                    configData.WaterFrequencyLerpSpeed * dt);
            }
            else
            {
                currentWaterRotation = Mathf.MoveTowards(currentWaterRotation, defaultWaterRotate,
                    configData.WaterRotationSpeed * dt);
                currentAmplitude = Mathf.MoveTowards(currentAmplitude, defaultAmplitude,
                    configData.WaterAmplitudeLerpSpeed * dt);
                currentFrequency = Mathf.MoveTowards(currentFrequency, defaultFrequency,
                    configData.WaterFrequencyLerpSpeed * dt);
            }

            if (currentWaterRotation != previousRotation)
                waterMat.SetFloat(ShaderId.ROTALE_ANGLE, currentWaterRotation);
            if (currentAmplitude != previousAmplitude)
                waterMat.SetFloat(ShaderId.AMPLITUDE, currentAmplitude);
            if (currentFrequency != previousFrequency)
                waterMat.SetFloat(ShaderId.FREQUENCY, currentFrequency);
        }

        private Vector3 GetMovementPosition()
        {
            if (!BlockBehavior)
                return Vector3.zero;
            return BlockBehavior.BlockRigidbody ? BlockBehavior.BlockRigidbody.position : BlockBehavior.transform.position;
        }

        public struct LinkedObjectData
        {
            public readonly LevelBlockBehavior Block;
            public readonly Transform LiftTransform;
            public readonly float LiftBaseOffset;

            public LinkedObjectData(LevelBlockBehavior block, Transform liftTransform, float liftBaseOffset)
            {
                Block = block;
                LiftTransform = liftTransform;
                LiftBaseOffset = liftBaseOffset;
            }
        }
    }
}
