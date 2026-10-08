using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace Dashboards.Services
{
    public static class RegistrationMail
    {
        public static void Send(string email, string name, string phone, string role)
        {
            var senderEmail = ConfigurationManager.AppSettings["sender_email"];
            var senderName = ConfigurationManager.AppSettings["sender_name"] ?? "Yantra Search Team";
            if (string.IsNullOrWhiteSpace(senderEmail))
                senderEmail = "yantrasearch.help@gmail.com";

            var subject = "Welcome to YantraSearch – Registration Successful!";
            var body = Body(name, email, phone, role);
            using (var mail = new MailMessage())
            {
                mail.From = new MailAddress(senderEmail, senderName);
                mail.To.Add(new MailAddress(email, string.IsNullOrWhiteSpace(name) ? email : name));
                AddOnce(mail.Bcc, ConfigurationManager.AppSettings["sender_bcc"]);
                AddOnce(mail.Bcc, ConfigurationManager.AppSettings["debug_cc"]);
                mail.Subject = subject;
                mail.Body = body;
                mail.IsBodyHtml = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var smtp = new SmtpClient())
                    smtp.Send(mail);
            }
        }

        static void AddOnce(MailAddressCollection list, string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            address = address.Trim();
            foreach (var existing in list)
            {
                if (string.Equals(existing.Address, address, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            list.Add(new MailAddress(address));
        }

        static string Body(string name, string email, string phone, string role)
        {
            var accountDetails = "<br/><b>Your account details are as follows:</b><br/>User ID: " + email + "<br/>Contact Number: " + phone + "<br/>";
            var accountDetailsHindi = "<br/><b>आपके खाते की जानकारी इस प्रकार है:</b><br/>यूजर आईडी: " + email + "<br/>संपर्क नंबर: " + phone + "<br/>";
            string english;
            string hindi;
            switch (role)
            {
                case "Equipment Supplier":
                    english = "<p>Dear " + name + ",</p><p>Thank you for joining <a href='https://www.yantrasearch.com'>YantraSearch</a> as an equipment supplier.</p><p>You are now part of a platform where construction companies and contractors look for reliable equipment owners like you.</p><p>With your account, you can:</p><ul><li><b>List your equipment</b> with availability and rates</li><li><b>Connect with companies and contractors</b> who need your machines</li><li><b>Track inquiries and bookings</b> in one place</li></ul>";
                    hindi = "<p>प्रिय " + name + ",</p><p><a href='https://www.yantrasearch.com'>YantraSearch</a> से उपकरण आपूर्तिकर्ता के रूप में जुड़ने के लिए धन्यवाद।</p><p>अब आप उस मंच का हिस्सा हैं जहाँ निर्माण कंपनियाँ और ठेकेदार आप जैसे भरोसेमंद उपकरण मालिकों को ढूँढते हैं।</p><ul><li>अपने <b>उपकरण को उपलब्धता और दरों के साथ सूची में</b> डाल सकते हैं</li><li>उन <b>कंपनियों और ठेकेदारों से जुड़</b> सकते हैं जिन्हें आपकी मशीनों की ज़रूरत है</li><li>सभी <b>पूछताछ और बुकिंग एक ही जगह देख</b> सकते हैं</li></ul>";
                    break;
                case "Project Contractor":
                    english = "<p>Dear " + name + ",</p><p>Welcome to <a href='https://www.yantrasearch.com'>YantraSearch</a> as a project contractor.</p><p>You are now connected to a network of construction companies, projects, and equipment owners.</p><ul><li><b>Find projects</b> that need your skills and services</li><li><b>Connect with companies</b> looking for reliable contractors</li><li><b>Collaborate with equipment owners</b> for tools and machinery you need</li></ul>";
                    hindi = "<p>प्रिय " + name + ",</p><p><a href='https://www.yantrasearch.com'>YantraSearch</a> पर परियोजना ठेकेदार के रूप में आपका स्वागत है।</p><ul><li>ऐसी <b>परियोजनाएँ ढूँढें</b> जिन्हें आपके कौशल और सेवाओं की ज़रूरत है</li><li>ऐसी <b>कंपनियों से जुड़ें</b> जो भरोसेमंद ठेकेदार ढूँढ रही हैं</li><li>ज़रूरी उपकरण और मशीनरी के लिए <b>उपकरण मालिकों के साथ सहयोग</b> करें</li></ul>";
                    break;
                case "Construction Employee":
                    english = "<p>Dear " + name + ",</p><p>Thanks for signing up with <a href='https://www.yantrasearch.com'>YantraSearch</a> as a construction employee.</p><p>With your new account, you can:</p><ul><li><b>Search job opportunities</b> that match your trade and experience</li><li><b>Create a strong profile</b> so companies and contractors can find you</li><li><b>Stay updated</b> on new openings in your area</li></ul>";
                    hindi = "<p>प्रिय " + name + ",</p><p><a href='https://www.yantrasearch.com'>YantraSearch</a> पर निर्माण कर्मचारी के रूप में साइन अप करने के लिए धन्यवाद।</p><ul><li>अपने काम और अनुभव के अनुसार <b>नौकरी के अवसरों की खोज</b> कर सकते हैं</li><li>मज़बूत विवरण बना सकते हैं ताकि <b>कंपनियाँ और ठेकेदार आपको आसानी से ढूँढ</b> सकें</li><li>अपने इलाके में आने वाली <b>नई रिक्तियों से अपडेट</b> रह सकते हैं</li></ul>";
                    break;
                default:
                    english = "<p>Dear " + name + ",</p><p>Thank you for registering on <a href='https://www.yantrasearch.com'>YantraSearch</a> as " + role + ".</p><p>Your account has been created successfully. You can now log in and start exploring projects, equipment, companies, and opportunities.</p>";
                    hindi = "<p>प्रिय " + name + ",</p><p><a href='https://www.yantrasearch.com'>YantraSearch</a> पर " + role + " के रूप में रजिस्टर करने के लिए धन्यवाद।</p><p>आपका खाता सफलतापूर्वक बना दिया गया है। अब आप लॉगिन करके परियोजनाएँ, उपकरण, कंपनियों और अवसरों को देख सकते हैं।</p>";
                    break;
            }
            return english + accountDetails + "<p>Best regards,<br/>YantraSearch Team</p><br/><hr style=\"border:0;border-top:1px solid #ccc;margin:16px 0;\"/><br/>" + hindi + accountDetailsHindi + "<p>शुभकामनाएँ,<br/>YantraSearch टीम</p>";
        }
    }
}
