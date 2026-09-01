namespace CyberChatDemo.Models
{
    public class ChatRoomViewModel
    {
        public string ConversationId { get; set; } = "";

        public string CurrentUserId { get; set; } = "";

        public string OtherUserId { get; set; } = "";

        public string OtherInstagramId { get; set; } = "";

        public string OtherDisplayName { get; set; } = "";

        public bool OtherIsOnline { get; set; }

        public DateTime? OtherLastSeenAt { get; set; }

        public string OtherPresenceText { get; set; } = "Offline";

        public List<ChatMessageViewModel> Messages { get; set; } = new();
    }


    public class ChatMessageViewModel
    {
        public string Id { get; set; } = "";

        public string SenderId { get; set; } = "";

        public string Text { get; set; } = "";

        public DateTime SentAt { get; set; }

        public bool IsMine { get; set; }

        public bool Seen { get; set; }
    }
}