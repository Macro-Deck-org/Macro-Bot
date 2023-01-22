using Discord;
using Discord.Interactions;
using MacroBot.DataAccess.RepositoryInterfaces;

namespace MacroBot.Extensions;

public static class IGuildUserExtensions
{
    public static async Task<string?> Report(this IGuildUser user, IGuildUser author, string reason, IServiceScopeFactory _serviceScopeFactory) {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var reportRepository = scope.ServiceProvider.GetRequiredService<IReportRepository>();
        return await reportRepository.CreateReport(author.Id, user.Id, reason, null);
    }
}