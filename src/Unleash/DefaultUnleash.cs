namespace Unleash
{
    using Internal;
    using Logging;
    using Strategies;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Unleash.Events;
    using Unleash.Variants;

    /// <inheritdoc />
    public class DefaultUnleash : IUnleash
    {
        private static readonly ILogger Logger = LoggingService.GetLogger();

        private static readonly UnknownStrategy UnknownStrategy = new UnknownStrategy();

        private static int InitializedInstanceCount = 0;

        private const int WarnOnInstanceCount = 10;

        private static readonly IStrategy[] DefaultStrategies = {
            new DefaultStrategy(),
            new UserWithIdStrategy(),
            new GradualRolloutUserIdStrategy(),
            new GradualRolloutRandomStrategy(),
            new ApplicationHostnameStrategy(),
            new GradualRolloutSessionIdStrategy(),
            new RemoteAddressStrategy(),
            new FlexibleRolloutStrategy()
        };

        private readonly UnleashSettings settings;

        private readonly Dictionary<string, IStrategy> strategyMap;

        internal readonly UnleashServices services;

        ///// <summary>
        ///// Initializes a new instance of Unleash client with a set of default strategies. 
        ///// </summary>
        ///// <param name="config">Unleash settings</param>
        ///// <param name="strategies">Additional custom strategies.</param>
        public DefaultUnleash(UnleashSettings settings, params IStrategy[] strategies)
            : this(settings, overrideDefaultStrategies: false, strategies)
        { }

        ///// <summary>
        ///// Initializes a new instance of Unleash client.
        ///// </summary>
        ///// <param name="config">Unleash settings</param>
        ///// <param name="overrideDefaultStrategies">When true, it overrides the default strategies.</param>
        ///// <param name="strategies">Custom strategies.</param>
        public DefaultUnleash(UnleashSettings settings, bool overrideDefaultStrategies, params IStrategy[] strategies)
        {
            var currentInstanceNo = Interlocked.Increment(ref InitializedInstanceCount);

            this.settings = settings;

            var settingsValidator = new UnleashSettingsValidator();
            settingsValidator.Validate(settings);

            strategies = SelectStrategies(strategies, overrideDefaultStrategies);
            strategyMap = BuildStrategyMap(strategies);

            services = new UnleashServices(settings, EventConfig, strategyMap);

            Logger.Log($"UNLEASH: Unleash instance number { currentInstanceNo } is initialized and configured with: {settings}", LogVerbocity.Info);

            if (currentInstanceNo >= WarnOnInstanceCount)
            {
                Logger.Log($"UNLEASH: Unleash instance count for this process is now {currentInstanceNo}.", LogVerbocity.Warning);
                Logger.Log("Ideally you should only need 1 instance of Unleash per app/process, we strongly recommend setting up Unleash as a singleton.", LogVerbocity.Warning);
            }
        }

        /// <inheritdoc />
        public ICollection<FeatureToggle> FeatureToggles => services.ToggleCollection.Instance.Features;

        private EventCallbackConfig EventConfig { get; } = new EventCallbackConfig();

        /// <inheritdoc />
        public bool IsEnabled(string toggleName)
        {
            return IsEnabled(toggleName, false);
        }

        /// <inheritdoc />
        public bool IsEnabled(string toggleName, bool defaultSetting)
        {
            return IsEnabled(toggleName, services.ContextProvider.Context, defaultSetting);
        }

        public bool IsEnabled(string toggleName, UnleashContext context)
        {
            return IsEnabled(toggleName, context, false);
        }

        public bool IsEnabled(string toggleName, UnleashContext context, bool defaultSetting)
        {
            return CheckIsEnabled(toggleName, context, defaultSetting);
        }

        private bool CheckIsEnabled(string toggleName, UnleashContext context, bool defaultSetting)
        {
            var featureToggle = GetToggle(toggleName);

            bool enabled = false;
            if (featureToggle == null)
            {
                enabled = defaultSetting;
            }
            else if(!featureToggle.Enabled)
            {
                // Overall false
                enabled = false;
            }
            else if (featureToggle.Strategies.Count == 0)
            {
                enabled = true;
            }
            else
            {
                var enhancedContext = context.ApplyStaticFields(settings);
                enabled = featureToggle.Strategies.Any(s => GetStrategyOrUnknown(s.Name).IsEnabled(s.Parameters, enhancedContext, ResolveConstraints(s).Union(s.Constraints)));
            }

            RegisterCount(toggleName, enabled);

            if (featureToggle?.ImpressionData ?? false) EmitImpressionEvent("isEnabled", context, enabled, featureToggle.Name);

            return enabled;
        }

        public Variant GetVariant(string toggleName)
        {
            return GetVariant(toggleName, services.ContextProvider.Context, Variant.DISABLED_VARIANT);
        }

        public Variant GetVariant(string toggleName, Variant defaultVariant)
        {
            return GetVariant(toggleName, services.ContextProvider.Context, defaultVariant);
        }

        public Variant GetVariant(string toggleName, UnleashContext context, Variant defaultValue)
        {
            var toggle = GetToggle(toggleName);

            var enabled = CheckIsEnabled(toggleName, context, false);
            var variant = enabled ? VariantUtils.SelectVariant(toggle, context, defaultValue) : defaultValue;

            RegisterVariant(toggleName, variant);

            if (toggle?.ImpressionData ?? false) EmitImpressionEvent("getVariant", context, enabled, toggle.Name, variant.Name);

            return variant;
        }

        public IEnumerable<VariantDefinition> GetVariants(string toggleName)
        {
            return GetVariants(toggleName, services.ContextProvider.Context);
        }

        public IEnumerable<VariantDefinition> GetVariants(string toggleName, UnleashContext context)
        {
            if (!IsEnabled(toggleName, context)) return null;

            var toggle = GetToggle(toggleName);

            return toggle?.Variants;
        }

        private FeatureToggle GetToggle(string toggleName)
        {
            return services
                .ToggleCollection
                .Instance
                .GetToggleByName(toggleName);
        }

        private void RegisterCount(string toggleName, bool enabled)
        {
            if (services.IsMetricsDisabled)
                return;

            services.MetricsBucket.RegisterCount(toggleName, enabled);
        }

        private void RegisterVariant(string toggleName, Variant variant)
        {
            if (services.IsMetricsDisabled)
                return;

            services.MetricsBucket.RegisterCount(toggleName, variant.Name);
        }

        private static IStrategy[] SelectStrategies(IStrategy[] strategies, bool overrideDefaultStrategies)
        {
            if (overrideDefaultStrategies)
            {
                return strategies ?? new IStrategy[0];
            }
            else
            {
                return DefaultStrategies.Concat(strategies).ToArray();
            }
        }

        private static Dictionary<string, IStrategy> BuildStrategyMap(IStrategy[] strategies)
        {
            var map = new Dictionary<string, IStrategy>(strategies.Length);

            foreach (var strategy in strategies)
                map.Add(strategy.Name, strategy);

            return map;
        }

        private IStrategy GetStrategyOrUnknown(string strategy)
        {
            return strategyMap.ContainsKey(strategy) 
                ? strategyMap[strategy] 
                : UnknownStrategy;
        }

        private IEnumerable<Constraint> ResolveConstraints(ActivationStrategy activationStrategy)
        {
            foreach (var segmentId in activationStrategy.Segments)
            {
                var segment = services.ToggleCollection.Instance.GetSegmentById(segmentId);
                if (segment != null)
                {
                    foreach (var constraint in segment.Constraints)
                    {
                        yield return constraint;
                    }
                }
                else
                {
                    yield return null;
                }
            }
        }

        public void ConfigureEvents(Action<EventCallbackConfig> callback)
        {
            if (callback == null)
            {
                Logger.Log($"UNLEASH: Unleash->ConfigureEvents parameter callback is null", LogVerbocity.Error);
                return;
            }

            try
            {
                callback(EventConfig);
            }
            catch (Exception ex)
            {
                Logger.Log($"UNLEASH: Unleash->ConfigureEvents executing callback threw exception: {ex.Message}", LogVerbocity.Error);
            }
        }

        private void EmitImpressionEvent(string type, UnleashContext context, bool enabled, string name, string variant = null)
        {
            if (EventConfig?.ImpressionEvent == null)
            {
                Logger.Log($"UNLEASH: Unleash->ImpressionData callback is null, unable to emit event", LogVerbocity.Error);
                return;
            }

            try
            {
                EventConfig.ImpressionEvent(new ImpressionEvent
                {
                    Type = type,
                    Context = context,
                    EventId = Guid.NewGuid().ToString(),
                    Enabled = enabled,
                    FeatureName = name,
                    Variant = variant
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"UNLEASH: Emitting impression event callback threw exception: {ex.Message}", LogVerbocity.Error);
            }
        }

        public void Dispose()
        {
            services?.Dispose(); 
        }
    }
}