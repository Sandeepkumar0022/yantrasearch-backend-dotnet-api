using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Web;

namespace Dashboards.Services
{
    public static class ContactMail
    {
        public static void Send(string name, string email, string phone, string message, string subject)
        {
            var senderEmail = ConfigurationManager.AppSettings["sender_email"];
            var senderName = ConfigurationManager.AppSettings["sender_name"] ?? "Yantra Search Team";
            if (string.IsNullOrWhiteSpace(senderEmail))
                senderEmail = "yantrasearch.help@gmail.com";

            var inbox = ConfigurationManager.AppSettings["sender_bcc"];
            if (string.IsNullOrWhiteSpace(inbox))
                inbox = "yantrasearch@gmail.com";

            var visitor = string.IsNullOrWhiteSpace(name) ? "Website visitor" : name.Trim();
            using (var mail = new MailMessage())
            {
                mail.From = new MailAddress(senderEmail, senderName);
                mail.To.Add(new MailAddress(inbox.Trim(), "YantraSearch"));
                if (!string.IsNullOrWhiteSpace(email))
                    mail.ReplyToList.Add(new MailAddress(email.Trim(), visitor));
                mail.Subject = string.IsNullOrWhiteSpace(subject) ? "Website contact form" : subject.Trim();
                mail.Body = Body(visitor, email, phone, message);
                mail.IsBodyHtml = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var smtp = new SmtpClient())
                    smtp.Send(mail);
            }
        }

        static string Body(string name, string email, string phone, string message)
        {
            var text = HttpUtility.HtmlEncode(message ?? "").Replace("\n", "<br/>");
            return "<p><strong>Name:</strong> " + HttpUtility.HtmlEncode(name) + "</p>"
                + "<p><strong>Email:</strong> " + HttpUtility.HtmlEncode(email ?? "") + "</p>"
                + "<p><strong>Phone:</strong> " + HttpUtility.HtmlEncode(phone ?? "") + "</p>"
                + "<p><strong>Message:</strong></p><p>" + text + "</p>"
                + "<hr/><p>This message was sent via the YantraSearch website.</p>";
        }
    }
}
