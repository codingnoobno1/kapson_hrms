using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vispl.HRManagement.BM.ITF.Candidate;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.Mail.Shared;

namespace Vispl.HRManagement.Identity.Candidate;

/// <summary>
/// Issues the activation link and mails it. Lives here because this is where the identity side
/// already is; the mail side is a leaf the whole application shares.
/// </summary>
public sealed class CandidateInvitation(
    ICandidateActivationService activation,
    IEmailSender mailer,
    IOptions<MailOptions> mail,
    IHostEnvironment environment,
    ILogger<CandidateInvitation> logger) : ICandidateInvitation
{
    public async Task InviteAsync(
        string tenantKey,
        string email,
        string candidateName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await activation.RequestAsync(tenantKey, email, cancellationToken);

            if (token is null)
            {
                // No candidate, or already activated. Both are silent by design: saying which
                // would turn this into a way of testing whether an address has applied.
                return;
            }

            var link = CandidateLinks.Activation(mail.Value.PublicBaseUrl, tenantKey, token);

            var sent = await mailer.SendAsync(
                CandidateMail.Activation(email, candidateName, tenantKey, link), cancellationToken);

            if (sent.Sent)
            {
                return;
            }

            // Logged with the link in Development only, so a developer with no mail service
            // running can still finish the flow. Never anywhere else: an activation link in a
            // log file is a way in.
            if (environment.IsDevelopment())
            {
                logger.LogWarning(
                    "Mail did not send ({Reason}). Activation link for {Email}: {Link}",
                    sent.Failure, email, link);
            }
            else
            {
                logger.LogError(
                    "Could not mail the activation link to {Email}: {Reason}", email, sent.Failure);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Could not invite {Email}. Their application was still accepted.", email);
        }
    }
}
