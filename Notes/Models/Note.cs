using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notes.Models
{
    public class Note
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Filename { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public List<Attachment> Attachments { get; set; } = new();
    }
}
