using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace CyberChatDemo.Controllers
{
    public class FirebaseTestController : Controller
    {
        private readonly FirestoreDb _firestoreDb;

        public FirebaseTestController(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        public async Task<IActionResult> Index()
        {
            var testData = new Dictionary<string, object>
            {
                { "message", "Firebase connected successfully!" },
                { "createdAt", Timestamp.GetCurrentTimestamp() }
            };

            await _firestoreDb
                .Collection("connectionTests")
                .AddAsync(testData);

            return Content("SUCCESS: .NET is connected to Firebase Firestore!");
        }
    }
}