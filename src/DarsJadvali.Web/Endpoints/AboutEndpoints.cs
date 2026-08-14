using DarsJadvali.Domain.Common;
using DarsJadvali.Web.Dtos;

namespace DarsJadvali.Web.Endpoints;

/// <summary>"Dastur haqida" ma'lumotlari — yagona manba: AppInfo.</summary>
public static class AboutEndpoints
{
    public static void MapAboutEndpoints(this IEndpointRouteBuilder api, string dbPath)
    {
        api.MapGet("/about", () => Results.Ok(new AboutDto(
            AppInfo.AppName,
            AppInfo.Version,
            AppInfo.Author,
            AppInfo.Description,
            AppInfo.TelegramUrl,
            AppInfo.TelegramHandle,
            AppInfo.DonateCardNumber,
            AppInfo.DonateCardType,
            AppInfo.DonateCardHolder,
            dbPath)));
    }
}
