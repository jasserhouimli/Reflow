using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Modules.Identity.Domain;
using Reflow.Modules.Identity.Features.Register;
using Reflow.Modules.Identity.Features.Login;
using Reflow.Modules.Identity.Features.GetUser;
using Reflow.Modules.Identity.Features.Logout;
using Reflow.Modules.Identity.Features.RefreshToken;
using Reflow.Modules.Identity.Persistence;
using Reflow.Modules.Identity.Services;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.Identity;

public static class IdentityModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Reflow");

        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, b => b.MigrationsAssembly("Reflow.Api")));

        builder.Services.AddIdentityCore<User>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddSignInManager<SignInManager<User>>();

        builder.Services.AddValidatorsFromAssemblyContaining<IdentityDbContext>();
        builder.Services.AddScoped<IdentityDbContext>();
        builder.Services.AddScoped<TokenService>();

        builder.Services.AddScoped<RegisterHandler>();
        builder.Services.AddScoped<LoginHandler>();
        builder.Services.AddScoped<GetCurrentUserHandler>();
        builder.Services.AddScoped<RefreshTokenHandler>();
        builder.Services.AddScoped<LogoutHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        RegisterEndpoint.Map(app);
        LoginEndpoint.Map(app);
        GetCurrentUserEndpoint.Map(app);
        RefreshTokenEndpoint.Map(app);
        LogoutEndpoint.Map(app);
    }
}
