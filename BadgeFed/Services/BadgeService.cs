using System.Security.Cryptography;
using System.Text;
using ActivityPubDotNet.Core;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class BadgeService
{
    private readonly LocalDbService DbService;

    public BadgeService(LocalDbService dbService)
    {
        DbService = dbService;
    }

    public static BadgeRecord GetBadge()
    {
        var badge = new BadgeRecord();
        return badge;
    }

    static string CreateHashSha256(string input)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hashBytes);
        }
    }

    public string GetFingerprint(ActivityPubNote note, BadgeRecord record)
    {
        var serializedNote = System.Text.Json.JsonSerializer.Serialize(note);

        var badge = DbService.GetBadgeDefinitionById(record.Badge.Id);
        var actor = DbService.GetActorById(badge.IssuedBy);

        using (RSA rsa = RSA.Create())
        {
            rsa.ImportFromPem(actor.PrivateKeyPemClean);

            string digest = $"SHA-256={CreateHashSha256(serializedNote)}";

            byte[] signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(digest), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Convert the signature to a SHA-256 hash string
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(signatureBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        return string.Empty;
    }

    public static string GetMention(string name, string link)
    {
        if (name.StartsWith("@"))
            name = name.Substring(1);

        return $"<a href=\"{link}\" class=\"u-url mention\">@<span>{name}</span></a>";
    }

    public static ActivityPubNote GetNoteFromBadgeRecord(BadgeRecord record)
    {
        var note = NotesService.GetBadgeNote(record);

        note.BadgeMetadata = record;

        return note;
    }

    public static BadgeRecord GetBadgeRecordFromNote(ActivityPubNote note)
    {
        throw new NotImplementedException();
    }

    public BadgeRecord GetGrantBadgeRecord(Badge badge, Recipient recipient)
    {
        var acceptKey = Guid.NewGuid().ToString();

        var actor = DbService.GetActorById(badge.IssuedBy);

        var badgeRecord = new BadgeRecord()
        {
            Title = badge.Title,
            Description = badge.Description,
            IssuedBy = actor.Uri!.ToString(),
            IssuedOn = DateTime.UtcNow,
            Image = badge.Image,
            ImageAltText = badge.ImageAltText,
            IssuedToName = recipient?.Name ?? string.Empty,
            IssuedToSubjectUri = recipient?.ProfileUri ?? string.Empty,
            IssuedToEmail = recipient?.Email ?? string.Empty,
            EarningCriteria = badge.EarningCriteria,
            AcceptedOn = null,
            Badge = badge,
            AcceptKey = acceptKey
        };

        return badgeRecord;
    }
}