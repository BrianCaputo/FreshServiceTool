using System.Text.Json.Serialization;

namespace RAG2_Gemini.Models
{
    public class FreshServiceSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
    }

    public class GeminiSettings
    {
        public string Gemini { get; set; } = string.Empty;
    }

    public class FreshServiceArticle
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        // Mapped to "description_text" for the plain text content. 
        // The "description" field contains HTML.
        [JsonPropertyName("description_text")]
        public string Description { get; set; }

        [JsonPropertyName("user_id")]
        public string AuthorID { get; set; }

        [JsonPropertyName("id")]
        public long ID { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("review_date")]
        public DateTime? ReviewDate { get; set; } // Changed to nullable DateTime to handle null values

        [JsonPropertyName("folder_id")]
        public long FolderID { get; set; }

        [JsonPropertyName("category_id")]
        public long CategoryID { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("views")]
        public int Views { get; set; }

        [JsonPropertyName("article_type")]
        public int ArticleType { get; set; }

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; }
    }
    
    public class FreshServiceGroupResponse
    {
        [JsonPropertyName("groups")]
        public List<FreshServiceGroup> Groups { get; set; } = new();
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
        public long RequesterId { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("source")]
        public int Source { get; set; }

        [JsonPropertyName("group_id")]
        public long? GroupId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        public string GroupName { get; set; } = string.Empty;
    }

    public class ConversationsResponse
    {
        [JsonPropertyName("conversations")]
        public List<Conversation> Conversations { get; set; } = new();

        [JsonPropertyName("meta")]
        public Meta Meta { get; set; } = new();
    }

    public class Conversation
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        [JsonPropertyName("to_emails")]
        public List<string> ToEmails { get; set; } = new();

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("body_text")]
        public string BodyText { get; set; } = string.Empty;

        [JsonPropertyName("ticket_id")]
        public long TicketId { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("incoming")]
        public bool Incoming { get; set; }

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("support_email")]
        public string SupportEmail { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public int Source { get; set; }

        [JsonPropertyName("from_email")]
        public string FromEmail { get; set; } = string.Empty;

        [JsonPropertyName("cc_emails")]
        public List<string> CcEmails { get; set; } = new();

        [JsonPropertyName("bcc_emails")]
        public List<string> BccEmails { get; set; } = new();

        [JsonPropertyName("attachments")]
        public List<object> Attachments { get; set; } = new();
    }

    public class Category
    {
        /// <summary>
        /// Gets or sets the unique identifier.
        /// Mapped from the "id" JSON property.
        /// </summary>
        [JsonPropertyName("id")]
        public long ID { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// Mapped from the "created_at" JSON property.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the last update timestamp.
        /// Mapped from the "updated_at" JSON property.
        /// </summary>
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// Mapped from the "name" JSON property.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// Mapped from the "description" JSON property.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the default category.
        /// Mapped from the "default_category" JSON property.
        /// </summary>
        [JsonPropertyName("default_category")]
        public bool DefaultCategory { get; set; }

        /// <summary>
        /// Gets or sets the position or order index.
        /// Mapped from the "position" JSON property.
        /// </summary>
        [JsonPropertyName("position")]
        public int Position { get; set; }
    }

    public class CategoryGroup
    {
        [JsonPropertyName("categories")]
        public List<Category> Categories { get; set; } = new List<Category>();
    }

    public class Meta
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }
    }

    public class RelevantTicket
    {
        [JsonPropertyName("ID")]
        public long Id { get; set; }

        [JsonPropertyName("RequesterID")]
        public long RequesterId { get; set; }

        [JsonPropertyName("Similarity_Score")]
        public int SimilarityScore { get; set; }

        [JsonPropertyName("Reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public class GroupSelectionResponse
    {
        [JsonPropertyName("GroupID")]
        public long GroupId { get; set; }

        [JsonPropertyName("Reason")]
        public string Reason { get; set; } = string.Empty;
    }
}