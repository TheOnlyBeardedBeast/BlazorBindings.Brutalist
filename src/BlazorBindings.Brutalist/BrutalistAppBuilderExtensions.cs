// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace BlazorBindings.Brutalist;

public static class BrutalistAppBuilderExtensions
{
    public static IServiceCollection UseMauiBlazorBindings(this IServiceCollection services)
    {
        services
            .AddSingleton<NavigationManager>()
            .AddSingleton<BrutalistBlazorBindingsRenderer>();

        return services;
    }
}
