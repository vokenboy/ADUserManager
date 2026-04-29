using ActiveManager.Services.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ActiveManager.Services;

public class EmailNotificationService
{
    /// <summary>
    /// Sends a termination confirmation email. Never throws — failures are logged and swallowed.
    /// </summary>
    public async Task SendTerminationNotificationAsync(TerminationRecord record)
    {
        var settings = AppSettings.Instance.Email;
        if (!settings.Enabled || !settings.NotifyOnTermination) return;

        var recipients = settings.GetRecipientList();
        if (recipients.Count == 0) return;

        var hasFailures = record.StepResults.Any(s => !s.Success);
        var subject = hasFailures
            ? $"[ActiveManager] Termination completed with errors — {record.DisplayName}"
            : $"[ActiveManager] Termination completed — {record.DisplayName}";

        var body = BuildTerminationBody(record, hasFailures);

        await SendAsync(settings, recipients, subject, body);
    }

    /// <summary>
    /// Sends a rollback alert email. Never throws — failures are logged and swallowed.
    /// </summary>
    public async Task SendRollbackNotificationAsync(TerminationRecord record, bool wasSuccessful)
    {
        var settings = AppSettings.Instance.Email;
        if (!settings.Enabled || !settings.NotifyOnRollback) return;

        var recipients = settings.GetRecipientList();
        if (recipients.Count == 0) return;

        var subject = wasSuccessful
            ? $"[ActiveManager] Rollback completed — {record.DisplayName}"
            : $"[ActiveManager] Rollback completed with errors — {record.DisplayName}";

        var body = BuildRollbackBody(record, wasSuccessful);

        await SendAsync(settings, recipients, subject, body);
    }

    /// <summary>
    /// Sends a test email to verify SMTP settings. Returns null on success or an error message.
    /// </summary>
    public async Task<string?> SendTestEmailAsync()
    {
        var settings = AppSettings.Instance.Email;
        var recipients = settings.GetRecipientList();
        if (recipients.Count == 0) return "No recipients configured.";

        try
        {
            var body = "<p>This is a test email from <strong>ActiveManager</strong>.</p>" +
                       "<p>Your SMTP configuration is working correctly.</p>";
            await SendAsync(settings, recipients, "[ActiveManager] Test email", body, throwOnError: true);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static async Task SendAsync(
        EmailSettings settings,
        List<string> recipients,
        string subject,
        string htmlBody,
        bool throwOnError = false)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderAddress));

            foreach (var address in recipients)
            {
                message.To.Add(MailboxAddress.Parse(address));
            }

            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();

            var secureSocketOption = settings.UseSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(settings.SmtpServer, settings.SmtpPort, secureSocketOption);

            var password = settings.DecryptPassword();
            if (!string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(settings.Username, password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("EmailNotificationService", ex);
            if (throwOnError) throw;
        }
    }

    private static string BuildTerminationBody(TerminationRecord record, bool hasFailures)
    {
        var statusColor = hasFailures ? "#c0392b" : "#27ae60";
        var statusText = hasFailures ? "Completed with errors" : "Completed successfully";

        var stepRows = string.Join("", record.StepResults.Select(s =>
        {
            var icon = s.Success ? "✓" : "✗";
            var color = s.Success ? "#27ae60" : "#c0392b";
            var error = s.Success ? "" : $"<br/><small style='color:#888'>{HtmlEncode(s.ErrorMessage ?? "")}</small>";
            return $"<tr><td style='padding:6px 12px'>{HtmlEncode(s.StepName)}{error}</td>" +
                   $"<td style='padding:6px 12px;color:{color};font-weight:bold'>{icon} {(s.Success ? "Success" : "Failed")}</td>" +
                   $"<td style='padding:6px 12px;color:#888'>{s.ExecutedAt:HH:mm:ss}</td></tr>";
        }));

        var groups = record.GroupMemberships.Count > 0
            ? "<ul>" + string.Join("", record.GroupMemberships.Select(g => $"<li>{HtmlEncode(g.GroupName)}</li>")) + "</ul>"
            : "<p style='color:#888'>None</p>";

        return $@"
<html><body style='font-family:sans-serif;color:#333;max-width:700px;margin:0 auto'>
<h2 style='border-bottom:2px solid #ddd;padding-bottom:8px'>User Termination Report</h2>

<table style='border-collapse:collapse;width:100%;margin-bottom:20px'>
  <tr><td style='padding:4px 12px 4px 0;color:#888;width:160px'>User</td><td><strong>{HtmlEncode(record.DisplayName)}</strong> ({HtmlEncode(record.SamAccountName)})</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#888'>Performed by</td><td>{HtmlEncode(record.PerformedBy)}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#888'>Terminated at</td><td>{record.TerminatedAt:yyyy-MM-dd HH:mm:ss}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#888'>Status</td><td style='color:{statusColor};font-weight:bold'>{statusText}</td></tr>
</table>

<h3 style='margin-top:24px'>Actions performed</h3>
<table style='border-collapse:collapse;width:100%;border:1px solid #ddd'>
  <thead><tr style='background:#f5f5f5'>
    <th style='padding:8px 12px;text-align:left'>Step</th>
    <th style='padding:8px 12px;text-align:left'>Result</th>
    <th style='padding:8px 12px;text-align:left'>Time</th>
  </tr></thead>
  <tbody>{stepRows}</tbody>
</table>

<h3 style='margin-top:24px'>Groups removed</h3>
{groups}

<p style='margin-top:32px;font-size:12px;color:#aaa'>Sent by ActiveManager &mdash; {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
</body></html>";
    }

    private static string BuildRollbackBody(TerminationRecord record, bool wasSuccessful)
    {
        var statusColor = wasSuccessful ? "#27ae60" : "#c0392b";
        var statusText = wasSuccessful ? "Completed successfully" : "Completed with errors";

        return $@"
<html><body style='font-family:sans-serif;color:#333;max-width:700px;margin:0 auto'>
<h2 style='border-bottom:2px solid #ddd;padding-bottom:8px'>Termination Rollback Report</h2>

<p style='background:#fff8e1;border-left:4px solid #f39c12;padding:12px;border-radius:4px'>
  A previously terminated account has been <strong>rolled back</strong> and may have been re-enabled.
</p>

<table style='border-collapse:collapse;width:100%;margin-bottom:20px'>
  <tr><td style='padding:4px 12px 4px 0;color:#888;width:160px'>User</td><td><strong>{HtmlEncode(record.DisplayName)}</strong> ({HtmlEncode(record.SamAccountName)})</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#888'>Originally terminated</td><td>{record.TerminatedAt:yyyy-MM-dd HH:mm:ss} by {HtmlEncode(record.PerformedBy)}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#888'>Rolled back at</td><td>{record.RolledBackAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#888'>Rolled back by</td><td>{HtmlEncode(record.RolledBackBy ?? Environment.UserName)}</td></tr>
  <tr><td style='padding:4px 12px 4px 0;color:#888'>Status</td><td style='color:{statusColor};font-weight:bold'>{statusText}</td></tr>
</table>

<p style='margin-top:32px;font-size:12px;color:#aaa'>Sent by ActiveManager &mdash; {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
</body></html>";
    }

    private static string HtmlEncode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
