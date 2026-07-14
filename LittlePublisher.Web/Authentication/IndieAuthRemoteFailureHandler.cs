using Microsoft.AspNetCore.Authentication;

namespace LittlePublisher.Web.Authentication
{
    public static class IndieAuthRemoteFailureHandler
    {
        public const string LoginFailureMessage = "Unable to start IndieAuth login for the configured website";

        public static Task HandleAsync(RemoteFailureContext context)
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("IndieAuth");

            logger.LogWarning(context.Failure, "IndieAuth remote authentication failed");

            context.Response.Redirect($"/login?error={Uri.EscapeDataString(LoginFailureMessage)}");
            context.HandleResponse();

            return Task.CompletedTask;
        }
    }
}
