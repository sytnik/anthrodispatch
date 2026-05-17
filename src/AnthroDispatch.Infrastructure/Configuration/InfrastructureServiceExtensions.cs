using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Application.DataPreparation;
using AnthroDispatch.Infrastructure.Data;
using AnthroDispatch.Infrastructure.MockData;
using AnthroDispatch.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AnthroDispatch.Infrastructure.Configuration;

public static class InfrastructureServiceExtensions
{
    public static void AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AnthroDispatchDbContext>(options =>
            options.UseInMemoryDatabase("AnthroDispatchPrototype"));

        services.AddScoped(typeof(IRepository<>), typeof(AppRepository<>));
        services.AddScoped<IMockDatasetGenerator, MockDatasetGenerator>();
        services.AddScoped<IAnthroDispatchMockDataGenerator, AnthroDispatchMockDataGenerator>();

        // DispatchProblem cache (singleton — lives for the lifetime of the process)
        services.TryAddSingleton<DispatchProblemCache>();

        // DataPreparation services
        services.AddTransient<ICurriculumHoursCalculator, CurriculumHoursCalculator>();
        services.AddTransient<AssignmentExpander>();
        services.AddTransient<OperationalDataValidator>();
        services.AddTransient<IDispatchInputBuilder, DispatchInputBuilder>();
    }
}