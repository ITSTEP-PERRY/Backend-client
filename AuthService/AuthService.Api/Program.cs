using FluentValidation;
using FluentValidation.AspNetCore;
using AuthService.Infrastructure;
using AuthService.Api.Infrastructure;
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
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub", RoleClaimType = "role"
        };
        options.Events = ApiJwtBearerEvents.Create();
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
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub"
        };
        options.Events = ApiJwtBearerEvents.Create();
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
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The supplied value is invalid."
                        : error.ErrorMessage)
                    .ToArray());
        return new BadRequestObjectResult(new ApiErrorResponse
        {
            Code = "VALIDATION_ERROR",
            Message = "One or more validation errors occurred.",
            Errors = errors
        });
    };
});

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
            policy.WithOrigins(origins).AllowCredentials();
        else
            policy.AllowAnyOrigin();
        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks();


var app = builder.Build();

app.UseExceptionHandler();


app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");

app.UseHttpsRedirection();

app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    if (response.StatusCode == StatusCodes.Status404NotFound)
        await ApiErrorWriter.WriteAsync(response, response.StatusCode, "NOT_FOUND", "Resource was not found.");
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

public partial class Program;

internal static class ApiJwtBearerEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            return ApiErrorWriter.WriteAsync(context.Response, StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED", "Authentication is required.");
        },
        OnForbidden = context => ApiErrorWriter.WriteAsync(context.Response, StatusCodes.Status403Forbidden,
            "FORBIDDEN", "You do not have permission to perform this action.")
    };
}
