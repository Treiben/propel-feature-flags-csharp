using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Clients;

namespace Propel.FeatureFlags.AspNetCore.Middleware;

/// <summary>
/// Represents a global feature flag with associated metadata, including a key, status code, and response object.
/// </summary>
/// <remarks>This class is typically used to define and manage feature flags in an application, allowing for the
/// configuration of feature availability and associated responses. The <see cref="Key"/> property identifies the
/// feature, while <see cref="StatusCode"/> and <see cref="Response"/> provide metadata for handling feature-related
/// requests.</remarks>
public class GlobalFlag
{
	public string Key { get; set; } = string.Empty;
	public int StatusCode { get; set; } = 404;
	public object Response { get; set; } = new { error = "Feature not available" };
}

/// <summary>
/// Configuration options for the <see cref="FeatureFlagMiddleware"/>, including maintenance mode settings,
/// </summary>
public class FeatureFlagMiddlewareOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the application is in maintenance mode.
	/// </summary>
	public bool EnableMaintenanceMode { get; set; } = false;

	/// <summary>
	/// Gets or sets the key used to identify the maintenance flag in the system. Default is "maintenance-mode".
	/// </summary>
	public string MaintenanceFlagKey { get; set; } = "maintenance-mode";

	/// <summary>
	/// Gets or sets the response data related to a maintenance operation.
	/// </summary>
	public object? MaintenanceResponse { get; set; }
	public List<GlobalFlag> GlobalFlags { get; set; } = [];
	public List<Func<HttpContext, Dictionary<string, object>>> AttributeExtractors { get; set; } = [];

	/// <summary>
	/// Gets or sets the function used to extract the tenant identifier from the <see cref="HttpContext"/>.
	/// </summary>
	/// <remarks>This property allows customization of how the tenant identifier is resolved from the HTTP request
	/// context.  The function should return a unique identifier for the tenant, or <see langword="null"/> if no tenant is
	/// associated  with the request.</remarks>
	public Func<HttpContext, string?>? TenantIdExtractor { get; set; }

	/// <summary>
	/// Gets or sets the function used to extract the user ID from an <see cref="HttpContext"/>.
	/// </summary>
	/// <remarks>This property allows customization of how the user ID is retrieved from the HTTP context,  such as
	/// extracting it from headers, claims, or other context-specific data.</remarks>
	public Func<HttpContext, string?>? UserIdExtractor { get; set; }
}

/// <summary>
/// Middleware that evaluates feature flags and global flags for incoming HTTP requests, enabling or restricting access
/// based on the configured rules.
/// </summary>
/// <remarks>This middleware integrates with feature flagging systems to enforce global and application-specific
/// feature gates. It evaluates flags based on tenant, user, and request attributes, and can block requests if certain
/// conditions are not met.  Key features include: - Maintenance mode enforcement: Returns a 503 response if maintenance
/// mode is active. - Global flag evaluation: Blocks requests if any configured global flag is disabled. - Contextual
/// feature flag evaluation: Adds a feature flag evaluator to the <see cref="HttpContext.Items"/> collection for
/// downstream middleware or application logic.  To use this middleware, configure it with <see
/// cref="FeatureFlagMiddlewareOptions"/> to define global flags, maintenance mode settings, and custom attribute
/// extractors.</remarks>
public class FeatureFlagMiddleware
{
	private readonly RequestDelegate _next;
	private readonly IGlobalFlagClient _globalFlags;
	private readonly IApplicationFlagClient _featureFlags;
	private readonly ILogger<FeatureFlagMiddleware> _logger;
	private readonly FeatureFlagMiddlewareOptions _options;

	public FeatureFlagMiddleware(
		RequestDelegate next,
		IGlobalFlagClient globalFlags,
		IApplicationFlagClient featureFlags,
		ILogger<FeatureFlagMiddleware> logger,
		FeatureFlagMiddlewareOptions options)
	{
		_next = next;
		_globalFlags = globalFlags;
		_featureFlags = featureFlags;
		_logger = logger;
		_options = options;
	}

