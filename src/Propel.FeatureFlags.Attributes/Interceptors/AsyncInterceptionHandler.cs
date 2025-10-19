using Castle.DynamicProxy;
using System.Reflection;

namespace Propel.FeatureFlags.Attributes.Interceptors;

internal sealed class AsyncInterceptionHandler(IFeatureFlagEvaluator evaluator)
{
	public void Handle(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		if (invocation.Method.ReturnType.IsGenericType)
		{
			// Task<T>
			var resultType = invocation.Method.ReturnType.GetGenericArguments()[0];
			var method = typeof(AsyncInterceptionHandler)
				.GetMethod(nameof(HandleGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
				.MakeGenericMethod(resultType);
			invocation.ReturnValue = method.Invoke(this, [invocation, flagAttribute]);
		}
		else
		{
			// Task (void)
			invocation.ReturnValue = HandleVoid(invocation, flagAttribute);
		}
	}

	private async Task HandleVoid(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		var flag = FeatureFlagCache.GetFeatureFlagInstance(flagAttribute.FlagType);
		if (flag is not null && await evaluator.IsEnabledAsync(flag).ConfigureAwait(false))
		{
			// Fixed to call the method directly on the target, not through the proxy because that would cause infinite recursion
			// Call the actual target method directly, not through the proxy
			var result = invocation.Method.Invoke(invocation.InvocationTarget, invocation.Arguments);
			if (result is Task task)
			{
				await task;
			}
		}
		else
		{
			await HandleFallback(invocation, flagAttribute).ConfigureAwait(false);
		}
	}

	private async Task<T> HandleGeneric<T>(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		var flag = FeatureFlagCache.GetFeatureFlagInstance(flagAttribute.FlagType);
		if (flag is not null && await evaluator.IsEnabledAsync(flag).ConfigureAwait(false))
		{
			// Fixed to call the method directly on the target, not through the proxy because that would cause infinite recursion
			// Call the actual target method directly, not through the proxy
			var result = invocation.Method.Invoke(invocation.InvocationTarget, invocation.Arguments);
			if (result is Task<T> task)
			{
				return await task;
			}

			return (T)result!;
		}
		else if (!string.IsNullOrEmpty(flagAttribute.FallbackMethod))
		{
			return await HandleFallbackGeneric<T>(invocation, flagAttribute).ConfigureAwait(false);
		}

		return (T)FeatureFlagCache.GetDefaultValue(typeof(T))!;
	}

	private static async Task HandleFallback(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		if (string.IsNullOrWhiteSpace(flagAttribute.FallbackMethod)) return;	

		var fallbackMethod = invocation.TargetType!.GetMethod(flagAttribute.FallbackMethod);
		if (fallbackMethod != null)
		{
			var result = fallbackMethod.Invoke(invocation.InvocationTarget, invocation.Arguments);
			if (result is Task task)
				await task;
		}
	}

	private static async Task<T> HandleFallbackGeneric<T>(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		if (!string.IsNullOrWhiteSpace(flagAttribute.FallbackMethod))
		{
			var fallbackMethod = invocation.TargetType!.GetMethod(flagAttribute.FallbackMethod);
			if (fallbackMethod != null)
			{
				var result = fallbackMethod.Invoke(invocation.InvocationTarget, invocation.Arguments);
				if (result is Task<T> task)
					return await task;
				if (result is T directResult)
					return directResult;
			}
		}

		return (T)FeatureFlagCache.GetDefaultValue(typeof(T))!;
	}
}
