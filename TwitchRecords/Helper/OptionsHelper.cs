using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchRecords.Helper;

public static class OptionsHelper
{
    public static IServiceCollection AddValidatedOptions<T>(this IServiceCollection services, IConfiguration section) where T : class
    {
        services.AddOptions<T>()
        .Bind(section)
        .ValidateDataAnnotations()
        .ValidateOnStart(); // Не хочет работать никак.

        return services;
    }
}
