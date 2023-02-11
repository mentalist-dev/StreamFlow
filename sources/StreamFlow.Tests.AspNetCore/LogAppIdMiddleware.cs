namespace StreamFlow.Tests.AspNetCore;

public class LogAppIdMiddleware : IStreamFlowMiddleware
{
    private readonly ILogger<LogAppIdMiddleware> _logger;

    public LogAppIdMiddleware(ILogger<LogAppIdMiddleware> logger)
    {
        _logger = logger;
    }

    public Task Invoke(IMessageContext context, Func<IMessageContext, Task> next)
    {
        if (!string.IsNullOrWhiteSpace(context.AppId))
        {
            _logger.LogInformation("AppId: {AppId}", context.AppId);
        }

        var value = context.GetHeader("not-existent", "not found");
        _logger.LogInformation("not-existent header value: {HeaderValue}", value);

        var customAppName = context.GetHeader("custom_app_name", "");
        _logger.LogInformation("custom app name header value: {CustomAppName}", customAppName);

        var customAppId = context.GetHeader("custom_app_id", Guid.Empty);
        _logger.LogInformation("custom app id header value: {CustomAppId}", customAppId);

        var customAppIdString = context.GetHeader("custom_app_id", string.Empty);
        _logger.LogInformation("custom app id (string) header value: {CustomAppId}", customAppIdString);

        var index = context.GetHeader("index", string.Empty);
        _logger.LogInformation("index header value: {Index}", index);

        var indexId = context.GetHeader("index-id", string.Empty);
        _logger.LogInformation("index-id header value: {IndexId}", indexId);

        var priority = context.GetHeader("check-priority", string.Empty);
        _logger.LogInformation("check-priority header value: {Priority}", priority);

        var state = new List<KeyValuePair<string, object>> { new("Account", "Account Name") };

        using (_logger.BeginScope(state))
        {
            return next(context);
        }
    }
}

public class SetAppIdMiddleware : IStreamFlowMiddleware
{
    private readonly string _appId;
    private readonly string _customAppName;

    public SetAppIdMiddleware(string appId, string customAppName)
    {
        _appId = appId;
        _customAppName = customAppName;
    }

    public Task Invoke(IMessageContext context, Func<IMessageContext, Task> next)
    {
        context.WithAppId(_appId);
        context.SetHeader("custom_app_name", _customAppName);
        context.SetHeader("custom_app_id", Guid.NewGuid());
        context.SetHeader("check-priority", "set-inside-middleware");
        return next(context);
    }
}
