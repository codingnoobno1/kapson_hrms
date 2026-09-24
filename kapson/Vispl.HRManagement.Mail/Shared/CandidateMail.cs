using System.Net;
using Vispl.HRManagement.BM.ITF.Shared;

namespace Vispl.HRManagement.Mail.Shared;

/// <summary>
/// The messages a candidate receives.
///
/// Written here rather than in a template file because there are two of them and both are
/// mostly one link. Every value that reaches the HTML is escaped: a candidate supplies their
/// own name, and a name is not a safe thing to concatenate into markup.
/// </summary>
public static class CandidateMail
{
    /// <summary>
    /// Sent when somebody applies. The link is what makes the rest of the pipeline reachable —
    /// an offer can only be accepted while signed in — so the mail leads with it rather than
    /// burying it under a paragraph of welcome.
    /// </summary>
    public static EmailMessage Activation(
        string toAddress, string candidateName, string companyName, string activationUrl)
    {
        var subject = $"Set up your {companyName} application account";

        var text =
            $"""
             Hello {candidateName},

             Thanks for applying to {companyName}. Set a password to track your application
             and complete your joining formalities:

             {activationUrl}

             The link is valid for 24 hours. If it expires you can request another from the
             onboarding page.

             You will not be asked for a payment at any stage. If anyone asks you for one,
             it is not us.

             {companyName}
             """;

        var html =
            $"""
             <p>Hello {Escape(candidateName)},</p>
             <p>Thanks for applying to {Escape(companyName)}. Set a password to track your
             application and complete your joining formalities.</p>
             <p><a href="{Escape(activationUrl)}"
                   style="display:inline-block;padding:11px 20px;border-radius:8px;
                          background:#2563eb;color:#ffffff;font-weight:600;
                          text-decoration:none">Set my password</a></p>
             <p style="color:#64748b;font-size:13px">
               Or paste this into your browser:<br />
               <span style="word-break:break-all">{Escape(activationUrl)}</span>
             </p>
             <p style="color:#64748b;font-size:13px">
               The link is valid for 24 hours. If it expires you can request another from the
               onboarding page.
             </p>
             <p style="color:#64748b;font-size:13px">
               You will not be asked for a payment at any stage. If anyone asks you for one,
               it is not us.
             </p>
             <p>{Escape(companyName)}</p>
             """;

        return new EmailMessage(toAddress, candidateName, subject, text, html);
    }

    /// <summary>
    /// Sent when an offer is accepted and the checklist appears. Onboarding is the step where
    /// candidates go quiet, so this says what is outstanding rather than only that it exists.
    /// </summary>
    public static EmailMessage OnboardingReady(
        string toAddress, string candidateName, string companyName, string onboardingUrl, int outstanding)
    {
        var subject = $"Your {companyName} joining checklist is ready";

        var work = outstanding == 1
            ? "There is 1 item left for you to complete."
            : $"There are {outstanding} items left for you to complete.";

        var text =
            $"""
             Hello {candidateName},

             Welcome aboard. Your joining checklist is ready. {work}

             {onboardingUrl}

             {companyName}
             """;

        var html =
            $"""
             <p>Hello {Escape(candidateName)},</p>
             <p>Welcome aboard. Your joining checklist is ready. {Escape(work)}</p>
             <p><a href="{Escape(onboardingUrl)}"
                   style="display:inline-block;padding:11px 20px;border-radius:8px;
                          background:#2563eb;color:#ffffff;font-weight:600;
                          text-decoration:none">Open my checklist</a></p>
             <p>{Escape(companyName)}</p>
             """;

        return new EmailMessage(toAddress, candidateName, subject, text, html);
    }

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
