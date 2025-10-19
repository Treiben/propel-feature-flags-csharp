using Microsoft.AspNetCore.Http;
using Propel.FeatureFlags.AspNetCore.Extensions;
using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Attributes;

internal interface IFeatureFlagEvaluator
{
	Task<bool> IsEnabledAsync(IFeatureFlag flag);
}

internal sealed class HttpFeatureFlagEvaluator(IHttpContextAccessor httpContextAccessor) : IFeatureFlagEvaluator
{
	private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
	public async Task<bool> IsEnabledAsync(IFeatureFlag flag)
	{
		var context = _httpContextAccessor.HttpContext;
		if (context == null)
			return false;

		var evaluator = context.FeatureFlags();
		if (evaluator == null)
			return false;

		return await evaluator.IsEnabledAsync(flag).ConfigureAwait(false);
	}
}

internal sealed class DefaultEvaluator(IApplicationFlagClient featureFlagClient) : IFeatureFlagEvaluator
{
	private readonly IApplicationFlagClient _featureFlagClient = featureFlagClient ?? throw new ArgumentNullException(nameof(featureFlagClient));
	public async Task<bool> IsEnabledAsync(IFeatureFlag flag)
	{
		return await _featureFlagClient.IsEnabledAsync(flag).ConfigureAwait(false);
	}
}