	/// <summary>
	/// Processes the incoming HTTP request, evaluates feature flags and maintenance mode, and determines whether to
	/// continue to the next middleware or return an appropriate response.
	/// </summary>
	/// <remarks>This middleware performs the following operations: <list type="bullet"> <item><description>Extracts
	/// tenant and user identifiers from the request.</description></item> <item><description>Checks if the application is
	/// in maintenance mode and, if so, returns a 503 Service Unavailable response.</description></item>
	/// <item><description>Evaluates global feature flags and, if any are disabled, returns the corresponding response as
	/// defined in the configuration.</description></item> <item><description>Adds a feature flag evaluator to the <see
	/// cref="HttpContext.Items"/> collection for downstream middleware or application components.</description></item>
	/// </list> If none of the above conditions are met, the middleware invokes the next middleware in the
	/// pipeline.</remarks>
	/// <param name="context">The <see cref="HttpContext"/> representing the current HTTP request and response.</param>
	/// <returns></returns>
	public async Task InvokeAsync(HttpContext context)
	{
		_logger.LogDebug("FeatureFlagMiddleware.InvokeAsync called for request {Path}", context.Request.Path);

		// Extract user ID using configured extractor or fallback
		var tenantId = ExtractTenantId(context);
		_logger.LogDebug("Tenant ID extracted: {TenantId}", tenantId ?? "null");
		var userId = ExtractUserId(context);
		_logger.LogDebug("User ID extracted: {UserId}", userId ?? "null");
		// Build base attributes
		var attributes = ExtractAttributes(context) ?? new Dictionary<string, object>();
		_logger.LogDebug("Attributes extracted: {@Attributes}", attributes);

		if (await CheckMaintenanceMode(tenantId, userId, attributes).ConfigureAwait(false))
		{
			_logger.LogInformation("Maintenance mode is active, returning 503 response");
			context.Response.StatusCode = 503;
			context.Response.ContentType = "application/json";

			var response = _options.MaintenanceResponse ?? new
			{
				error = "Service temporarily unavailable for maintenance",
				retryAfter = "300"
			};

			await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response)).ConfigureAwait(false);
			return;
		}

		// Check global feature gates
		foreach (var flag in _options.GlobalFlags)
		{
			_logger.LogDebug("Checking global flag: {FlagKey}", flag.Key);
			bool isEnabled = await _globalFlags.IsEnabledAsync(flagKey: flag.Key, tenantId: tenantId, userId: userId, attributes: attributes).ConfigureAwait(false);
			_logger.LogDebug("Global flag {FlagKey} status: {IsEnabled}", flag.Key, isEnabled);

			if (!isEnabled)
			{
				_logger.LogInformation("Global flag {FlagKey} is disabled, returning {StatusCode} response",
					flag.Key, flag.StatusCode);
				context.Response.StatusCode = flag.StatusCode;
				context.Response.ContentType = "application/json";
				await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(flag.Response)).ConfigureAwait(false);
				return;
			}
		}

		// Add evaluator to context
		context.Items["FeatureFlagEvaluator"] = new HttpContextFeatureFlagEvaluator(_featureFlags, tenantId, userId, attributes);
		_logger.LogDebug("Feature flag evaluator added to HttpContext.Items");

		_logger.LogDebug("FeatureFlagMiddleware completed processing, calling next middleware");
		await _next(context);
	}

	private string? ExtractTenantId(HttpContext context)
	{
		string? tenantId = null;
		if (_options.TenantIdExtractor != null)
		{
			try
			{
				tenantId = _options.TenantIdExtractor(context);
				_logger.LogDebug("Extracted tenant ID using custom extractor: {TenantId}", tenantId ?? "null");
				return tenantId;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error extracting tenant ID with custom extractor");
			}
		}

		tenantId = context.Request.Headers["X-Tenant-ID"].FirstOrDefault() ??
			   context.Request.Query["tenantId"].FirstOrDefault() ??
			   context.User.FindFirst("tenantId")?.Value;

		_logger.LogDebug("Extracted tenant ID using default extractors: {TenantId}", tenantId ?? "null");
		return tenantId;
	}

	private string? ExtractUserId(HttpContext context)
	{
		string? userId = null;
		if (_options.UserIdExtractor != null)
		{
			try
			{
				userId = _options.UserIdExtractor(context);
				_logger.LogDebug("Extracted user ID using custom extractor: {UserId}", userId ?? "null");
				return userId;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error extracting user ID with custom extractor");
			}
		}

		// Default extraction
		userId = context.User.Identity?.Name ??
			  context.Request.Headers["X-User-ID"].FirstOrDefault() ??
			  context.Request.Query["userId"].FirstOrDefault();

		_logger.LogDebug("Extracted user ID using default extractors: {UserId}", userId ?? "null");
		return userId;
	}

	private Dictionary<string, object>? ExtractAttributes(HttpContext context)
	{
		var attributes = new Dictionary<string, object>
		{
			["userAgent"] = context.Request.Headers["User-Agent"].ToString(),
			["ipAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
			["path"] = context.Request.Path,
			["method"] = context.Request.Method
		};
		_logger.LogDebug("Base attributes: {@Attributes}", attributes);

		// Add custom attributes using configured extractors
		if (_options.AttributeExtractors != null)
		{
			foreach (var extractor in _options.AttributeExtractors)
			{
				try
				{
					var customAttributes = extractor(context);
					foreach (var attr in customAttributes)
					{
						attributes[attr.Key] = attr.Value;
					}
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Error extracting custom attributes");
				}
			}
		}
		return attributes;
	}

	private async Task<bool> CheckMaintenanceMode(string? tenantId, string? userId, Dictionary<string, object> attributes)
	{
		if (!_options.EnableMaintenanceMode)
		{
			_logger.LogDebug("Maintenance mode is disabled");
			return false;
		}

		return await _globalFlags.IsEnabledAsync(flagKey: _options.MaintenanceFlagKey,
			tenantId: tenantId, userId: userId, attributes: attributes).ConfigureAwait(false);
	}
}
