using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Web;

namespace Dashboards.Services
{
    public static class ListingEnquiryMail
    {
        public static void Send(EnquiryDetails details)
        {
            if (details == null) return;
            var senderEmail = ConfigurationManager.AppSettings["sender_email"];
            var senderName = ConfigurationManager.AppSettings["sender_name"] ?? "Yantra Search Team";
            if (string.IsNullOrWhiteSpace(senderEmail))
                senderEmail = "yantrasearch.help@gmail.com";

            var inbox = ConfigurationManager.AppSettings["sender_bcc"];
            if (string.IsNullOrWhiteSpace(inbox))
                inbox = "yantrasearch@gmail.com";

            var item = string.IsNullOrWhiteSpace(details.ItemTitle) ? "Listing" : details.ItemTitle.Trim();
            var client = string.IsNullOrWhiteSpace(details.ClientName) ? "Client" : details.ClientName.Trim();
            using (var mail = new MailMessage())
            {
                mail.From = new MailAddress(senderEmail, senderName);
                mail.To.Add(new MailAddress(inbox.Trim(), "YantraSearch"));
                if (!string.IsNullOrWhiteSpace(details.ClientEmail))
                    mail.ReplyToList.Add(new MailAddress(details.ClientEmail.Trim(), client));
                mail.Subject = "Listing enquiry: " + item;
                mail.Body = Body(details, item, client);
                mail.IsBodyHtml = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var smtp = new SmtpClient())
                    smtp.Send(mail);
            }
        }

        static string Body(EnquiryDetails details, string item, string client)
        {
            var message = HttpUtility.HtmlEncode(details.Message ?? "").Replace("\n", "<br/>");
            return "<p><strong>Mail type:</strong> Listing enquiry</p>"
                + "<p>A client sent an enquiry about a listing. This is not a posted requirement.</p>"
                + "<h3>Item</h3>"
                + Line("Item type", details.ItemType)
                + Line("Item", item)
                + Line("Listing id", details.ListingId)
                + "<h3>Vendor</h3>"
                + Line("Vendor", details.VendorName)
                + Line("Vendor type", details.VendorType)
                + Line("Vendor email", details.VendorEmail)
                + Line("Vendor phone", details.VendorPhone)
                + "<h3>Client</h3>"
                + Line("Client", client)
                + Line("Company", details.ClientCompany)
                + Line("Email", details.ClientEmail)
                + Line("Phone", details.ClientPhone)
                + Line("Address", details.ClientAddress)
                + "<h3>Enquiry</h3>"
                + "<p>" + message + "</p>";
        }

        static string Line(string label, string value)
        {
            return "<p><strong>" + HttpUtility.HtmlEncode(label) + ":</strong> "
                + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "—" : value.Trim())
                + "</p>";
        }

        public class EnquiryDetails
        {
            public string ItemType { get; set; }
            public string ItemTitle { get; set; }
            public string ListingId { get; set; }
            public string VendorName { get; set; }
            public string VendorType { get; set; }
            public string VendorEmail { get; set; }
            public string VendorPhone { get; set; }
            public string ClientName { get; set; }
            public string ClientCompany { get; set; }
            public string ClientEmail { get; set; }
            public string ClientPhone { get; set; }
            public string ClientAddress { get; set; }
            public string Message { get; set; }
        }
    }
}
