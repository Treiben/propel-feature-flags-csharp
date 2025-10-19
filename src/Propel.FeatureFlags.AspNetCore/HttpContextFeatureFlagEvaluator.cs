using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.AspNetCore;

/// <summary>
/// Provides functionality to evaluate feature flags in the context of an HTTP request,  using tenant, user, and custom
/// attributes for evaluation.
/// </summary>
/// <remarks>This class acts as a wrapper around an <see cref="IApplicationFlagClient"/> to evaluate  feature
/// flags based on the provided tenant ID, user ID, and additional attributes.  It is designed to be used in scenarios
/// where feature flag evaluation is tied to  HTTP request-specific context, such as multi-tenant or user-specific
/// applications.</remarks>
/// <param name="client"></param>
/// <param name="tenantId"></param>
/// <param name="userId"></param>
/// <param name="attributes"></param>
public class HttpContextFeatureFlagEvaluator(
	IApplicationFlagClient client,
	string? tenantId, 
	string? userId,
	Dictionary<string, object> attributes)
{
	/// <summary>
	/// Determines whether the specified feature flag is enabled for the current tenant and user.
	/// </summary>
	/// <param name="flag">The feature flag to evaluate.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/>  if the feature flag
	/// is enabled; otherwise, <see langword="false"/>.</returns>
	public async Task<bool> IsEnabledAsync(IFeatureFlag flag)
	{
		return await client.IsEnabledAsync(
			flag: flag, 
			tenantId: tenantId, 
			userId: userId,
			attributes: attributes).ConfigureAwait(false);
	}

	/// <summary>
	/// Retrieves the evaluated variation of the specified feature flag for the current user and tenant.
	/// </summary>
	/// <typeparam name="T">The type of the variation value.</typeparam>
	/// <param name="flag">The feature flag to evaluate. Cannot be <see langword="null"/>.</param>
	/// <param name="defaultValue">The default value to return if the feature flag evaluation fails or the flag is disabled.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the evaluated variation of the feature
	/// flag,  or <paramref name="defaultValue"/> if the evaluation fails or the flag is disabled.</returns>
	public async Task<T> GetVariationAsync<T>(IFeatureFlag flag, T defaultValue)
	{
		return await client.GetVariationAsync(
			flag: flag,
			defaultValue: defaultValue, 
			tenantId: tenantId, 
			userId: userId,
			attributes: attributes).ConfigureAwait(false); 
	}
}
