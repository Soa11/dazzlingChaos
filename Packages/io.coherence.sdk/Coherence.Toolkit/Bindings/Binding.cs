// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Bindings
{
    using Log;
    using System;
    using System.Reflection;
    using Coherence.SimulationFrame;
    using Entities;
    using Interpolation;
    using UnityEngine;
    using Logger = Log.Logger;
    using Object = UnityEngine.Object;
    using UnityEngine.Serialization;

    /// <summary>
    /// Base class for everything that can be synced over the network.
    /// A binding holds the metadata that associates a user-facing unity component
    /// with its network replicated coherence component.
    /// </summary>
    /// <seealso cref="ValueBinding{T}">ValueBinding{T} for synced fields and properties.</seealso>
    /// <seealso cref="CommandBinding">CommandBinding for synced methods</seealso>
    [Serializable]
    public abstract class Binding
    {
        [SerializeReference] protected Descriptor descriptor;
        //////////////////////////////////////////////////////////
        // Fields
        //////////////////////////////////////////////////////////
        [SerializeField] public MessageTarget routing;

        [SerializeField]
        protected SyncMode syncMode = SyncMode.Always;

        [Obsolete("Please use predictionMode instead.")]
        [Deprecated("17/01/2024", 1, 2, 0, Reason = "Replaced by predictionMode.")]
        public bool isPredicted;

        public PredictionMode predictionMode = PredictionMode.Never;

        public virtual bool IsInterpolated => interpolationSettings != null && !interpolationSettings.IsInterpolationNone;

        public string guid;
        [SerializeField]
        internal BindingArchetypeData archetypeData;
        public Component unityComponent;
        public CoherenceSync coherenceSync;


        [SerializeField, FormerlySerializedAs("interpolationSettings")]
        [InterpolationPicker(nameof(RaiseOnInterpolationSettingsChanged))]
        private InterpolationSettings _interpolationSettings;

        public InterpolationSettings interpolationSettings
        {
            get => _interpolationSettings;
            set
            {
                if (_interpolationSettings == value)
                {
                    return;
                }

                var oldValue = _interpolationSettings;
                _interpolationSettings = value;

                RaiseOnInterpolationSettingsChanged(oldValue, value);
            }
        }

        public event Action<InterpolationSettings, InterpolationSettings> OnInterpolationSettingsChanged;

        public event Action<object, bool, long> OnNetworkSampleReceived;

        private Logger logger;
        internal Logger Logger
        {
            get
            {
                if (logger != null)
                {
                    return logger;
                }

                logger = coherenceSync != null
                    ? coherenceSync.logger.With<Binding>()
                    : Log.GetLogger<Binding>();

                return logger;
            }

            set => logger = value;
        }

        protected double nextSampleTime;

        //////////////////////////////////////////////////////////
        // Properties
        //////////////////////////////////////////////////////////
        public string Name => descriptor != null ? descriptor.Name : "Null";
        public virtual string MemberNameInComponentData => Name;
        public virtual string MemberNameInUnityComponent => Name;
        public string FullName => unityComponent ? $"{unityComponent.GetType().FullName}.{Name}" : Name;
        public Type MonoAssemblyRuntimeType => descriptor != null ? TypeUtils.GetMemoizedType(descriptor.MonoAssemblyType) : null;

        private Type coherenceComponentType;
        public virtual Type CoherenceComponentType => coherenceComponentType ??= TypeUtils.GetMemoizedType($"{CoherenceComponentNamespace}.{CoherenceComponentName}, {CoherenceComponentAssemblyName}");
        public virtual string CoherenceComponentName => null;
        public virtual string CoherenceComponentNamespace => "Coherence.Generated";
        public virtual string CoherenceComponentAssemblyName => "Coherence.Interop.Generated";
        public virtual bool EmitSchemaComponentDefinition => true;
        public string SchemaFieldName => TypeUtils.GetSchemaFieldName(MonoAssemblyRuntimeType);
        public string SchemaFieldSimulationFrameName => SchemaFieldName + "SimulationFrame";
        public virtual uint FieldMask => 1;
        public string BakedSyncScriptCSharpType => descriptor != null ? descriptor.BakedCSharpType : null;
        public virtual string BakedSyncScriptGetter => null;
        public virtual string BakedSyncScriptSetter => null;
        public virtual bool OverrideSetter => false;
        public virtual bool OverrideGetter => false;
        public Component UnityComponent => unityComponent;
        public bool IsValid => unityComponent != null;
        public bool IsMethod => descriptor != null && descriptor.MemberType == MemberTypes.Method;
        public bool EnforcesLODingWhenFieldsOverriden => descriptor != null && descriptor.EnforcesLODingWhenFieldsOverriden;
        public virtual object UntypedValue => null;

        /// <summary>
        /// Rich text representation of the binding's name and type that is displayed in the Configure and Optimize binding windows.
        /// </summary>
        public virtual string SignatureRichText => SignaturePlainText;
        public virtual string SignaturePlainText => GetType().Name;

        public string Signature =>
#if UNITY_EDITOR
            SignatureRichText;
#else
            SignaturePlainText;
#endif

        /// <summary>
        /// Gets or sets the synchronization mode that determines when this binding's data is replicated over the network.
        /// </summary>
        /// <value>
        /// The current <see cref="Bindings.SyncMode"/> for this binding. Defaults to <see cref="Bindings.SyncMode.Always"/>.
        /// </value>
        /// <remarks>
        /// The sync mode controls the timing and conditions under which the binding's value is checked for changes
        /// and sent across the network. This allows fine-tuning of network traffic and performance.
        /// <para>
        /// The default sync mode can be configured through the <see cref="SyncAttribute.DefaultSyncMode"/> property
        /// when marking fields or properties with the [Sync] attribute in your code.
        /// </para>
        /// </remarks>
        /// <seealso cref="Bindings.SyncMode"/>
        /// <seealso cref="SyncAttribute"/>
        /// <seealso cref="ValueBinding{T}.MarkForSyncing"/>
        public virtual SyncMode SyncMode
        {
            get => syncMode;
            set => syncMode = value;
        }

        internal BindingArchetypeData BindingArchetypeData
        {
            get => archetypeData;
            set => archetypeData = value;
        }

        public Descriptor Descriptor
        {
            get => descriptor;
            internal set => descriptor = value;
        }

        protected Binding() { }
        public Binding(Descriptor descriptor, Component unityComponent)
        {
            this.descriptor = descriptor;
            this.unityComponent = unityComponent;
            this.coherenceSync = unityComponent.GetComponentInParent<CoherenceSync>() ?? unityComponent.GetComponent<CoherenceSync>();
            _ = EnsureGuid();
            routing = descriptor.DefaultRouting;
            syncMode = descriptor.DefaultSyncMode;

#if UNITY_EDITOR
            if (descriptor.ShouldDefaultToNoneInterpolation())
            {
                interpolationSettings = InterpolationSettings.Empty;
            }
            else
            {
                interpolationSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<InterpolationSettings>(InterpolationSettings.DefaultSettingsPath);
            }
#endif
        }

        internal void AssignComponent(Component component)
        {
            unityComponent = component;
        }

        internal bool EnsureGuid()
        {
            if (string.IsNullOrEmpty(guid))
            {
                guid = Guid.NewGuid().ToString("N");
                return true;
            }

            return false;
        }

        internal virtual void Activate() { }

        internal Type GetUnityComponentType() => descriptor.OwnerType;

        public virtual void CloneTo(Binding clone)
        {
            clone.unityComponent = unityComponent;
            clone.coherenceSync = coherenceSync;

            clone.archetypeData = archetypeData;
            clone.descriptor = descriptor;
            clone.guid = guid;

            clone.interpolationSettings = interpolationSettings;
            clone.predictionMode = predictionMode;

            clone.routing = routing;
            clone.syncMode = syncMode;

            clone.OnBindingCloned();
        }

        public Binding Clone(Component c)
        {
            var newBinding = (Binding)MemberwiseClone();
            newBinding.AssignComponent(c);
            newBinding.guid = Guid.NewGuid().ToString("N");

            return newBinding;
        }

        internal bool CreateArchetypeData(SchemaType schemaType, int maxLods)
        {
            if (archetypeData == null)
            {
                archetypeData = new(schemaType, MonoAssemblyRuntimeType);
                _ = archetypeData.AddLODStep(maxLods);

                return true;
            }

            return false;
        }

        public virtual bool IsCurrentlyPredicted()
        {
            return predictionMode switch
            {
                PredictionMode.Never => false,
                PredictionMode.Always => true,
                PredictionMode.InputAuthority => coherenceSync.HasInputAuthority,
                var _ => throw new ArgumentOutOfRangeException()
            };
        }



        internal void ClearSampleTime()
        {
            nextSampleTime = 0;
        }

        internal virtual void ResetInterpolation()
        {

        }

        public virtual void OnConnectedEntityChanged()
        {

        }

        public virtual ICoherenceComponentData CreateComponentData()
        {
            var componentType = CoherenceComponentType;
            if (componentType == null)
            {
                return null;
            }

            return Activator.CreateInstance(componentType) as ICoherenceComponentData;
        }

        protected void RaiseNetworkSampleReceived(object data, bool stopped, AbsoluteSimulationFrame sampleFrame)
        {
            OnNetworkSampleReceived?.Invoke(data, stopped, sampleFrame);
        }

        public virtual void SampleValue(double time) { }
        public virtual void Interpolate(double time) { }
        public virtual void RemoveOutdatedSamples(double time) { }
        public virtual void InvokeValueSyncCallback() { }
        public virtual ICoherenceComponentData WriteComponentData(ICoherenceComponentData coherenceComponent, AbsoluteSimulationFrame simFrame) { return coherenceComponent; }
        public virtual void ReceiveComponentData(ICoherenceComponentData coherenceComponent, AbsoluteSimulationFrame clientFrame, Vector3 floatingOriginDelta) { }
        public virtual MemberInfo GetMemberInfo() { return null; }
        public virtual void SetToLastSample() { }
        public virtual void ResetLastSentData() { }
        public virtual void ValidateNotBound() { }
        public virtual bool HasValueChangedCallback() { return false; }
        public abstract void IsDirty(AbsoluteSimulationFrame simulationFrame, out bool dirty, out bool justStopped);
        internal abstract void MarkAsReadyToSend();
        internal abstract bool IsReadyToSample(double currentTime);
        protected virtual void OnBindingCloned() { }
        internal virtual (bool IsValid, string Reason) IsBindingValid() { return (true, string.Empty); }

        private void RaiseOnInterpolationSettingsChanged(InterpolationSettings oldSettings, InterpolationSettings newSettings)
        {
            OnInterpolationSettingsChanged?.Invoke(oldSettings, newSettings);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Contains names of serialized properties found in <see cref="Binding"/>.
        /// Can be used in the editor with SerializedObject.FindProperty etc.
        /// </summary>
        internal static class Property
        {
            public const string archetypeData = nameof(Binding.archetypeData);
        }
#endif
    }
}
