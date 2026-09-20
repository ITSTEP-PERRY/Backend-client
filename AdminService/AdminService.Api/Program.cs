using System.Text;
using System.Text.Json.Serialization;
using AdminService.Api;
using AdminService.Api.Authorization;
using AdminService.Api.ErrorHandling;
using AdminService.Api.Models;
using AdminService.Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<
    AdminService.Application.Validators.Users.GetUsersRequestValidator>();

builder.Services.AddInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningSecret))
    throw new InvalidOperationException("JWT signing secret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningSecret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var tokenUse = context.Principal?.FindFirst("token_use")?.Value;
                if (tokenUse is not null && !string.Equals(tokenUse, "access", StringComparison.Ordinal))
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
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy(AdminAuthorizationPolicies.AdminAccess, policy =>
        policy.RequireAuthenticatedUser().RequireRole("Admin")));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.Configure<ApiBehaviorOptions>(ApiPipelineConfiguration.ConfigureInvalidModelState);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "User access token issued by AuthService. The AdminAccess policy requires role=Admin."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});
builder.Services.AddHealthChecks();
builder.Services.AddCors(options => ApiPipelineConfiguration.AddCorsPolicy(
    options, builder.Configuration, builder.Environment));

var app = builder.Build();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(ApiPipelineConfiguration.CorsPolicyName);
app.UseHttpsRedirection();
app.UseStatusCodePages(statusCodeContext =>
    ApiPipelineConfiguration.WriteStatusCodeErrorAsync(statusCodeContext.HttpContext.Response));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program;

internal sealed class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
}
