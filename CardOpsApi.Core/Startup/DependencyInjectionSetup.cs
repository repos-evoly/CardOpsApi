using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardOpsApi.Data.Context;
using CardOpsApi.Core.Dtos;

using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.DependencyInjection;
using CardOpsApi.Core.Filters;
using CardOpsApi.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using CardOpsApi.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using CardOpsApi.Data.Seeding;
using CardOpsApi.Data.Repositories;
using CardOpsApi.Data.Abstractions;
using CardOpsApi.Validators;
using CardOpsApi.Core.Validators;




namespace CardOpsApi.Core.Startup
{
  public static class DependencyInjectionSetup
  {
    public static WebApplicationBuilder RegisterServices(this WebApplicationBuilder builder)
    {
      var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new ArgumentNullException("Jwt:Issuer is missing in configuration.");
      var audience = builder.Configuration["Jwt:Audience"] ?? throw new ArgumentNullException("Jwt:Audience is missing in configuration.");
      var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key is missing in configuration.");

      builder.Services.RegisterCors();
      builder.Services.RegisterSwagger();
      builder.Services.RegisterAuths(issuer, audience, jwtKey);
      if (builder.Environment.IsDevelopment())
      {
        builder.Services.AddDbContext<CardOpsApiDbContext>(opt => opt.UseSqlServer(builder.Configuration["ConnectionStrings:DevConnection"]));
      }
      else if (builder.Environment.IsStaging())
      {
        builder.Services.AddDbContext<CardOpsApiDbContext>(opt => opt.UseSqlServer(builder.Configuration["ConnectionStrings:StagingConnection"]));
      }
      else
      {
        builder.Services.AddDbContext<CardOpsApiDbContext>(opt =>
        {
          opt.UseSqlServer(builder.Configuration["ConnectionStrings:ProdConnection"]);
        });
      }
      builder.Services.AddHttpContextAccessor();
      builder.Services.AddTransient<DataSeeder>();
      builder.Services.AddAutoMapper(typeof(MappingConfig));
      builder.Services.Configure<JsonOptions>(options =>
      {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.WriteIndented = true;
        options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
      });

      builder.Services.RegisterValidators();
      builder.Services.AddSignalR();

      // Optionally, add your NotificationService to DI:

      builder.Services.RegisterRepos();
      return builder;
    }

    public static IServiceCollection RegisterCors(this IServiceCollection cors)
    {
      cors.AddCors(options =>
            {
              options.AddPolicy("AllowSpecificOrigins",
                    builder =>
                    {
                      builder.WithOrigins("http://localhost:3000",
                                          "http://localhost:3010",
                                          "http://10.3.3.11:3010",
                                          "http://localhost:3011",
                                          "http://10.3.3.11:3011",
                                          "http://localhost:5000",
                                          "http://10.1.1.205:3010",
                                          "http://10.1.1.205:3011")
                             .AllowAnyHeader()
                             .AllowAnyMethod()
                             .AllowCredentials();
                    });
            });
      return cors;
    }

    public static IServiceCollection RegisterAuths(this IServiceCollection auth, string issuer, string audience, string jwtKey)
    {
      auth.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
          .AddJwtBearer(options =>
          {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters()
            {
              ValidateIssuer = true,
              ValidateAudience = true,
              ValidateLifetime = true,
              ValidateIssuerSigningKey = true,

              ValidIssuer = issuer,
              ValidAudience = audience,
              IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };

            options.Events = new JwtBearerEvents
            {
              OnMessageReceived = context =>
                {
                  // Get token from Authorization header
                  var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                  if (authHeader != null && authHeader.StartsWith("Bearer "))
                  {
                    context.Token = authHeader.Substring("Bearer ".Length).Trim();
                  }
                  return Task.CompletedTask;
                }
            };
          });

      auth.AddAuthorization(a =>
     {
       a.AddPolicy("requireAuthUser", b => b
             .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
             .RequireAuthenticatedUser());


       a.AddPolicy("CanUsers", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanDefinitions", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanArea", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanBranches", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanReasons", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanSources", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanRoles", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanPermissions", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanDocuments", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanBankDocuments", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanUserActivity", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
       a.AddPolicy("CanSettings", b => b.RequireRole(
           "SuperAdmin", "Admin", "Maker", "Manager", "AssistantManager", "DeputyManager", "Checker", "Viewer", "Auditor"));
     });
      return auth;
    }


    public static IServiceCollection RegisterValidators(this IServiceCollection validators)
    {

      validators.AddScoped<IValidator<DefinitionCreateDto>, DefinitionCreateDtoValidator>();
      validators.AddScoped<IValidator<DefinitionUpdateDto>, DefinitionUpdateDtoValidator>();
      validators.AddScoped<IValidator<TransactionCreateDto>, TransactionCreateDtoValidator>();
      validators.AddScoped<IValidator<TransactionUpdateDto>, TransactionUpdateDtoValidator>();
      validators.AddScoped<IValidator<CurrencyCreateDto>, CurrencyCreateDtoValidator>();
      validators.AddScoped<IValidator<CurrencyUpdateDto>, CurrencyUpdateDtoValidator>();


      return validators;
    }

    public static IServiceCollection RegisterRepos(this IServiceCollection services)
    {
      services.AddEndpointsApiExplorer();
      services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

      ;
      services.AddScoped<IUserRepository, UserRepository>();
      services.AddScoped<IRoleRepository, RoleRepository>();
      services.AddScoped<ITransactionRepository, TransactionRepository>();
      services.AddScoped<IDefinitionRepository, DefinitionRepository>();
      services.AddScoped<ISettingsRepository, SettingsRepository>();
      services.AddScoped<ICurrencyRepository, CurrencyRepository>();
      services.AddScoped<IReasonRepository, ReasonRepository>();
      services.AddHttpClient<IUserRepository, UserRepository>();
      services.AddScoped<IAuditLogRepository, AuditLogRepository>();



      services.AddTransient<IUnitOfWork, UnitOfWork>();

      return services;
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
          //services.AddHostedService<EscalationTimeoutService>();
          // Add other services, repositories, etc.
        });


    public static IServiceCollection RegisterSwagger(this IServiceCollection services)
    {
      services.AddSwaggerGen(options =>
          {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
              Scheme = "Bearer",
              BearerFormat = "JWT",
              In = ParameterLocation.Header,
              Name = "Authorization",
              Description = "Bearer Authentication with JWT Token",
              Type = SecuritySchemeType.Http
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
              {
                new OpenApiSecurityScheme
                {
                  Reference = new OpenApiReference
                  {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme
                  }
                },
                new List<string>()
              }
            });
            options.OperationFilter<FileUploadOperationFilter>();
          });

      return services;
    }
  }
}