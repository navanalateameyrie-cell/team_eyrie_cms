using Microsoft.Extensions.DependencyInjection;

namespace CMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Reserved for MediatR/validators/handlers in later phases.
        return services;
    }
}
