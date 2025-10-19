using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Clients;

/// <summary>
/// Provides methods to evaluate feature flags and determine their state for a given context.
/// </summary>
/// <remarks>This interface is typically used to check whether a feature flag is enabled or to retrieve detailed
/// evaluation results based on contextual information such as tenant, user, and custom attributes.</remarks>
public interface IGlobalFlagClient
{
	/// <summary>
	/// Determines whether the specified feature flag is enabled based on the provided context.
	/// </summary>
	Task<bool> IsEnabledAsync(string flagKey, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
	/// <summary>
	/// Evaluates the specified feature flag asynchronously and returns the evaluation result.
	/// </summary>
	Task<EvaluationResult?> EvaluateAsync(string flagKey, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
}

public sealed class GlobalFlagClient(
	IGlobalFlagProcessor processor) : IGlobalFlagClient
{
	/// <summary>
	/// Determines whether the specified feature flag is enabled based on the provided context.
	/// </summary>
	/// <remarks>This method evaluates the feature flag using the provided context, which may include tenant, user,
	/// and additional attributes. The evaluation result depends on the configuration of the feature flag and the context
	/// provided.</remarks>
	/// <param name="flagKey">The unique key identifying the feature flag to evaluate. Cannot be <see langword="null"/> or empty.</param>
	/// <param name="tenantId">An optional identifier for the tenant. If provided, the evaluation will consider tenant-specific settings.</param>
	/// <param name="userId">An optional identifier for the user. If provided, the evaluation will consider user-specific settings.</param>
	/// <param name="attributes">An optional dictionary of additional attributes to include in the evaluation context.  These attributes can provide
	/// extra information for fine-grained feature flag evaluation.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the feature flag is
	/// enabled; otherwise, <see langword="false"/>.</returns>
	public async Task<bool> IsEnabledAsync(
		string flagKey,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes);

		var result = await processor.Evaluate(flagKey, context).ConfigureAwait(false);
		return result!.IsEnabled;
	}

	/// <summary>
	/// Evaluates the specified feature flag asynchronously and returns the evaluation result.
	/// </summary>
	/// <remarks>This method constructs an evaluation context using the provided parameters and evaluates the
	/// specified feature flag. The evaluation result may depend on the tenant, user, and attributes provided.</remarks>
	/// <param name="flagKey">The unique key identifying the feature flag to evaluate. This parameter cannot be null or empty.</param>
	/// <param name="tenantId">An optional identifier for the tenant in a multi-tenant environment. If null, the evaluation will not be scoped to
	/// a specific tenant.</param>
	/// <param name="userId">An optional identifier for the user. If null, the evaluation will not be scoped to a specific user.</param>
	/// <param name="attributes">An optional dictionary of additional attributes to include in the evaluation context. Keys represent attribute
	/// names, and values represent their corresponding values.</param>
	/// <returns>An <see cref="EvaluationResult"/> representing the outcome of the feature flag evaluation, or <see
	/// langword="null"/> if the evaluation could not be performed.</returns>
	public async Task<EvaluationResult?> EvaluateAsync(
		string flagKey,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes);

		return await processor.Evaluate(flagKey, context).ConfigureAwait(false);
	}
}
