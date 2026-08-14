using System.Text.Json.Serialization;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.DependencyInjection;
using DarsJadvali.Infrastructure.DependencyInjection;
using DarsJadvali.Web.Endpoints;

// ---------------------------------------------------------------------------
// DarsJadvali.Web — localhost test harness.
// WPF (DarsJadvali.UI) faqat Windows'da ishlaydi, shuning uchun AYNI
// Application + Infrastructure qatlamlari ustiga yupqa veb qobiq qurilgan.
// Ishga tushirish:  dotnet run --project src/DarsJadvali.Web
// Boshqa baza bilan: dotnet run --project src/DarsJadvali.Web -- --db /tmp/test.db
// ---------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// Baza yo'li: --db argumenti > DARSJADVALI_DB muhit o'zgaruvchisi > WPF bilan umumiy standart yo'l.
var dbPath = builder.Configuration["db"];
if (string.IsNullOrWhiteSpace(dbPath))
    dbPath = Environment.GetEnvironmentVariable("DARSJADVALI_DB");
if (string.IsNullOrWhiteSpace(dbPath))
    dbPath = InfrastructureServiceRegistration.DefaultDbPath;

// Manzil: --urls argumenti (yoki ASPNETCORE_URLS) berilgan bo'lsa — o'shanga hurmat qilinadi,
// aks holda standart http://localhost:5080. Bu bir vaqtda ikkinchi nusxani boshqa portda
// (masalan test uchun) ishga tushirish imkonini beradi.
var urls = builder.Configuration["urls"];
if (string.IsNullOrWhiteSpace(urls))
{
    urls = "http://localhost:5080";
    builder.WebHost.UseUrls(urls);
}

builder.Services.AddApplication();
builder.Services.AddInfrastructureSqlite(dbPath);

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

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapCatalogEndpoints();
api.MapAssignmentEndpoints();
api.MapSettingsEndpoints();
api.MapScheduleEndpoints();
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

app.Run();
