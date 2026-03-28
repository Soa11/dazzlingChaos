namespace Coherence.Toolkit.Bindings.ValueBindings
{
    using Log;
    using System;
    using Entities;
    using UnityEngine;
    using Coherence.Toolkit.Debugging;

    [Serializable]
    public class ReferenceBinding : ValueBinding<Entity>
    {
        protected ReferenceBinding() { }
        public ReferenceBinding(Descriptor descriptor, Component unityComponent) : base(descriptor, unityComponent)
        {
        }

        public override Entity Value
        {
            get => MapToEntityId(GetValueUsingReflection());
            set => SetValueUsingReflection(MapToUnityObject(value));
        }

        public override void InvokeValueSyncCallback()
        {
            if (!valueSyncPrepared)
            {
                return;
            }

            try
            {
                InvokeValueChangedCallback(valueSyncOld, valueSyncNew);
            }
            catch (Exception handlerException)
            {
                Logger.Error(Error.ToolkitBindingOnValueSyncedException,
                    ("exception", handlerException));
            }

            valueSyncPrepared = false;
        }

        private Entity MapToEntityId(object target)
        {
            if (MonoAssemblyRuntimeType == typeof(GameObject)) return coherenceSync.CoherenceBridge.UnityObjectToEntityId((GameObject)target);
            if (MonoAssemblyRuntimeType == typeof(RectTransform)) return coherenceSync.CoherenceBridge.UnityObjectToEntityId((RectTransform)target);
            if (MonoAssemblyRuntimeType == typeof(Transform)) return coherenceSync.CoherenceBridge.UnityObjectToEntityId((Transform)target);
            if (MonoAssemblyRuntimeType == typeof(CoherenceSync)) return coherenceSync.CoherenceBridge.UnityObjectToEntityId((CoherenceSync)target);
            throw new Exception("unexpected type: " + MonoAssemblyRuntimeType);
        }

        private object MapToUnityObject(Entity entityId)
        {
            if (MonoAssemblyRuntimeType == typeof(GameObject)) return coherenceSync.CoherenceBridge.EntityIdToGameObject(entityId);
            if (MonoAssemblyRuntimeType == typeof(RectTransform)) return coherenceSync.CoherenceBridge.EntityIdToRectTransform(entityId);
            if (MonoAssemblyRuntimeType == typeof(Transform)) return coherenceSync.CoherenceBridge.EntityIdToTransform(entityId);
            if (MonoAssemblyRuntimeType == typeof(CoherenceSync)) return coherenceSync.CoherenceBridge.EntityIdToCoherenceSync(entityId);
            throw new Exception("unexpected type: " + MonoAssemblyRuntimeType);
        }

        protected override Type GetValueChangedCallbackType()
        {
            if (MonoAssemblyRuntimeType == typeof(GameObject)) return typeof(Action<GameObject, GameObject>);
            if (MonoAssemblyRuntimeType == typeof(CoherenceSync)) return typeof(Action<CoherenceSync, CoherenceSync>);
            if (MonoAssemblyRuntimeType == typeof(Transform)) return typeof(Action<Transform, Transform>);
            if (MonoAssemblyRuntimeType == typeof(RectTransform)) return typeof(Action<RectTransform, RectTransform>);
            throw new Exception("unexpected type: " + MonoAssemblyRuntimeType);
        }

        private void InvokeValueChangedCallback(Entity oldEntityId, Entity newEntityId)
        {
            if (MonoAssemblyRuntimeType == typeof(GameObject))
            {
                ((Action<GameObject, GameObject>)valueChangedCallback).Invoke(
                    coherenceSync.CoherenceBridge.EntityIdToGameObject(oldEntityId),
                    coherenceSync.CoherenceBridge.EntityIdToGameObject(newEntityId));
                return;
            }

            if (MonoAssemblyRuntimeType == typeof(CoherenceSync))
            {
                ((Action<CoherenceSync, CoherenceSync>)valueChangedCallback).Invoke(
                    coherenceSync.CoherenceBridge.EntityIdToCoherenceSync(oldEntityId),
                    coherenceSync.CoherenceBridge.EntityIdToCoherenceSync(newEntityId));
                return;
            }

            if (MonoAssemblyRuntimeType == typeof(Transform))
            {
                ((Action<Transform, Transform>)valueChangedCallback).Invoke(
                    coherenceSync.CoherenceBridge.EntityIdToTransform(oldEntityId),
                    coherenceSync.CoherenceBridge.EntityIdToTransform(newEntityId));
                return;
            }

            if (MonoAssemblyRuntimeType == typeof(RectTransform))
            {
                ((Action<RectTransform, RectTransform>)valueChangedCallback).Invoke(
                    coherenceSync.CoherenceBridge.EntityIdToRectTransform(oldEntityId),
                    coherenceSync.CoherenceBridge.EntityIdToRectTransform(newEntityId));
                return;
            }

            throw new Exception("unexpected type: " + MonoAssemblyRuntimeType);
        }

        protected override bool DiffersFrom(Entity first, Entity second)
        {
            return first != second;
        }

        [InterpolationDebugContextItem]
        public void ToggleInterpolationDebugGrapher() => ToggleInterpolationDebugGrapher(v => (uint)v.Index);
    }
}
