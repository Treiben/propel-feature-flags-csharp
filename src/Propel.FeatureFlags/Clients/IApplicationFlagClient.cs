using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Clients;

/// <summary>
/// Provides methods for evaluating feature flags and retrieving their values or variations in the context of a specific
/// tenant, user, or set of attributes.
/// </summary>
/// <remarks>This interface is designed to support feature flag management, allowing applications to determine
/// whether a feature is enabled, retrieve a specific variation of a feature flag, or evaluate a feature flag to obtain
/// detailed results. The methods support optional contextual parameters such as tenant ID, user ID, and custom
/// attributes to enable fine-grained control over feature flag evaluation.</remarks>
public interface IApplicationFlagClient
{
	/// <summary>
	/// Determines whether the specified feature flag is enabled based on the provided evaluation context.
	/// </summary>
	Task<bool> IsEnabledAsync(IFeatureFlag flag, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);

	/// <summary>
	/// Retrieves the evaluated variation of a feature flag for the specified context.
	/// </summary>
	Task<T> GetVariationAsync<T>(IFeatureFlag flag, T defaultValue, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);

	/// <summary>
	/// Evaluates the specified feature flag asynchronously within the given context.
	/// </summary>
	Task<EvaluationResult?> EvaluateAsync(IFeatureFlag flag, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
}

public sealed class ApplicationFlagClient(
	IApplicationFlagProcessor processor) : IApplicationFlagClient
{
	/// <summary>
	/// Determines whether the specified feature flag is enabled based on the provided evaluation context.
	/// </summary>
	/// <remarks>The evaluation considers the provided context, including tenant ID, user ID, and any additional
	/// attributes. If no context is provided, the evaluation is performed using default or global settings.</remarks>
	/// <param name="flag">The feature flag to evaluate. This parameter cannot be <see langword="null"/>.</param>
	/// <param name="tenantId">An optional identifier for the tenant. If specified, the evaluation will consider the tenant context.</param>
	/// <param name="userId">An optional identifier for the user. If specified, the evaluation will consider the user context.</param>
	/// <param name="attributes">An optional dictionary of additional attributes to include in the evaluation context. The keys represent attribute
	/// names, and the values represent their corresponding values.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the feature flag is
	/// enabled; otherwise, <see langword="false"/>.</returns>
	public async Task<bool> IsEnabledAsync(
		IFeatureFlag flag,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes);

		var result = await processor.Evaluate(flag, context).ConfigureAwait(false);
		return result!.IsEnabled;
	}

	/// <summary>
	/// Retrieves the evaluated variation of a feature flag for the specified context.
	/// </summary>
	/// <typeparam name="T">The type of the variation value.</typeparam>
	/// <param name="flag">The feature flag to evaluate. Cannot be <see langword="null"/>.</param>
	/// <param name="defaultValue">The default value to return if the feature flag cannot be evaluated.</param>
	/// <param name="tenantId">An optional identifier for the tenant. If <see langword="null"/>, the evaluation will not consider tenant-specific
	/// context.</param>
	/// <param name="userId">An optional identifier for the user. If <see langword="null"/>, the evaluation will not consider user-specific
	/// context.</param>
	/// <param name="attributes">An optional dictionary of additional attributes to include in the evaluation context.  Keys represent attribute
	/// names, and values represent their corresponding values.</param>
	/// <returns>The evaluated variation of the feature flag as a value of type <typeparamref name="T"/>.  If the feature flag
	/// cannot be evaluated, the <paramref name="defaultValue"/> is returned.</returns>
	public async Task<T> GetVariationAsync<T>(
		IFeatureFlag flag,
		T defaultValue,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes);

		return await processor.GetVariation(flag, defaultValue, context).ConfigureAwait(false);
	}

	/// <summary>
	/// Evaluates the specified feature flag asynchronously within the given context.
	/// </summary>
	/// <remarks>This method evaluates the feature flag based on the provided context, which may include tenant,
	/// user,  and additional attributes. The evaluation result depends on the rules defined for the feature
	/// flag.</remarks>
	/// <param name="flag">The feature flag to evaluate. This parameter cannot be <see langword="null"/>.</param>
	/// <param name="tenantId">An optional identifier for the tenant. If <see langword="null"/>, the evaluation will not be scoped to a specific
	/// tenant.</param>
	/// <param name="userId">An optional identifier for the user. If <see langword="null"/>, the evaluation will not be scoped to a specific
	/// user.</param>
	/// <param name="attributes">An optional dictionary of additional attributes to include in the evaluation context.  Keys represent attribute
	/// names, and values represent their corresponding values.</param>
	/// <returns>An <see cref="EvaluationResult"/> representing the outcome of the feature flag evaluation,  or <see
	/// langword="null"/> if the evaluation could not be completed.</returns>
	public async Task<EvaluationResult?> EvaluateAsync(
		IFeatureFlag flag,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes);

		return await processor.Evaluate(flag, context).ConfigureAwait(false);
	}
}
