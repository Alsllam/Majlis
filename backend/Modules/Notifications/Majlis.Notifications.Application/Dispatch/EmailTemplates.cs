using System.Net;
using Majlis.Notifications.Domain.Constants;

namespace Majlis.Notifications.Application.Dispatch;

/// <summary>
/// Email texts per recipient language (FR-NTF-002). No content by default (SRS §4.8): a short notice and a link;
/// the summary of an approval is the only user text and it is HTML-encoded.
/// </summary>
public static class EmailTemplates
{
    public static (string Subject, string Text) For(string type, string language, IReadOnlyDictionary<string, string> p)
    {
        var ar = language != "en";
        var name = p.GetValueOrDefault("name", "");
        var summary = p.GetValueOrDefault("summary", p.GetValueOrDefault("title", ""));
        return type switch
        {
            NotificationTypes.ApprovalRequested => ar
                ? ("مجلس: طلب موافقة جديد", $"اقترح الوكيل إجراءً بطلب من {name}: {summary}. افتح الغرفة لمراجعته والموافقة عليه أو رفضه.")
                : ("Majlis: new approval request", $"The agent proposed an action requested by {name}: {summary}. Open the room to approve or reject it."),
            NotificationTypes.ApprovalDecided => ar
                ? ("مجلس: تم البت في طلبك", $"{(p.GetValueOrDefault("decision") == "Approved" ? "وافق" : "رفض")} {name} على الإجراء: {summary}.")
                : ("Majlis: your request was decided", $"{name} {(p.GetValueOrDefault("decision") == "Approved" ? "approved" : "rejected")} the action: {summary}."),
            NotificationTypes.ApprovalExecuted => ar
                ? ("مجلس: نتيجة الإجراء", p.GetValueOrDefault("succeeded") == "true" ? $"نُفِّذ الإجراء: {summary}." : $"تعذّر تنفيذ الإجراء: {summary}.")
                : ("Majlis: action outcome", p.GetValueOrDefault("succeeded") == "true" ? $"The action ran: {summary}." : $"The action could not run: {summary}."),
            NotificationTypes.TaskAssigned => ar
                ? ("مجلس: مهمة جديدة مسندة إليك", $"أسند إليك {name} المهمة: {summary}.")
                : ("Majlis: a task was assigned to you", $"{name} assigned you the task: {summary}."),
            NotificationTypes.HandOffOffered => ar
                ? ("مجلس: عرض تسليم التحكم", $"يعرض {name} عليك قيادة الجلسة.")
                : ("Majlis: hand-off offered", $"{name} offers you control of the session."),
            _ => ar ? ("مجلس: إشعار جديد", "لديك إشعار جديد.") : ("Majlis: new notification", "You have a new notification."),
        };
    }

    public static string Html(string text, string link, string language)
    {
        var dir = language == "en" ? "ltr" : "rtl";
        var open = language == "en" ? "Open in Majlis" : "فتح في مجلس";
        return $"<div dir=\"{dir}\" style=\"font-family:Segoe UI,Tahoma,sans-serif;font-size:15px;line-height:1.6\"><p>{WebUtility.HtmlEncode(text)}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">{open}</a></p></div>";
    }
}
