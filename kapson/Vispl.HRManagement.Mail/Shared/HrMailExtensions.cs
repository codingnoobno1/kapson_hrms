using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vispl.HRManagement.BM.ITF.Shared;

namespace Vispl.HRManagement.Mail.Shared;

public static class HrMailExtensions
{
    public static IServiceCollection AddHrMail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));

        services.AddHttpClient<IEmailSender, MailServiceEmailSender>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<MailOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        // The read side. Separate client because the two talk to different services and time
        // out for different reasons — a slow inbox must never hold up a send.
        services.AddHttpClient<IMailInbox, MailInboxClient>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<MailOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        return services;
    }
}
