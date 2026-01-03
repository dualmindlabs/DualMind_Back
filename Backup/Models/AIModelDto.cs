using System;

namespace DualMind_Back.Models
{
    public class AIModelDto
    {
        public Guid ModelId { get; set; }
        public string ModelName { get; set; }
        public string DisplayName { get; set; }
        public string ProviderName { get; set; }
        public string ApiUrl { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
