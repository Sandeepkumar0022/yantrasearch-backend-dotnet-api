using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Web;

namespace Dashboards.Services
{
    public static class RequirementMail
    {
        public static void Send(
            string clientName,
            string email,
            string phone,
            string address,
            string title,
            string location,
            string budget,
            string description,
            IList<RequirementFile> attachments)
        {
            var senderEmail = ConfigurationManager.AppSettings["sender_email"];
            var senderName = ConfigurationManager.AppSettings["sender_name"] ?? "Yantra Search Team";
            if (string.IsNullOrWhiteSpace(senderEmail))
                senderEmail = "yantrasearch.help@gmail.com";

            var inbox = ConfigurationManager.AppSettings["sender_bcc"];
            if (string.IsNullOrWhiteSpace(inbox))
                inbox = "yantrasearch@gmail.com";

            var name = string.IsNullOrWhiteSpace(clientName) ? "Client" : clientName.Trim();
            var subjectTitle = string.IsNullOrWhiteSpace(title) ? "New requirement" : title.Trim();
            using (var mail = new MailMessage())
            {
                mail.From = new MailAddress(senderEmail, senderName);
                mail.To.Add(new MailAddress(inbox.Trim(), "YantraSearch"));
                if (!string.IsNullOrWhiteSpace(email))
                    mail.ReplyToList.Add(new MailAddress(email.Trim(), name));
                mail.Subject = "New client requirement: " + subjectTitle;
                mail.Body = Body(name, email, phone, address, subjectTitle, location, budget, description, attachments);
                mail.IsBodyHtml = true;
                if (attachments != null)
                {
                    foreach (var file in attachments)
                    {
                        if (file == null || file.Data == null || file.Data.Length == 0) continue;
                        var stream = new MemoryStream(file.Data);
                        var filename = string.IsNullOrWhiteSpace(file.Name) ? "attachment" : file.Name.Trim();
                        var type = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType.Trim();
                        mail.Attachments.Add(new Attachment(stream, filename, type));
                    }
                }
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var smtp = new SmtpClient())
                    smtp.Send(mail);
            }
        }

        static string Body(
            string name,
            string email,
            string phone,
            string address,
            string title,
            string location,
            string budget,
            string description,
            IList<RequirementFile> attachments)
        {
            var names = new List<string>();
            if (attachments != null)
            {
                foreach (var file in attachments)
                    if (file != null && !string.IsNullOrWhiteSpace(file.Name)) names.Add(file.Name.Trim());
            }
            var files = names.Count == 0 ? "None" : string.Join(", ", names);
            var text = HttpUtility.HtmlEncode(description ?? "").Replace("\n", "<br/>");
            return "<p>A client posted a new requirement on YantraSearch.</p>"
                + Line("Client", name)
                + Line("Email", email)
                + Line("Phone", phone)
                + Line("Address", address)
                + Line("Title", title)
                + Line("Location", location)
                + Line("Budget", budget)
                + "<p><strong>Description:</strong></p><p>" + text + "</p>"
                + Line("Attachments", files);
        }

        public class RequirementFile
        {
            public string Name { get; set; }
            public string ContentType { get; set; }
            public byte[] Data { get; set; }
        }

        static string Line(string label, string value)
        {
            return "<p><strong>" + HttpUtility.HtmlEncode(label) + ":</strong> "
                + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "—" : value.Trim())
                + "</p>";
        }
    }
}
