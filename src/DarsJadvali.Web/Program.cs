using System.Text.Json.Serialization;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.DependencyInjection;
using DarsJadvali.Infrastructure.DependencyInjection;
using DarsJadvali.Web.Endpoints;
using DarsJadvali.Web.Security;
using DarsJadvali.Web.Services;

// ---------------------------------------------------------------------------
// DarsJadvali.Web — localhost test harness.
// WPF (DarsJadvali.UI) faqat Windows'da ishlaydi, shuning uchun AYNI
// Application + Infrastructure qatlamlari ustiga yupqa veb qobiq qurilgan.
// Ishga tushirish:  dotnet run --project src/DarsJadvali.Web
// Boshqa baza bilan: dotnet run --project src/DarsJadvali.Web -- --db /tmp/test.db
// ---------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// Baza yo'li: --db argumenti > ConnectionStrings:DarsJadvali (to'liq ulanish satri) >
// Database:Path sozlamasi > DARSJADVALI_DB muhit o'zgaruvchisi > WPF bilan umumiy standart yo'l.
var connectionString = builder.Configuration.GetConnectionString("DarsJadvali");

var dbPath = builder.Configuration["db"];
if (string.IsNullOrWhiteSpace(dbPath))
    dbPath = builder.Configuration["Database:Path"];
if (string.IsNullOrWhiteSpace(dbPath))
    dbPath = Environment.GetEnvironmentVariable("DARSJADVALI_DB");
if (string.IsNullOrWhiteSpace(dbPath))
    dbPath = InfrastructureServiceRegistration.DefaultDbPath;

// Manzil: --urls argumenti (yoki ASPNETCORE_URLS) berilgan bo'lsa — o'shanga hurmat qilinadi,
// aks holda standart http://127.0.0.1:5080. STANDART BOG'LANISH FAQAT SHU KOMPYUTER:
// "localhost" o'rniga aynan 127.0.0.1 — tasodifan tarmoqqa ochilib qolmasligi uchun.
var urls = builder.Configuration["urls"];
if (string.IsNullOrWhiteSpace(urls))
{
    urls = WebSecurityOptions.DefaultUrl;
    builder.WebHost.UseUrls(urls);
}

var security = WebSecurityOptions.Load(builder.Configuration, dbPath, urls);

builder.Services.AddApplication();

if (string.IsNullOrWhiteSpace(connectionString))
    builder.Services.AddInfrastructureSqlite(dbPath);
else
    builder.Services.AddInfrastructure(connectionString);

builder.Services.AddWebSecurity(security);

// Generatsiya uzoq davom etadi — HTTP so'rovi uni kutmaydi, fon rejimida ishlaydi.
builder.Services.AddSingleton<GenerationJobs>();

// Enum'lar satr sifatida ("Dushanba"), TimeSpan esa DTO ichida "HH:mm" satriga aylantiriladi.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.WriteIndented = false;
});

var app = builder.Build();

// Global xato ushlagich: xato { error: "..." } ko'rinishida qaytadi.
// Status kod xato turiga qarab tanlanadi: so'rovning o'zi buzuq bo'lsa (buzuq JSON,
// noto'g'ri enum qiymati va h.k.) ASP.NET BadHttpRequestException ni o'zida 400 bilan
// tashlaydi — uni 500 qilib yubormaymiz. Foydalanuvchiga faqat o'zbekcha umumiy xabar
// beriladi; ichki (inglizcha) tafsilot faqat server logiga yoziladi.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DarsJadvali.Web");

        var statusCode = ex is BadHttpRequestException bad
            ? bad.StatusCode
            : StatusCodes.Status500InternalServerError;

        if (statusCode >= 500)
            logger.LogError(ex, "So'rovda kutilmagan xato: {Path}", context.Request.Path);
        else
            logger.LogWarning(ex, "Noto'g'ri so'rov: {Path}", context.Request.Path);

        if (context.Response.HasStarted) throw;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new { error = UserMessage(statusCode) });
    }
});

// Foydalanuvchiga ko'rinadigan o'zbekcha xabar (ichki tafsilotlarsiz).
static string UserMessage(int statusCode) => statusCode switch
{
    StatusCodes.Status400BadRequest => "So'rov ma'lumotlari noto'g'ri.",
    StatusCodes.Status404NotFound => "So'ralgan ma'lumot topilmadi.",
    StatusCodes.Status413PayloadTooLarge => "So'rov hajmi juda katta.",
    StatusCodes.Status415UnsupportedMediaType => "So'rov turi qo'llab-quvvatlanmaydi.",
    < 500 => "So'rovni bajarib bo'lmadi.",
    _ => "Serverda kutilmagan xatolik yuz berdi."
};

// Tartib muhim: HTTPS > chastota cheklovi > CORS > kalit tekshiruvi > statik fayllar > API.
if (security.RequireHttps)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseCors(SecurityExtensions.CorsPolicyName);
app.UseApiKeyAuthorization(security);

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapSecurityEndpoints(security);
api.MapCatalogEndpoints();
api.MapAssignmentEndpoints();
api.MapSettingsEndpoints();

// YANGI yo'l — Card/Lesson modeli (juft dars, A/B hafta, guruh, qulf, generatsiya).
api.MapBoardEndpoints();

// ESKI yo'l — ScheduleEntry modeli. Sahifaning o'zi (wwwroot/index.html) hali shu
// yo'lni 8 ta joyda chaqirgani uchun yonma-yon qoldirildi; har javobda "Deprecation"
// sarlavhasi bilan belgilanadi va sahifa ko'chgach o'chadi.
#pragma warning disable CS0618 // Eskirgan — ataylab, ko'chish davrida.
api.MapScheduleEndpoints();
#pragma warning restore CS0618

api.MapScheduleSetEndpoints();
api.MapAboutEndpoints(dbPath);

// Bazani yaratish / migratsiya qilish / boshlang'ich ma'lumot bilan to'ldirish.
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.Logger.LogInformation("Baza fayli: {DbPath}", dbPath);
app.Logger.LogInformation("Dastur manzili: {Urls}", urls);

// Kalit birinchi ishga tushishda yaratiladi va AYNAN SHU YERDA bir marta ko'rsatiladi.
if (security.ApiKeyCreated)
{
    app.Logger.LogWarning(
        "Yangi API kalit yaratildi: {ApiKey}\nSaqlangan fayl: {KeyFile}\n" +
        "Yozuv so'rovlarida «{Header}» sarlavhasida shu kalit yuborilishi kerak.",
        security.ApiKey,
        security.ApiKeyFilePath,
        LocalApiKey.HeaderName);
}
else if (security.ApiKeyFilePath is not null)
{
    app.Logger.LogInformation("API kalit fayldan o'qildi: {KeyFile}", security.ApiKeyFilePath);
}
else
{
    app.Logger.LogInformation("API kalit sozlamadan olindi (Security:ApiKey / DARSJADVALI_API_KEY).");
}

if (security.IsNetworkExposed)
{
    app.Logger.LogWarning(
        "DIQQAT: dastur tarmoqqa ochilgan ({Urls}). Trafik shifrlanmagan (HTTP) — " +
        "faqat ishonchli lokal tarmoqda ishlating yoki manzilni 127.0.0.1 ga qaytaring.",
        urls);
}

app.Run();
