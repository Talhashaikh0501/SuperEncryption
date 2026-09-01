using Google.Cloud.Firestore;

namespace CyberChatDemo.Models
{
    [FirestoreData]
    public class AppUser
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = "";

        [FirestoreProperty]
        public string InstagramId { get; set; } = "";

        // DEMO PASSWORD ONLY.
        // Never use this pattern for real production passwords.
        [FirestoreProperty]
        public string DemoPassword { get; set; } = "";

        [FirestoreProperty]
        public string DisplayName { get; set; } = "";

        [FirestoreProperty]
        public string Role { get; set; } = "User";

        [FirestoreProperty]
        public bool IsOnline { get; set; }

        [FirestoreProperty]
        public Timestamp CreatedAt { get; set; }

        [FirestoreProperty]
        public Timestamp LastSeen { get; set; }
    }
}