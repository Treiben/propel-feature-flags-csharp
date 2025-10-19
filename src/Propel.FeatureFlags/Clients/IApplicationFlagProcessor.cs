using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Utilities;
using System.Text.Json;

namespace Propel.FeatureFlags.Clients;

/// <summary>
/// Defines methods for evaluating feature flags and retrieving their variations based on the provided evaluation
/// context.
/// </summary>
/// <remarks>This interface is designed to support feature flag evaluation in applications, allowing developers to
/// determine the state of a feature flag and retrieve specific variations of its value. Implementations of this
/// interface should handle the evaluation logic and provide appropriate results based on the supplied context and
/// feature flag configuration.</remarks>
public interface IApplicationFlagProcessor
{
	/// <summary>
	/// Evaluates the specified feature flag within the given evaluation context and returns the result.
	/// </summary>
	Task<EvaluationResult?> Evaluate(IFeatureFlag flag, EvaluationContext context, CancellationToken cancellationToken = default);
	/// <summary>
	/// Retrieves the variation of a feature flag for the specified context, returning a default value if the flag is not
	/// configured or cannot be evaluated.
	/// </summary>
	Task<T> GetVariation<T>(IFeatureFlag flag, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default);
}

public sealed class ApplicationFlagProcessor(
	IFeatureFlagRepository repository,
	IEvaluatorsSet evaluators,
	IFeatureFlagCache? cache = null) : IApplicationFlagProcessor
{
	private readonly IFeatureFlagRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly IEvaluatorsSet _flagProcessor = evaluators ?? throw new ArgumentNullException(nameof(evaluators));

	private string ApplicationName => ApplicationInfo.Name;
	private string ApplicationVersion => ApplicationInfo.Version;

	/// <summary>
	/// Evaluates the specified feature flag within the given evaluation context and returns the result.
	/// </summary>
	/// <remarks>This method attempts to retrieve the configuration for the specified feature flag. If the
	/// configuration does not exist, the method automatically creates a default flag in the database. The evaluation
	/// process uses the provided context to determine the flag's state. In case of an error, the method returns a disabled
	/// result with an appropriate reason.</remarks>
	/// <param name="applicationFlag">The feature flag to evaluate. This parameter must not be <see langword="null"/>.</param>
	/// <param name="context">The evaluation context containing user or environment-specific data used to determine the flag's state. This
	/// parameter must not be <see langword="null"/>.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
	/// <returns>An <see cref="EvaluationResult"/> representing the evaluation outcome. If the feature flag is not configured, the
	/// method creates a default flag in the database and returns a result based on the flag's mode. If an error occurs
	/// during evaluation, the result indicates that the flag is disabled.</returns>
	public async Task<EvaluationResult?> Evaluate(IFeatureFlag applicationFlag, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var appFlagIdentifier = new ApplicationFlagIdentifier(applicationFlag.Key);
			var config = await GetFlagConfiguration(appFlagIdentifier, cancellationToken);
			if (config == null)
			{
				// Auto-create flag in disabled state for deployment scenarios
				await RegisterApplicationFlag(applicationFlag, appFlagIdentifier, cancellationToken);
				return new EvaluationResult
				(
					isEnabled: applicationFlag.OnOffMode == EvaluationMode.On,
					reason: $"An application flag created in the database with specified mode {applicationFlag.OnOffMode} by {ApplicationName} application"
				);
			}

			return await _flagProcessor.Evaluate(config, context);
		}
		catch (Exception ex)
		{
			if (ex is OperationCanceledException)
				throw;
			// On any error, return disabled result
			return new EvaluationResult(isEnabled: false, reason: $"Error during evaluation: {ex.Message}");
		}
	}

	/// <summary>
	/// Retrieves the variation of a feature flag for the specified context, returning a default value if the flag is not
	/// configured or cannot be evaluated.
	/// </summary>
	/// <remarks>This method evaluates the specified feature flag using the provided context and retrieves the
	/// corresponding variation value. If the flag is not configured, it will be automatically registered, and the default
	/// variation will be returned.   The method handles deserialization of JSON-based variation values into the specified
	/// type <typeparamref name="T"/>. If the variation value cannot be deserialized, converted, or matched to the
	/// requested type, the default variation is returned.  Exceptions other than <see cref="OperationCanceledException"/>
	/// are caught and suppressed, ensuring that the default variation is returned in case of unexpected errors.</remarks>
	/// <typeparam name="T">The type of the variation value to retrieve.</typeparam>
	/// <param name="applicationFlag">The feature flag to evaluate. The <see cref="IFeatureFlag.Key"/> property must uniquely identify the flag.</param>
	/// <param name="defaultVariation">The default value to return if the flag is not configured, disabled, or cannot be evaluated.</param>
	/// <param name="context">The evaluation context that provides additional information for flag evaluation, such as user attributes or
	/// environment details.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.</param>
	/// <returns>The variation value of the feature flag if it is enabled and a matching variation is found; otherwise, <paramref
	/// name="defaultVariation"/>.</returns>
	public async Task<T> GetVariation<T>(IFeatureFlag applicationFlag, T defaultVariation, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var appFlagIdentifier = new ApplicationFlagIdentifier(applicationFlag.Key);
			// Get flag once and reuse for both evaluation and variation lookup
			var config = await GetFlagConfiguration(appFlagIdentifier, cancellationToken);
			if (config == null)
			{
				// Auto-create and return default
				await RegisterApplicationFlag(applicationFlag, appFlagIdentifier, cancellationToken);
				return defaultVariation;
			}

			// Evaluate using the already-fetched flag configuration
			var result = await _flagProcessor.Evaluate(config, context);
			if (result?.IsEnabled == false)
			{
				return defaultVariation;
			}

			// Variation lookup from the evaluated result
			if (config.Variations.Values.TryGetValue(result!.Variation, out var variationValue))
			{
				if (variationValue is JsonElement jsonElement)
				{
					var deserializedValue = JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), JsonDefaults.JsonOptions);
					return deserializedValue ?? defaultVariation;
				}

				if (variationValue is T directValue)
				{
					return directValue;
				}

				// Try to convert to requested variation type
				var convertedValue = (T)Convert.ChangeType(variationValue, typeof(T));
				return convertedValue ?? defaultVariation;
			}

			return defaultVariation;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return defaultVariation;
		}
	}

	private async Task<EvaluationOptions?> GetFlagConfiguration(FlagIdentifier flagIdentifier, CancellationToken cancellationToken)
	{
		EvaluationOptions? config = null;

		// Try cache first
		var cacheKey = new ApplicationFlagCacheKey(flagIdentifier.Key, flagIdentifier.ApplicationName, flagIdentifier.ApplicationVersion);
		if (cache != null)
		{
			config = await cache.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
		}

		// If not in cache, get from repository
		if (config == null)
		{
			config = await _repository.GetEvaluationOptionsAsync(flagIdentifier, cancellationToken).ConfigureAwait(false);

			// Cache for future requests if found
			if (config != null && cache != null)
			{
				await cache.SetAsync(cacheKey, config, cancellationToken).ConfigureAwait(false);
			}
		}

		return config;
	}

	private async Task RegisterApplicationFlag(IFeatureFlag applicationFlag, FlagIdentifier flagIdentifier, CancellationToken cancellationToken)
	{
		var activationMode = applicationFlag.OnOffMode == EvaluationMode.On ? EvaluationMode.On : EvaluationMode.Off;
		var name = applicationFlag.Name ?? applicationFlag.Key;
		var description = applicationFlag.Description ?? $"Auto-created flag for {applicationFlag.Key} in application {ApplicationName}";
		try
		{
			// Save to repository and return the created flag (repository may set additional properties)
			await _repository.CreateApplicationFlagAsync(flagIdentifier, activationMode, name, description, cancellationToken).ConfigureAwait(false);
			// Cache for future requests
			if (cache != null)
			{
				// Create composite key for uniqueness per application
				var cacheKey = new ApplicationFlagCacheKey(flagIdentifier.Key, flagIdentifier.ApplicationName, flagIdentifier.ApplicationVersion);
				await cache.SetAsync(cacheKey,
					new EvaluationOptions(key: applicationFlag.Key, modeSet: new ModeSet([activationMode])), cancellationToken).ConfigureAwait(false);
			}
		}
		catch
		{
			// Even if repository save fails, just return
			// This handles scenarios where repository might be temporarily unavailable
			// The flag will be attempted to be created again on next evaluation
		}
	}
}