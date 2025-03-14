using Microsoft.Extensions.Logging;
using System;
using System.Data.SQLite;
using System.Text.Json;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class LocalDbService
{
    private readonly string connectionString;

    private readonly string dbPath;

    public static LocalDbService GetInstance(string username)
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        string filePath = Path.Combine(currentDirectory, $"{username.ToLowerInvariant()}.db");
        return new LocalDbService(filePath);
    }

    public LocalDbService(string dbPath)
    {
        this.dbPath = dbPath;
        this.connectionString = $"Data Source={dbPath};Version=3;";

        CreateDb();
    }

    public SQLiteConnection GetConnection()
    {
        return new SQLiteConnection(connectionString);
    }

    private void CreateDb()
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Actor (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Summary TEXT,
                AvatarPath TEXT,
                InformationUri TEXT,
                Domain TEXT,
                CreatedAt DATETIME NOT NULL,
                UpdatedAt DATETIME NOT NULL,
                PublicKeyPem TEXT
            );
        ";
        command.ExecuteNonQuery();
    }

    public void UpsertActor(Actor actor)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Actor (Name, Summary, AvatarPath, InformationUri, Uri, Domain, PublicKeyPem, PrivateKeyPem, Username)
            VALUES (@Name, @Summary, @AvatarPath, @InformationUri, @Uri, @Domain, @PublicKeyPem, @PrivateKeyPem, @Username);
        ";

        command.Parameters.AddWithValue("@Id", actor.Id);
        command.Parameters.AddWithValue("@Name", actor.FullName);
        command.Parameters.AddWithValue("@Summary", actor.Summary);
        command.Parameters.AddWithValue("@AvatarPath", actor.AvatarPath);
        command.Parameters.AddWithValue("@InformationUri", actor.InformationUri);
        command.Parameters.AddWithValue("@Domain", actor.Domain);
        command.Parameters.AddWithValue("@PublicKeyPem", actor.PublicKeyPem);
        command.Parameters.AddWithValue("@PrivateKeyPem", actor.PrivateKeyPem);
        command.Parameters.AddWithValue("@Uri", actor.Uri);
        command.Parameters.AddWithValue("@Username", actor.Username);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void DeleteActor(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Actor WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
    }

    public List<Actor> GetActors()
    {
        var actors = new List<Actor>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Actor";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            actors.Add(new Actor
            {
                Id = int.Parse(reader["Id"].ToString()!),
                FullName = reader["Name"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : reader["InformationUri"].ToString()!,
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString(),
                Username = reader["Username"] == DBNull.Value ? null : reader["Username"].ToString()
            });
        }

        return actors;
    }

    public Actor GetActorById(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Actor WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new Actor
            {
                Id = int.Parse(reader["Id"].ToString()!),
                FullName = reader["Name"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : reader["InformationUri"].ToString()!,
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString(),
                Username = reader["Username"] == DBNull.Value ? null : reader["Username"].ToString()
            };
        }

        return null;
    }

    public Actor GetActorByFilter(string filter)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = $"SELECT Id, Name, Summary, AvatarPath, InformationUri, Domain, PublicKeyPem, Username, PrivateKeyPem FROM Actor WHERE {filter}";
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new Actor
            {
                Id = int.Parse(reader["Id"].ToString()!),
                FullName = reader["Name"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : reader["InformationUri"].ToString()!,
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString(),
                Username = reader["Username"] == DBNull.Value ? null : reader["Username"].ToString()
            };
        }

        return null;
    }

    public void UpsertFollower(Follower follower)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Follower (FollowerUri, Domain, ActorId)
            VALUES (@FollowerUri, @Domain, @ActorId)
            ON CONFLICT(FollowerUri) DO UPDATE SET
                Domain = excluded.Domain,
                ActorId = excluded.ActorId,
                CreatedAt = excluded.CreatedAt;
        ";

        command.Parameters.AddWithValue("@FollowerUri", follower.FollowerUri);
        command.Parameters.AddWithValue("@Domain", follower.Domain);
        command.Parameters.AddWithValue("@ActorId", follower.Parent.Id);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public List<Follower> GetFollowersByActorId(long actorId)
    {
        var followers = new List<Follower>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Follower WHERE ActorId = @ActorId";
        command.Parameters.AddWithValue("@ActorId", actorId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            followers.Add(new Follower
            {
                FollowerUri = reader.GetString(0),
                Domain = reader.GetString(1),
                CreatedAt = reader.GetDateTime(2),
                Parent = new Actor() { Id = reader.GetInt64(3) }
            });
        }

        return followers;
    }

    public void DeleteFollower(string followerUri)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Follower WHERE FollowerUri = @FollowerUri";
        command.Parameters.AddWithValue("@FollowerUri", followerUri);

        command.ExecuteNonQuery();
    }

    public void UpsertBadgeDefinition(Badge badge)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        if (badge.Id == 0)
        {
            command.CommandText = @"
                INSERT INTO Badge (Title, Description, IssuedBy, Image, EarningCriteria, CreatedAt, UpdatedAt, BadgeType)
                VALUES (@Title, @Description, @IssuedBy, @Image, @EarningCriteria, datetime('now'), datetime('now'), @BadgeType);
                SELECT last_insert_rowid();
            ";
        }
        else
        {
            command.CommandText = @"
                UPDATE Badge SET 
                    Title = @Title, 
                    Description = @Description, 
                    IssuedBy = @IssuedBy, 
                    Image = @Image,
                    EarningCriteria = @EarningCriteria, 
                    UpdatedAt = datetime('now'),
                    BadgeType = @BadgeType
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", badge.Id);
        }

         Console.WriteLine($"Saving badge: {badge.Image}");

        command.Parameters.AddWithValue("@Title", badge.Title);
        command.Parameters.AddWithValue("@Description", badge.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IssuedBy", badge.IssuedBy);
        command.Parameters.AddWithValue("@Image", badge.Image ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@EarningCriteria", badge.EarningCriteria ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@BadgeType", badge.BadgeType);

        if (badge.Id == 0)
        {
            badge.Id = Convert.ToInt32(command.ExecuteScalar());
        }
        else
        {
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<Badge> GetAllBadgeDefinitions()
    {
        var badges = new List<Badge>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Badge";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            badges.Add(new Badge
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString()!,
                IssuedBy = reader.GetInt32(3),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString()!,
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString()!,
                BadgeType = reader["BadgeType"].ToString()
            });
        }

        return badges;
    }

    public Recipient GetRecipientByIssuedTo(string issuedTo)
    {
        var issuedToType = Recipient.GetAssignedToType(issuedTo);

        string filter = "FALSE";

        var recipient = new Recipient();

        switch(issuedToType)
        {
            case "email":
                recipient = new Recipient { Email = issuedTo };
                filter = "Email = @IssuedTo";
                break;
            case "fediverse":
                recipient = new Recipient { FediverseHandle = issuedTo };
                filter = "FediverseHandle = @IssuedTo";
                break;
            case "profileuri":
                recipient = new Recipient { ProfileUri = issuedTo };
                filter = "ProfileUri = @IssuedTo";
                break;
            default:
                recipient = new Recipient { FullName = issuedTo };
                return recipient;
                break;
        }

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM Recipient WHERE {filter}";

        command.Parameters.AddWithValue("@IssuedTo", issuedTo);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            recipient.Id = reader.GetInt64(0);
            recipient.FullName = reader["FullName"] == DBNull.Value ? null : reader["FullName"].ToString();
            recipient.Email = reader["Email"] == DBNull.Value ? null : reader["Email"].ToString();
            recipient.FediverseHandle = reader["FediverseHandle"] == DBNull.Value ? null : reader["FediverseHandle"].ToString();
            recipient.ProfileUri = reader["ProfileUri"] == DBNull.Value ? null : reader["ProfileUri"].ToString();
        }

        return recipient;
    }

    public Badge UpsertRecipient(Recipient recipient)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Recipient (FullName, Email, FediverseHandle, ProfileUri)
            VALUES (@FullName, @Email, @FediverseHandle, @ProfileUri);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@FullName", recipient.FullName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Email", recipient.Email ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FediverseHandle", recipient.FediverseHandle ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ProfileUri", recipient.ProfileUri ?? (object)DBNull.Value);

        recipient.Id = Convert.ToInt64(command.ExecuteScalar());

        transaction.Commit();

        return new Badge { Id = recipient.Id };
    }

    public Badge GetBadgeDefinitionById(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Badge WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new Badge
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                IssuedBy = reader.GetInt32(3),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                BadgeType = reader["BadgeType"].ToString()
            };
        }

        return null;
    }

    public void DeleteBadgeDefinition(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Badge WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
    }

    public void AcceptBadgeRecord(BadgeRecord badgeRecord)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE BadgeRecord SET 
                AcceptedOn = @AcceptedOn,
                AcceptKey = null,
                IssuedTo = @IssuedTo
            WHERE Id = @Id;
        ";

        command.Parameters.AddWithValue("@Id", badgeRecord.Id);
        command.Parameters.AddWithValue("@AcceptedOn", DateTime.UtcNow);
        command.Parameters.AddWithValue("@IssuedTo", badgeRecord.IssuedTo);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void CreateBadgeRecord(BadgeRecord record)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO BadgeRecord (
                Title, IssuedBy, Description, Image, EarningCriteria, 
                IssuedUsing, IssuedOn, IssuedTo,
                AcceptKey, BadgeId
            )
            VALUES (
                @Title, @IssuedBy, @Description, @Image, @EarningCriteria,
                @IssuedUsing, @IssuedOn, @IssuedTo,
                @AcceptKey, @BadgeId
            );
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@Title", record.Title);
        command.Parameters.AddWithValue("@IssuedBy", record.IssuedBy);
        command.Parameters.AddWithValue("@Description", record.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Image", record.Image ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@EarningCriteria", record.EarningCriteria ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IssuedUsing", record.IssuedUsing ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IssuedOn", record.IssuedOn);
        command.Parameters.AddWithValue("@IssuedTo", record.IssuedTo);
        command.Parameters.AddWithValue("@AcceptKey", record.AcceptKey);
        command.Parameters.AddWithValue("@BadgeId", record.Badge!.Id!);

        record.Id = Convert.ToInt64(command.ExecuteScalar());

        transaction.Commit();
    }

    public BadgeRecord? GetBadgeToAccept(long id, string key)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM BadgeRecord WHERE Id = @Id AND AcceptKey = @AcceptKey";
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@AcceptKey", key);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            return new BadgeRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                IssuedBy = reader.GetString(reader.GetOrdinal("IssuedBy")),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                IssuedUsing = reader["IssuedUsing"] == DBNull.Value ? null : reader["IssuedUsing"].ToString(),
                IssuedOn = reader.GetDateTime(reader.GetOrdinal("IssuedOn")),
                IssuedTo = reader.GetString(reader.GetOrdinal("IssuedTo")),
                AcceptedOn = reader["AcceptedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("AcceptedOn")),
                FingerPrint = reader["FingerPrint"] == DBNull.Value ? null : reader["FingerPrint"].ToString(),
                AcceptKey = reader["AcceptKey"] == DBNull.Value ? null : reader["AcceptKey"].ToString(),
                Badge = new Badge { Id = reader.GetInt64(reader.GetOrdinal("BadgeId")) }
            };
        }

        return null;
    }

    public List<BadgeRecord> GetBadgeRecords(string issuedTo = null, long? badgeId = null)
    {
        var records = new List<BadgeRecord>();
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        var whereClause = new List<string>();

        if (!string.IsNullOrEmpty(issuedTo))
        {
            whereClause.Add("IssuedTo = @IssuedTo");
            command.Parameters.AddWithValue("@IssuedTo", issuedTo);
        }
        
        if (badgeId.HasValue)
        {
            whereClause.Add("BadgeId = @BadgeId");
            command.Parameters.AddWithValue("@BadgeId", badgeId.Value);
        }

        command.CommandText = "SELECT * FROM BadgeRecord" + 
            (whereClause.Count > 0 ? " WHERE " + string.Join(" AND ", whereClause) : "");

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            records.Add(new BadgeRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                IssuedBy = reader.GetString(reader.GetOrdinal("IssuedBy")),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                IssuedUsing = reader["IssuedUsing"] == DBNull.Value ? null : reader["IssuedUsing"].ToString(),
                IssuedOn = reader.GetDateTime(reader.GetOrdinal("IssuedOn")),
                IssuedTo = reader.GetString(reader.GetOrdinal("IssuedTo")),
                AcceptedOn = reader["AcceptedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("AcceptedOn")),
                FingerPrint = reader["FingerPrint"] == DBNull.Value ? null : reader["FingerPrint"].ToString(),
                AcceptKey = reader["AcceptKey"] == DBNull.Value ? null : reader["AcceptKey"].ToString(),
                Badge = new Badge { Id = reader.GetInt64(reader.GetOrdinal("BadgeId")) }
            });
        }

        return records;
    }
}