using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RAG2_Gemini.Models
{
    // Data models for FreshService API response
    public class FreshServiceGroupResponse
    {
        [JsonPropertyName("groups")]
        public List<FreshServiceGroup> Groups { get; set; } = new();
    }

    public class Ticket
    {
        [JsonPropertyName("ID")] // Maps JSON "TicketID" to C# TicketId
        public long ID { get; set; }

        [JsonPropertyName("Similarity_Score")] // Maps JSON "Similarity Score" to C# SimilarityScore
        public int SimilarityScore { get; set; }

        [JsonPropertyName("Reason")] // Maps JSON "Reason" to C# Reason
        public string Reason { get; set; }

        [JsonPropertyName("RequesterID")] // Maps JSON "Subject" to C# Subject
        public long UserID { get; set; }
    }

    public class FreshServiceGroup
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class FreshServiceTicketResponse
    {
        [JsonPropertyName("tickets")]
        public List<FreshServiceTicket> Tickets { get; set; } = new();
    }

    public class FreshServiceTicket
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("description_text")]
        public string DescriptionText { get; set; } = string.Empty;

        [JsonPropertyName("requester_id")]
        public long RequesterID { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("source")]
        public int Source { get; set; }

        [JsonPropertyName("group_id")]
        public long? GroupId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        public string GroupName { get; set; } = string.Empty; // You may need to map this from group_id

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? Updated { get; set; }
    }

    public class ConversationsResponse
    {
        [JsonPropertyName("conversations")]
        public List<Conversation> Conversations { get; set; }

        [JsonPropertyName("meta")]
        public Meta Meta { get; set; }
    }

    /// <summary>
    /// Represents a single conversation entry within a ticket.
    /// </summary>
    public class Conversation
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        [JsonPropertyName("to_emails")]
        public List<string> ToEmails { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("body_text")]
        public string BodyText { get; set; }

        [JsonPropertyName("ticket_id")]
        public int TicketId { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("incoming")]
        public bool Incoming { get; set; }

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("support_email")]
        public string SupportEmail { get; set; }

        [JsonPropertyName("source")]
        public int Source { get; set; }

        [JsonPropertyName("from_email")]
        public string FromEmail { get; set; }

        [JsonPropertyName("cc_emails")]
        public List<string> CcEmails { get; set; }

        // Note: bcc_emails can sometimes be null, so we handle that by not making it a required field.
        [JsonPropertyName("bcc_emails")]
        public List<string> BccEmails { get; set; }

        // Attachments are likely more complex objects, but are an empty array in the example.
        // For now, we can represent them as a list of objects.
        [JsonPropertyName("attachments")]
        public List<object> Attachments { get; set; }
    }

    /// <summary>
    /// Represents the metadata object that provides information about the result set.
    /// </summary>
    public class Meta
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }
    }
}
