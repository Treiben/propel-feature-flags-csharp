using Microsoft.AspNetCore.Http;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.AspNetCore.Extensions;

/// <summary>
/// Provides extension methods for accessing and evaluating feature flags within an <see cref="HttpContext"/>.
/// </summary>
/// <remarks>These extension methods enable interaction with the feature flag evaluation system, which must be
/// configured in the application's middleware pipeline. Ensure that the feature flag middleware is added using
/// <c>UseFeatureFlags()</c> before using these methods. Failure to do so will result in an <see
/// cref="InvalidOperationException"/>.</remarks>
public static class HttpContextExtensions
{
	/// <summary>
	/// Retrieves the <see cref="HttpContextFeatureFlagEvaluator"/> instance associated with the current HTTP context.
	/// </summary>
	/// <param name="context">The <see cref="HttpContext"/> from which to retrieve the feature flag evaluator.</param>
	/// <returns>The <see cref="HttpContextFeatureFlagEvaluator"/> instance if it is available in the <see
	/// cref="HttpContext.Items"/> collection;  otherwise, <see langword="null"/>.</returns>
	public static HttpContextFeatureFlagEvaluator? FeatureFlags(this HttpContext context)
	{
		return context.Items["FeatureFlagEvaluator"] as HttpContextFeatureFlagEvaluator;
	}

	/// <summary>
	/// Determines whether the specified feature flag is enabled for the current HTTP context.
	/// </summary>
	/// <param name="context">The current <see cref="HttpContext"/> instance. This cannot be <see langword="null"/>.</param>
	/// <param name="flag">The feature flag to evaluate. This cannot be <see langword="null"/>.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the feature flag is
	/// enabled; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the feature flag middleware is not configured. Ensure that <c>UseFeatureFlags()</c> is added to the
	/// application's middleware pipeline.</exception>
	public static async Task<bool> IsFeatureFlagEnabledAsync(this HttpContext context, IFeatureFlag flag)
	{
		var evaluator = context.FeatureFlags();
		if (evaluator == null)
			throw new InvalidOperationException("Feature flag middleware not configured. Add UseFeatureFlags() to your pipeline.");

		return await evaluator.IsEnabledAsync(flag).ConfigureAwait(false);
	}

	/// <summary>
	/// Retrieves the variation of a feature flag for the current HTTP context.
	/// </summary>
	/// <typeparam name="T">The type of the feature flag variation value.</typeparam>
	/// <param name="context">The current <see cref="HttpContext"/> instance.</param>
	/// <param name="flag">The feature flag to evaluate.</param>
	/// <param name="defaultValue">The default value to return if the feature flag variation cannot be determined.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the variation value of the feature
	/// flag, or <paramref name="defaultValue"/> if the variation cannot be determined.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the feature flag middleware is not configured. Ensure that <c>UseFeatureFlags()</c> is added to the
	/// application's middleware pipeline.</exception>
	public static async Task<T> GetFeatureFlagVariationAsync<T>(this HttpContext context, IFeatureFlag flag, T defaultValue)
	{
		var evaluator = context.FeatureFlags();
		if (evaluator == null)
			throw new InvalidOperationException("Feature flag middleware not configured. Add UseFeatureFlags() to your pipeline.");

		return await evaluator.GetVariationAsync(flag, defaultValue).ConfigureAwait(false);
	}
}
