using FluentValidation;
using FluentValidation.AspNetCore;
using AuthService.Infrastructure;
using AuthService.Api.Infrastructure;
using AuthService.Api.Security;
using AuthService.Infrastructure.Authentication;
using AuthService.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddRateLimiter(RateLimitingConfiguration.Configure);
builder.Services.AddSingleton<CsrfOriginValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();

builder.Services.AddValidatorsFromAssemblyContaining<
    AuthService.Application.Validators.Auth.RegisterRequestValidator>();



builder.Services.AddInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var internalJwt = builder.Configuration.GetSection(InternalJwtOptions.SectionName).Get<InternalJwtOptions>() ?? new InternalJwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningSecret))
    throw new InvalidOperationException("JWT signing secret is not configured.");
if (string.IsNullOrWhiteSpace(internalJwt.SigningSecret))
    throw new InvalidOperationException("Internal JWT signing secret is not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningSecret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub", RoleClaimType = "role"
        };
        options.Events = ApiJwtBearerEvents.Create(userAccessScheme: true);
    })
    .AddJwtBearer(InternalAuthConstants.Scheme, options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = internalJwt.Issuer,
            ValidateAudience = true, ValidAudience = internalJwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(internalJwt.SigningSecret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub"
        };
        options.Events = ApiJwtBearerEvents.Create(userAccessScheme: false);
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(InternalAuthConstants.UsersReadPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(InternalAuthConstants.Scheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(InternalAuthConstants.TokenUseClaim, InternalAuthConstants.TokenUseService);
        policy.RequireClaim(InternalAuthConstants.PermissionClaim, InternalAuthConstants.UsersRead);
    });
    options.AddPolicy(InternalAuthConstants.UsersManagePolicy, policy =>
    {
        policy.AddAuthenticationSchemes(InternalAuthConstants.Scheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(InternalAuthConstants.TokenUseClaim, InternalAuthConstants.TokenUseService);
        policy.RequireClaim(InternalAuthConstants.PermissionClaim, InternalAuthConstants.UsersManage);
    });
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.Configure<ApiBehaviorOptions>(ApiPipelineConfiguration.ConfigureInvalidModelState);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    options.AddSecurityDefinition("InternalService", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "Short-lived service token from POST /internal/auth/token."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});

builder.Services.AddCors(options => ApiPipelineConfiguration.AddCorsPolicy(
    options, builder.Configuration, builder.Environment));

builder.Services.AddHealthChecks();


var app = builder.Build();

app.UseExceptionHandler();


app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(ApiPipelineConfiguration.CorsPolicyName);

app.UseHttpsRedirection();

app.UseStatusCodePages(statusCodeContext =>
    ApiPipelineConfiguration.WriteStatusCodeErrorAsync(statusCodeContext.HttpContext.Response));

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

public partial class Program;

internal static class ApiJwtBearerEvents
{
    public static JwtBearerEvents Create(bool userAccessScheme) => new()
    {
        OnTokenValidated = context =>
        {
            if (!userAccessScheme) return Task.CompletedTask;
            var tokenUse = context.Principal?.FindFirst(InternalAuthConstants.TokenUseClaim)?.Value;
            if (tokenUse is not null && !string.Equals(tokenUse, InternalAuthConstants.TokenUseAccess, StringComparison.Ordinal))
                context.Fail("Unexpected token type.");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            context.HandleResponse();
            return ApiErrorWriter.WriteAsync(context.Response, StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED", "Потрібна автентифікація.");
        },
        OnForbidden = context => ApiErrorWriter.WriteAsync(context.Response, StatusCodes.Status403Forbidden,
            "FORBIDDEN", "У вас недостатньо прав для виконання цієї дії.")
    };
}
