using Microsoft.Extensions.DependencyInjection;

namespace HMS.Modules.Telemetry
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddTelemetryModule(this IServiceCollection services)
        {
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
            return services;
        }
    }
}
