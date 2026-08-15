using System.Net;
using System.Threading.RateLimiting;
using DarsJadvali.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.RateLimiting;

namespace DarsJadvali.Web.Security;

/// <summary>
/// Veb qobiqning himoya qatlami: kalit bilan avtorizatsiya, cheklangan CORS
/// va so'rovlar chastotasi cheklovi.
/// </summary>
public static class SecurityExtensions
{
    /// <summary>CORS siyosati nomi.</summary>
    public const string CorsPolicyName = "DarsJadvaliLocal";

    /// <summary>Chastota cheklovi siyosati nomi.</summary>
    public const string RateLimitPolicyName = "DarsJadvaliFixed";

    /// <summary>CORS va chastota cheklovini ro'yxatdan o'tkazadi.</summary>
    public static IServiceCollection AddWebSecurity(this IServiceCollection services, WebSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);

        // CORS: ochiq emas. Sahifa serverning O'ZIDAN beriladi, ya'ni odatda
        // begona manba (origin) umuman kerak emas — ro'yxat bo'sh bo'lsa hech kim o'tmaydi.
        services.AddCors(cors => cors.AddPolicy(CorsPolicyName, policy =>
        {
            if (options.AllowedOrigins.Count == 0)
            {
                policy.WithOrigins(Array.Empty<string>());
                return;
            }

            policy
                .WithOrigins(options.AllowedOrigins.ToArray())
                .WithHeaders("Content-Type", LocalApiKey.HeaderName)
                .WithMethods("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE");
        }));

        // Chastota cheklovi: bitta IP daqiqasiga N ta so'rov. Bu lokal dastur uchun
        // shovqinli emas, ammo tarmoqdan kelgan avtomatik urinishlarni to'xtatadi.
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "noma'lum",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.RequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            limiter.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "So'rovlar juda tez-tez yuborilmoqda. Bir ozdan keyin qayta urinib ko'ring." },
                    ct);
            };
        });

        return services;
    }

    /// <summary>
    /// Kalitni tekshiruvchi oraliq qatlam. Yozuv so'rovlari (POST/PUT/PATCH/DELETE)
    /// kalitsiz 401 oladi; o'qish esa sozlamaga bog'liq.
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthorization(this IApplicationBuilder app, WebSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);

        return app.Use(async (context, next) =>
        {
            // Statik fayllar (sahifaning o'zi) tekshirilmaydi — tekshiruv API uchun.
            if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            // Kalitni olish uchun mo'ljallangan lokal endpoint o'zini tekshirmaydi
            // (uning himoyasi — faqat shu kompyuterdan kirish, qarang: MapSecurityEndpoints).
            if (context.Request.Path.StartsWithSegments("/api/security", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            var provided = context.Request.Headers[LocalApiKey.HeaderName].ToString();

            var decision = LocalApiKey.Evaluate(
                context.Request.Method,
                provided,
                options.ApiKey,
                options.RequireKeyForReads);

            if (decision == ApiKeyDecision.Unauthorized)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Ruxsat yo'q: so'rovda to'g'ri kalit (" + LocalApiKey.HeaderName + ") bo'lishi kerak.",
                });
                return;
            }

            await next(context);
        });
    }

    /// <summary>
    /// Xavfsizlik endpointlari.
    /// <c>GET /api/security/local-key</c> kalitni FAQAT shu kompyuterdagi (loopback)
    /// so'rovga beradi — shu tufayli brauzerdagi sahifa kalitni o'zi olib, foydalanuvchini
    /// bezovta qilmaydi, tarmoqdagi begona qurilma esa uni ololmaydi.
    /// </summary>
    public static void MapSecurityEndpoints(this IEndpointRouteBuilder api, WebSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(options);

        var group = api.MapGroup("/security");

        group.MapGet("/local-key", (HttpContext context) =>
        {
            if (!IsLoopback(context))
            {
                return Results.Json(
                    new { error = "Kalitni faqat shu kompyuterdan olish mumkin." },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(new
            {
                header = LocalApiKey.HeaderName,
                apiKey = options.ApiKey,
            });
        });

        group.MapGet("/status", () => Results.Ok(new
        {
            header = LocalApiKey.HeaderName,
            requireKeyForReads = options.RequireKeyForReads,
            networkExposed = options.IsNetworkExposed,
        }));
    }

    /// <summary>So'rov shu kompyuterning o'zidan kelganmi.</summary>
    private static bool IsLoopback(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;

        // Ba'zi hollarda (Unix soketi, sinov serveri) manzil umuman bo'lmaydi —
        // bunda ulanish jarayonning o'zi ichida bo'ladi.
        if (remote is null)
            return true;

        return IPAddress.IsLoopback(remote);
    }
}
