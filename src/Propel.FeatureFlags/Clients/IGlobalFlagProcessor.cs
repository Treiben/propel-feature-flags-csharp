using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace Propel.FeatureFlags.Clients;

/// <summary>
/// Defines a contract for evaluating feature flags based on a given key and context.
/// </summary>
/// <remarks>This interface is typically implemented by services that evaluate feature flags in a dynamic
/// configuration system. The evaluation process may involve querying external systems, applying rules, or using cached
/// values.</remarks>
public interface IGlobalFlagProcessor
{
	/// <summary>
	/// Evaluates the specified feature flag using the provided evaluation context.
	/// </summary>
	Task<EvaluationResult?> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default);
}

public sealed class GlobalFlagProcessor(
	IFeatureFlagRepository repository,
	IEvaluatorsSet evaluators,
	IFeatureFlagCache? cache = null) : IGlobalFlagProcessor
{
	private readonly IFeatureFlagRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly IEvaluatorsSet _evaluators = evaluators ?? throw new ArgumentNullException(nameof(evaluators));

	/// <summary>
	/// Evaluates the specified feature flag using the provided evaluation context.
	/// </summary>
	/// <remarks>Ensure that the feature flag identified by <paramref name="flagKey"/> is created and configured in
	/// the system before calling this method.</remarks>
	/// <param name="flagKey">The unique key identifying the feature flag to evaluate.</param>
	/// <param name="context">The evaluation context containing attributes and other data used to determine the flag's value.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>An <see cref="EvaluationResult"/> representing the outcome of the evaluation, or <see langword="null"/> if the
	/// evaluation fails.</returns>
	/// <exception cref="Exception">Thrown if the specified feature flag does not exist in the system.</exception>
	public async Task<EvaluationResult?> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		var globalFlagIdentifier = new GlobalFlagIdentifier(flagKey);
		var config = await GetFlagConfiguration(globalFlagIdentifier, cancellationToken);
		if (config == null)
		{
			throw new Exception("The global feature flag is not found. Please create the flag in the system before evaluating it or remove reference to it.");
		}

		return await _evaluators.Evaluate(config, context);
	}

	private async Task<EvaluationOptions?> GetFlagConfiguration(FlagIdentifier flagIdentifier, CancellationToken cancellationToken)
	{
		// Create composite key for uniqueness per application
		var globalFlagCacheKey = new GlobalFlagCacheKey(flagIdentifier.Key);
		// Try cache first
		EvaluationOptions? flagConfig = null;
		if (cache != null)
		{
			flagConfig = await cache.GetAsync(globalFlagCacheKey, cancellationToken).ConfigureAwait(false);
		}

		// If not in cache, get from repository
		if (flagConfig == null)
		{
			flagConfig = await _repository.GetEvaluationOptionsAsync(flagIdentifier, cancellationToken).ConfigureAwait(false);

			// Cache for future requests if found
			if (flagConfig != null && cache != null)
			{
				await cache.SetAsync(globalFlagCacheKey, flagConfig, cancellationToken).ConfigureAwait(false);
			}
		}

		return flagConfig;
	}
}
