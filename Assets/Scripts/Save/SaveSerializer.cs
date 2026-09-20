using System;
using System.Text;
using UnityEngine;

namespace Game.Save
{
    /// <summary>Why a save file was rejected. Reported to the player as one sentence and logged in full.</summary>
    public enum SaveValidationResult
    {
        Valid,
        Empty,
        Unreadable,
        ChecksumMismatch,
        UnknownVersion,
        Implausible
    }

    /// <summary>
    /// Turns <see cref="SaveData"/> into the text on disk and back, and decides whether
    /// what came back is trustworthy (SPEC.md section 32).
    ///
    /// The file is an envelope — version, checksum, payload — rather than the payload
    /// alone, so a truncated or half-written file is detectable without parsing game
    /// data out of it. The checksum is FNV-1a: this guards against corruption, which is
    /// what the spec asks for. It is not a signature and does not try to stop a player
    /// editing their own save.
    /// </summary>
    public static class SaveSerializer
    {
        [Serializable]
        private class SaveEnvelope
        {
            public int Version;
            public string Checksum;
            public string Payload;
        }

        public static string ToJson(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var payload = JsonUtility.ToJson(data, true);
            var envelope = new SaveEnvelope
            {
                Version = data.Version,
                Checksum = Checksum(payload),
                Payload = payload
            };

            return JsonUtility.ToJson(envelope, true);
        }

        /// <summary>
        /// Reads a save file. Returns the reason on failure rather than throwing,
        /// because every caller has to handle failure anyway — SPEC.md section 32 says
        /// a bad save is restored from backup, never overwritten and never deleted.
        /// </summary>
        public static SaveValidationResult TryFromJson(string text, out SaveData data, out string detail)
        {
            data = null;
            detail = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                detail = "the file was empty";
                return SaveValidationResult.Empty;
            }

            SaveEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<SaveEnvelope>(text);
            }
            catch (Exception exception)
            {
                detail = $"the envelope did not parse: {exception.Message}";
                return SaveValidationResult.Unreadable;
            }

            if (envelope == null || string.IsNullOrEmpty(envelope.Payload))
            {
                detail = "the envelope carried no payload";
                return SaveValidationResult.Unreadable;
            }

            var expected = Checksum(envelope.Payload);
            if (!string.Equals(expected, envelope.Checksum, StringComparison.Ordinal))
            {
                detail = $"checksum {envelope.Checksum} does not match the payload's {expected}";
                return SaveValidationResult.ChecksumMismatch;
            }

            SaveData parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveData>(envelope.Payload);
            }
            catch (Exception exception)
            {
                detail = $"the payload did not parse: {exception.Message}";
                return SaveValidationResult.Unreadable;
            }

            if (parsed == null)
            {
                detail = "the payload parsed to nothing";
                return SaveValidationResult.Unreadable;
            }

            if (parsed.Version <= 0 || parsed.Version > SaveData.CurrentVersion)
            {
                // A save from a newer build. Refusing it is the safe half of SPEC.md
                // section 32: the player keeps the file, they just cannot load it here.
                detail = $"save version {parsed.Version} is not supported by this build " +
                         $"(newest known is {SaveData.CurrentVersion})";
                return SaveValidationResult.UnknownVersion;
            }

            if (!SaveMigration.TryMigrate(parsed, out var migrationDetail))
            {
                detail = migrationDetail;
                return SaveValidationResult.UnknownVersion;
            }

            var plausibility = Plausibility(parsed);
            if (plausibility != null)
            {
                detail = plausibility;
                return SaveValidationResult.Implausible;
            }

            data = parsed;
            return SaveValidationResult.Valid;
        }

        /// <summary>
        /// Catches a file that parses but cannot describe a real game — the shape of
        /// corruption that a checksum over rewritten data would not catch. Kept
        /// deliberately loose: the job is to reject nonsense, not to police content.
        /// </summary>
        private static string Plausibility(SaveData data)
        {
            if (data.PlayerStats == null)
            {
                return "the save has no player stats";
            }

            if (data.PlayerStats.MaxHealth <= 0f)
            {
                return $"max health is {data.PlayerStats.MaxHealth}";
            }

            if (data.PlayerStats.Health < 0f || data.PlayerStats.Health > data.PlayerStats.MaxHealth)
            {
                return $"health {data.PlayerStats.Health} is outside 0..{data.PlayerStats.MaxHealth}";
            }

            var position = data.PlayerPosition;
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)
                || float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                return $"the player position is {position}";
            }

            if (data.MemoryIntegrity < 0f || data.MemoryIntegrity > 1f)
            {
                return $"memory integrity is {data.MemoryIntegrity}, outside 0..1";
            }

            return null;
        }

        /// <summary>FNV-1a over the UTF-8 payload, as 16 lowercase hex digits.</summary>
        public static string Checksum(string payload)
        {
            const ulong offsetBasis = 14695981039346656037;
            const ulong prime = 1099511628211;

            var hash = offsetBasis;
            var bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);

            for (var i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }

            return hash.ToString("x16");
        }
    }
}
