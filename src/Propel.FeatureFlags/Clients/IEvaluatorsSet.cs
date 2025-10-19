using Microsoft.Extensions.Options;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace Propel.FeatureFlags.Clients;

/// <summary>
/// Defines a contract for evaluating feature flags using a set of evaluators.
/// </summary>
public interface IEvaluatorsSet
{
	/// <summary>
	/// Evaluates the specified options and context to determine the result of an evaluation process.
	/// </summary>
	ValueTask<EvaluationResult?> Evaluate(EvaluationOptions flag, EvaluationContext context);
}

public sealed class EvaluatorsSet : IEvaluatorsSet
{
	private readonly IEvaluator[] _evaluators;

	public EvaluatorsSet(HashSet<IEvaluator> handlers)
	{
		if (handlers == null)
			throw new ArgumentNullException(nameof(handlers));

		// Convert HashSet to ordered array to maintain evaluation order
		_evaluators = [.. handlers.OrderBy(h => h.EvaluationOrder)];
	}

	/// <summary>
	/// Evaluates the specified options and context to determine the result of an evaluation process.
	/// </summary>
	/// <remarks>The method iterates through a collection of evaluators that can process the provided options and
	/// context, ordered by their evaluation priority. If an evaluator produces a result where <see
	/// cref="EvaluationResult.IsEnabled"/> is <see langword="false"/>, the evaluation process terminates early and returns
	/// that result. If no such result is found and multiple evaluators contribute to an enabled result, the method
	/// combines their outcomes into a unified result with a descriptive reason.</remarks>
	/// <param name="evaluationOptions">The options that configure the evaluation process, such as feature flag settings or other criteria.</param>
	/// <param name="context">The context in which the evaluation is performed, providing additional information such as user or environment
	/// details.</param>
	/// <returns>An <see cref="EvaluationResult"/> representing the outcome of the evaluation, or <see langword="null"/> if no
	/// evaluators produce a result. If multiple evaluators process the request and the final result is enabled, the
	/// returned result includes a combined reason.</returns>
	public async ValueTask<EvaluationResult?> Evaluate(EvaluationOptions evaluationOptions, EvaluationContext context)
	{
		EvaluationResult? result = default;

		if (evaluationOptions.ModeSet.Contains([EvaluationMode.Off, EvaluationMode.On]))
		{
			// Handle fundamental states that don't require complex logic
			return EvaluateTerminalState(evaluationOptions, context);
		}

		var evaluators = _evaluators
				.Where(h => h.CanProcess(evaluationOptions, context))
				.OrderBy(p => p.EvaluationOrder)
				.ToList();

		foreach (var evaluator in evaluators)
		{
			result = await evaluator.Evaluate(evaluationOptions, context).ConfigureAwait(false);
			if (result != null && result.IsEnabled == false)
			{
				return result;
			}
		}

		if (result != default)
		{
			if (evaluators.Count == 1)
				return result;

			// If multiple handlers processed, and final result is enabled, provide a combined reason
			var reason = $"All configured conditions met for feature flag activation";
			return new EvaluationResult(isEnabled: true, variation: result.Variation, reason: reason);
		}

		return result;
	}

	private EvaluationResult? EvaluateTerminalState(EvaluationOptions evaluationOptions, EvaluationContext context)
	{
		bool disabled = evaluationOptions.ModeSet.Contains([EvaluationMode.Off]);
		string because = disabled
			? $"Feature flag '{evaluationOptions.Key}' is explicitly disabled"
			: $"Feature flag '{evaluationOptions.Key}' is explicitly enabled";
		return new EvaluationResult(isEnabled: !disabled, variation: evaluationOptions.Variations.DefaultVariation, reason: because);
	}
}

/// <summary>
/// Provides a factory method to create a default set of feature flag evaluators.
/// </summary>
public static class DefaultEvaluators
{
	public static IEvaluatorsSet Create()
	{
		// Create processor with all evaluators for legacy applications that don't use DI
		return new EvaluatorsSet(new HashSet<IEvaluator>(
		[
			new ActivationScheduleEvaluator(),
			new OperationalWindowEvaluator(),
			new TargetingRulesEvaluator(),
			new TenantRolloutEvaluator(),
			new UserRolloutEvaluator(),
		]));
	}
}
