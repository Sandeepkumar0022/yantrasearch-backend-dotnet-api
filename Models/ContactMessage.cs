using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Dashboards.Models
{
    public class ContactMessage
    {
        public int Id { get; set; }
        public string ClientId { get; set; } // new field
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string EmailStatus { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}