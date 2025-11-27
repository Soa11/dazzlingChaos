// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Bindings;
    using Bindings.TransformBindings;
    using Coherence.Common;
    using Coherence.Debugging;
    using Entities;
    using Log;
    using SimulationFrame;
    using UnityEngine;
    using Logger = Log.Logger;

    // IMPORTANT: This class is on a hot path. All the `foreach` loops
    // have been intentionally replaced with `for` loops to be faster
    // when incremental GC is enabled (due to wbarrier_conc() in foreach).
    internal class CoherenceSyncUpdater : ICoherenceSyncUpdater
    {
        public Logger logger { get; set; }

        public bool TaggedForNetworkedDestruction { get; set; }

        public bool ChangedConnection => connectedEntityChanged;
        public Entity NewConnection => newConnectedEntity;

        private bool hasTriggeredMissingBridgeWarning;
        private string lastSerializedCoherenceUUID = string.Empty;
        private string lastSerializedTag = string.Empty;

        readonly private ICoherenceSync coherenceSync;
        readonly private IClient client;

        private const float minTimeBetweenAdoptionRequests = 0.5f;

        readonly private List<Binding> bindingsWithCallbacks = new();
        readonly private List<Binding> valueBindings = new();
        readonly private List<List<Binding>> valueBindingGroups = new();
        readonly private List<Binding> interpolatedBindings = new();
        readonly private Dictionary<string, List<Binding>> valueBindingsByComponent = new();
        private PositionBinding positionBinding;
        private RotationBinding rotationBinding;
        private ScaleBinding scaleBinding;

        private bool updateConnectedEntityInSamplingLoop = true;
        private bool initialSampleDone = false;
        private bool initialSyncDone = false;
        private bool connectedEntityChanged;
        private Entity newConnectedEntity;
        private bool shouldRaiseOnNetworkedParentChanged;

        readonly private List<ICoherenceComponentData> queuedUpdates = new();

        public CoherenceSyncUpdater(ICoherenceSync coherenceSync, IClient client)
        {
            logger = Log.GetLogger<CoherenceSyncUpdater>(coherenceSync);

            this.coherenceSync = coherenceSync;
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            UpdateValueBindings();
        }

        public void InterpolateBindings()
        {
            if (!EnsureEntityInitializedAndReady())
            {
                return;
            }

            if (!coherenceSync.EntityState.HasStateAuthority)
            {
                PerformInterpolationOnAllBindings();
            }
        }

        public void InvokeCallbacks()
        {
            if (bindingsWithCallbacks.Count == 0)
            {
                return;
            }

            if (!EnsureEntityInitializedAndReady())
            {
                return;
            }

            if (coherenceSync.EntityState.HasStateAuthority)
            {
                return;
            }

            InvokeValueSyncCallbacksOnAllBindings();

            if (shouldRaiseOnNetworkedParentChanged)
            {
                shouldRaiseOnNetworkedParentChanged = false;
                coherenceSync.RaiseOnConnectedEntityChanged();
            }
        }

        public void SampleBindings(double time)
        {
            if (!EnsureEntityInitializedAndReady())
            {
                return;
            }

            if (coherenceSync.EntityState.HasStateAuthority)
            {
                if (updateConnectedEntityInSamplingLoop)
                {
                    // This might look wrong, but we need to sample (and thus also "send") the connected
                    // entity in the same loop as the position (and rotation and scale) bindings.
                    // This is important in case the bindings are sampled in the FixedUpdate loop,
                    // which can be invoked multiple times per frame, thus making multiple samples
                    // in the different coordinate system (under a new parent), before we can
                    // sample the new parent (and transform the samples coordinate system).
                    coherenceSync.SendConnectedEntity(time);
                }

                SampleAllBindings(time);
            }
        }

        public void SyncAndSend(double time, AbsoluteSimulationFrame simulationFrame)
        {
            if (!EnsureEntityInitializedAndReady())
            {
                return;
            }

            if (coherenceSync.EntityState.HasStateAuthority)
            {
                SendTag();

                // Ensure all bindings are flushed during the first update,
                // to take any changes happened during this frame since creation of the entity.
                if (!initialSampleDone)
                {
                    SampleAllBindings(time);
                }

                // If we didn't update the connected entity in the same update as
                // we updated the position, rotation or scale (because none of them are interpolated),
                // then we need to send it now.
                if (!updateConnectedEntityInSamplingLoop)
                {
                    coherenceSync.SendConnectedEntity(time);
                }

                var forceSerialize = !initialSyncDone;
                SendComponentUpdates(time, simulationFrame, forceSerialize);
            }
            else
            {
                coherenceSync.ValidateConnectedEntity();
            }

            if (coherenceSync.EntityState.HasInputAuthority)
            {
                if (coherenceSync.Input != null && !coherenceSync.Input.UseFixedSimulationFrames)
                {
                    coherenceSync.BakedScript.SendInputState();
                }
            }

            ProcessOrphanedBehavior();
            ProcessInitialSync();
        }

        public void GetComponentUpdates(List<ICoherenceComponentData> updates, double currentTime, AbsoluteSimulationFrame simulationFrame, bool forceSerialize = false)
        {
            // A dirty dirty hack. Bindings are now actually sampled at the end of a FixedUpdate
            // in case the InterpolationLocationConfig is set to FixedUpdate. But, since UnityEngine.Time.timeAsDouble
            // actually increases between FixedUpdate and Update, when we try to serialize a binding and interpolate
            // the sampled values, the NetworkTime.ClientSimulationFrame will most of the time be ahead of the latest
            // sample time. So we will constantly have to overshoot/extrapolate. This line decreases the simFrame by one
            // so we always interpolate between existing samples, and will unfortunately lead to a bigger latency.
            // This line should be removed when all of these are implemented:
            //  - Each binding having its own simFrame instead of sharing one: https://github.com/coherence/unity/issues/4332
            //  - Bindings which are sampled in FixedUpdate should be serialized with a simFrame from the last FixedUpdate: https://github.com/coherence/engine/issues/2070
            if (coherenceSync.InterpolationLocationConfig == CoherenceSync.InterpolationLoop.FixedUpdate)
            {
                simulationFrame -= 1;
            }

            var invalidBindings = false;

            var groupCount = valueBindingGroups.Count;
            for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                var group = valueBindingGroups[groupIndex];
                var groupComponent = group[0];

                ICoherenceComponentData update = null;
                uint stopMask = 0;

                var bindingCount = group.Count;
                for (var bindingIndex = 0; bindingIndex < bindingCount; bindingIndex++)
                {
                    var binding = group[bindingIndex];

                    if (!forceSerialize)
                    {
                        if (!binding.IsReadyToSample(currentTime))
                        {
                            continue;
                        }
                    }

                    bool dirty;
                    bool justStopped;

                    try
                    {
                        binding.IsDirty(simulationFrame, out dirty, out justStopped);
                    }
                    catch (Exception exception)
                    {
                        if (!binding.IsValid)
                        {
                            var rootObjectName = groupComponent.coherenceSync?.name;
                            logger.Error(Error.ToolkitSyncUpdateInvalidBindingGroup,
                                $"Invalid binding on \"{rootObjectName}\" for component group \"{groupComponent.CoherenceComponentName}\".\nDid you delete a component or child object of a synced gameObject?");
                        }
                        else
                        {
                            logger.Error(Error.ToolkitBindingIsDirtyException,
                                $"We can't check if the binding '{binding.Name}' is dirty",
                                ("context", binding.unityComponent),
                                ("error", exception.ToString()));
                        }

                        invalidBindings = true;
                        break;
                    }

                    if (justStopped)
                    {
                        stopMask |= binding.FieldMask;
                    }

                    // When the binding is just stopped,
                    // we send the last sample along with the
                    // stop state even if it's not dirty.
                    if (!dirty && !justStopped)
                    {
                        continue;
                    }

                    update ??= groupComponent.CreateComponentData();
                    update = SerializeBinding(binding, update,
                        simulationFrame);
                    update.FieldsMask |= binding.FieldMask;
                }

                if (update != null && update.FieldsMask != 0)
                {
                    update.StoppedMask = stopMask;

                    updates.Add(update);
                }
            }

            if (coherenceSync.EntityState != null && coherenceSync.EntityState.CoherenceUUID != lastSerializedCoherenceUUID)
            {
                var update = Impl.GetRootDefinition().GenerateCoherenceUUIDData(coherenceSync.EntityState.CoherenceUUID, simulationFrame);

                updates.Add(update);

                lastSerializedCoherenceUUID = coherenceSync.EntityState.CoherenceUUID;
            }

            if (invalidBindings)
            {
                UpdateValueBindings();
            }
        }

        public void PerformInterpolationOnAllBindings()
        {
            this.ApplyConnectedEntityChanges();

            for (var i = 0; i < valueBindings.Count; i++)
            {
                var binding = valueBindings[i];

                DbgAssert.ThatFmt(binding.IsValid,
                    "Binding is not valid: {0} on {1}", binding.FullName,
                    coherenceSync.name);

                if (!binding.IsCurrentlyPredicted())
                {
                    binding.Interpolate(coherenceSync.CoherenceBridge
                        .NetworkTimeAsDouble);
                }
            }
        }

        public void SampleAllBindings(double time)
        {
            initialSampleDone = true;

            for (var i = 0; i < interpolatedBindings.Count; i++)
            {
                DbgAssert.ThatFmt(interpolatedBindings[i].IsValid,
                    "Binding is not valid: {0} on {1}", interpolatedBindings[i].FullName,
                    coherenceSync.name);
                interpolatedBindings[i].SampleValue(time);
            }
        }

        private void InvokeValueSyncCallbacksOnAllBindings()
        {
            for (var i = 0; i < bindingsWithCallbacks.Count; i++)
            {
                var binding = bindingsWithCallbacks[i];
                binding.InvokeValueSyncCallback();
            }
        }

        public void ClearAllSampleTimes()
        {
            for (var i = 0; i < valueBindings.Count; i++)
            {
                var binding = valueBindings[i];
                binding.ClearSampleTime();
            }
        }

        public void OnConnectedEntityChanged()
        {
            for (var i = 0; i < valueBindings.Count; i++)
            {
                var binding = valueBindings[i];
                binding.OnConnectedEntityChanged();
            }
        }

        /// <summary>
        ///     Sends changes on all bindings manually instead of waiting for the update.
        ///     External use only when the CoherenceSync behaviour is disabled.
        ///     The sending of the packet containing these changes is still dependant on the packet send rate.
        ///     Do no call before the entity has been registered with the client, which will happen after the
        ///     First update after the client is connected and the CoherenceSync behaviour is enabled.
        ///     If called before the entity is registered with the client updates will be lost.
        /// </summary>
        public void ManuallySendAllChanges(bool sampleValuesBeforeSending)
        {
            if (coherenceSync.EntityState?.HasStateAuthority ?? true)
            {
                ClearAllSampleTimes();

                var time = coherenceSync.CoherenceBridge.NetworkTime.TimeAsDouble;
                var simulationFrame = coherenceSync.CoherenceBridge.NetworkTime.ClientSimulationFrame;

                if (sampleValuesBeforeSending)
                {
                    SampleAllBindings(time);
                }

                SendComponentUpdates(time, simulationFrame);
            }
        }

        public void ApplyComponentDestroys(HashSet<uint> destroyedComponents)
        {
            foreach (var componentId in destroyedComponents)
            {
                if (componentId == Impl.GetConnectedEntityComponentIdInternal())
                {
                    // Destroying the ConnectedEntity component will cause the GameObject to detach from its parent.
                    if (coherenceSync.ConnectedEntity)
                    {
                        connectedEntityChanged = true;
                        newConnectedEntity = Entity.InvalidRelative;
                    }
                }
                else
                {
                    logger.Warning(Warning.ToolkitSyncUpdateDestroyNotSupported,
                        $"Destroy component is not supported: {coherenceSync.name} ComponentType: {componentId}");
                }
            }
        }

        public void ApplyComponentUpdates(ComponentUpdates componentUpdates)
        {
            var currentOrigin = this.coherenceSync.CoherenceBridge.GetFloatingOrigin();
            var floatingOriginDelta = componentUpdates.FloatingOrigin - currentOrigin;
            if (!floatingOriginDelta.IsWithinRange(float.MaxValue))
            {
                logger.Warning(Warning.CoreInConnectionFloatingOriginDelta,
                    ("current origin", currentOrigin),
                    ("received origin", componentUpdates.FloatingOrigin));

                floatingOriginDelta = Vector3d.zero;
            }

            for (var i = 0; i < componentUpdates.Store.SortedValues.Count; i++)
            {
                ApplySingleUpdate(componentUpdates.Store.SortedValues[i], floatingOriginDelta.ToUnityVector3());
            }

            // If we get any updates then this is a replicated entity and we can set the
            // initial sample as sampled since that's what we've received.
            initialSampleDone = true;
        }

        private void ApplySingleUpdate(ComponentChange change, Vector3 floatingOriginDelta)
        {
            var newComponentData = change.Data;
            var componentTypeId = newComponentData.GetComponentType();
            var componentName = Impl.ComponentNameFromTypeId(componentTypeId);
            var clientFrame = coherenceSync.CoherenceBridge.NetworkTime?.ClientSimulationFrame ?? default;

            logger.Trace($"Comp: {componentTypeId} Frame: {clientFrame}");

            if (ApplyInternalUpdate(componentName, newComponentData))
            {
                return;
            }

            if (!valueBindingsByComponent.TryGetValue(componentName, out var group))
            {
                logger.Warning(Warning.ToolkitSyncUpdateMissingComponent,
                    $"We can't find any binding for component '{componentName}' when receiving a component update.");
                return;
            }

            for (var i = 0; i < group.Count; i++)
            {
                var binding = group[i];
                var hasChanges =
                    (newComponentData.FieldsMask & binding.FieldMask) != 0;

                if (hasChanges)
                {
                    binding.ReceiveComponentData(newComponentData, clientFrame,
                        floatingOriginDelta);
                }
            }
        }

        private bool ApplyInternalUpdate(string componentName, ICoherenceComponentData newComponentData)
        {
            switch (componentName)
            {
                case "UniqueID":
                    coherenceSync.EntityState.CoherenceUUID = lastSerializedCoherenceUUID = Impl.GetRootDefinition().ExtractCoherenceUUID(newComponentData);
                    return true;
                case "ConnectedEntity":
                    newConnectedEntity = Impl.GetRootDefinition().ExtractConnectedEntityID(newComponentData);
                    connectedEntityChanged = true;
                    return true;
                case "Tag":
                    coherenceSync.CoherenceTag = lastSerializedTag = Impl.GetRootDefinition().ExtractCoherenceTag(newComponentData);
                    return true;
                case "Scene":
                    throw new Exception("Scene updates should be filtered out by the replication server.");
                case "Persistence":
                case "Connection":
                case "Global":
                case "GlobalQuery":
                case "WorldPositionQuery":
                case "TagQuery":
                case "WorldPositionComponent":
                case "ArchetypeComponent":
                case "ConnectionScene":
                case "PreserveChildren":
                    return true;
            }

            return false;
        }

        public void ApplyConnectedEntityChanges()
        {
            coherenceSync.ApplyNodeBindings();

            if (connectedEntityChanged)
            {
                if (coherenceSync.ConnectedEntityChanged(newConnectedEntity, out var didChangeParent))
                {
                    connectedEntityChanged = false;
                    shouldRaiseOnNetworkedParentChanged |= didChangeParent;
                }
            }
        }

        private bool EnsureEntityInitializedAndReady()
        {
            if (TaggedForNetworkedDestruction)
            {
                return false;
            }

            DbgAssert.ThatFmt(coherenceSync.CoherenceBridge != null, ref hasTriggeredMissingBridgeWarning,
                "No CoherenceBridge instance was found, '{0}' will not be able to sync.", coherenceSync.name);
            return true;
        }

        private void ProcessOrphanedBehavior()
        {
            if (coherenceSync.EntityState == null || !coherenceSync.EntityState.IsOrphaned)
            {
                return;
            }

            switch (coherenceSync.OrphanedBehaviorConfig)
            {
                case CoherenceSync.OrphanedBehavior.DoNothing:
                    break;
                case CoherenceSync.OrphanedBehavior.AutoAdopt:
                    if (Time.time - coherenceSync.EntityState.LastTimeRequestedOrphanAdoption > minTimeBetweenAdoptionRequests)
                    {
                        coherenceSync.CoherenceBridge.AuthorityManager.Adopt(coherenceSync.EntityState);
                    }
                    break;
            }
        }

        private void ProcessInitialSync()
        {
            if (initialSyncDone)
            {
                return;
            }

            initialSyncDone = true;

            if (valueBindings.Exists(b => b.SyncMode == SyncMode.CreationOnly))
            {
                UpdateValueBindings();
            }
        }

        private void UpdateValueBindings()
        {
            interpolatedBindings.Clear();
            bindingsWithCallbacks.Clear();
            valueBindings.Clear();
            valueBindingGroups.Clear();
            valueBindingsByComponent.Clear();

            for (var i = 0; i < coherenceSync.Bindings.Count; i++)
            {
                var b = coherenceSync.Bindings[i];

                if (b is null || !b.IsValid || b.IsMethod)
                {
                    continue;
                }

                if (initialSyncDone && b.SyncMode == SyncMode.CreationOnly)
                {
                    continue;
                }

                valueBindings.Add(b);

                if (b.IsInterpolated)
                {
                    interpolatedBindings.Add(b);
                }

                if (b.HasValueChangedCallback())
                {
                    bindingsWithCallbacks.Add(b);
                }
            }

            foreach (var group in valueBindings.GroupBy(b => b.CoherenceComponentName))
            {
                foreach (var binding in group)
                {
                    if (!valueBindingsByComponent.ContainsKey(binding.CoherenceComponentName))
                    {
                        var groupList = new List<Binding>();
                        valueBindingsByComponent.Add(binding.CoherenceComponentName, groupList);
                        valueBindingGroups.Add(groupList);
                    }

                    valueBindingsByComponent[binding.CoherenceComponentName].Add(binding);
                }
            }

            positionBinding = (PositionBinding)coherenceSync.Bindings.FirstOrDefault(b => b is PositionBinding);
            rotationBinding = (RotationBinding)coherenceSync.Bindings.FirstOrDefault(b => b is RotationBinding);
            scaleBinding = (ScaleBinding)coherenceSync.Bindings.FirstOrDefault(b => b is ScaleBinding);

            // We need to update the connected entity in the same update as
            // we update the position, rotation or scale if any of them are interpolated,
            // to ensure the samples are in the correct coordinate system.
            updateConnectedEntityInSamplingLoop = positionBinding is {IsInterpolated: true}
                || rotationBinding is {IsInterpolated: true}
                || scaleBinding is {IsInterpolated: true};
        }

        private ICoherenceComponentData SerializeBinding(Binding binding, ICoherenceComponentData inst, AbsoluteSimulationFrame simulationFrame)
        {
            try
            {
                return binding.WriteComponentData(inst, simulationFrame);
            }
            catch (Exception e)
            {
                if (!binding.IsValid)
                {
                    logger.Warning(Warning.ToolkitSyncUpdateBindingNull,
                        $"Invalid binding {binding.Name} on \"{binding.coherenceSync?.name}\".\nDid you delete a component or child object of a synced gameObject?");
                    return default;
                }

                logger.Error(Error.ToolkitSyncUpdateException,
                    $"We can't serialize the binding '{binding.Name}'",
                    ("context", binding.unityComponent),
                    ("error", e.ToString()));
                return default;
            }
        }

        public void SendTag()
        {
            if (coherenceSync.CoherenceTag == lastSerializedTag)
            {
                return;
            }

            if (string.IsNullOrEmpty(coherenceSync.CoherenceTag))
            {
                Impl.RemoveTag(client, coherenceSync.EntityState.EntityID);
            }
            else
            {
                Impl.UpdateTag(client, coherenceSync.EntityState.EntityID, coherenceSync.CoherenceTag,
                    coherenceSync.CoherenceBridge.NetworkTime.ClientSimulationFrame);
            }

            lastSerializedTag = coherenceSync.CoherenceTag;
        }

        private void SendComponentUpdates(double time, AbsoluteSimulationFrame simulationFrame, bool forceSerialize = false)
        {
            if (!client.CanSendUpdates(coherenceSync.EntityState.EntityID))
            {
                // Don't process this change until the client allows it.
                // Likely in the middle of an authority update.
                return;
            }

            queuedUpdates.Clear();

            GetComponentUpdates(queuedUpdates, time, simulationFrame, forceSerialize);
            if (queuedUpdates.Count > 0)
            {
                client.UpdateComponents(coherenceSync.EntityState.EntityID, queuedUpdates.ToArray());
            }
        }

        public bool TryFlushPosition(bool sampleValueBeforeSending)
        {
            if (positionBinding == null)
            {
                return false;
            }

            positionBinding.MarkAsReadyToSend();

            var time = coherenceSync.CoherenceBridge.NetworkTime.TimeAsDouble;
            var simulationFrame = coherenceSync.CoherenceBridge.NetworkTime.ClientSimulationFrame;

            if (sampleValueBeforeSending && positionBinding.IsInterpolated)
            {
                positionBinding.SampleValue(time);
            }

            SendComponentUpdates(time, simulationFrame);

            return true;
        }
    }
}
