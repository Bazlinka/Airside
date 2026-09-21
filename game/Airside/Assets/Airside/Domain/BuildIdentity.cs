using System;
using System.Globalization;
using System.Text;

namespace Airside.Domain
{
    /// <summary>
    /// Git identity baked into a running game so two machines can be compared after a
    /// pull and a rebuild. Parsed from <c>key=value</c> text written by
    /// <c>scripts/stamp-build-identity.sh</c>. No file or Unity types.
    /// </summary>
    public sealed class BuildIdentity
    {
        public const string UnstampedLabel = "build unstamped";

        private BuildIdentity(
            bool isStamped,
            string commit,
            string commitFull,
            string branch,
            string subject,
            string committedAt,
            bool dirty,
            string stampedAt)
        {
            IsStamped = isStamped;
            Commit = commit ?? "";
            CommitFull = commitFull ?? "";
            Branch = branch ?? "";
            Subject = subject ?? "";
            CommittedAt = committedAt ?? "";
            Dirty = dirty;
            StampedAt = stampedAt ?? "";
        }

        public static BuildIdentity Unstamped { get; } = new BuildIdentity(
            false, "", "", "", "", "", false, "");

        public bool IsStamped { get; }
        public string Commit { get; }
        public string CommitFull { get; }
        public string Branch { get; }
        public string Subject { get; }
        public string CommittedAt { get; }
        public bool Dirty { get; }
        public string StampedAt { get; }

        /// <summary>Corner label: <c>927634e1 · main</c>, plus <c>local changes</c> when the tree was dirty.</summary>
        public string ShortLabel
        {
            get
            {
                if (!IsStamped)
                    return UnstampedLabel;

                var label = string.IsNullOrEmpty(Branch) ? Commit : Commit + " · " + Branch;
                return Dirty ? label + " · local changes" : label;
            }
        }

        /// <summary>Pause-menu line: short label plus commit time and when this copy was stamped.</summary>
        public string FullLabel
        {
            get
            {
                if (!IsStamped)
                    return UnstampedLabel;

                var text = new StringBuilder(ShortLabel);
                AppendWhen(text, "committed", CommittedAt);
                AppendWhen(text, "built", StampedAt);
                return text.ToString();
            }
        }

        public static BuildIdentity Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Unstamped;

            string commit = null;
            string commitFull = null;
            string branch = null;
            string subject = null;
            string committedAt = null;
            string stampedAt = null;
            bool dirty = false;

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                var split = line.IndexOf('=');
                if (split <= 0)
                    continue;
                var key = line.Substring(0, split).Trim();
                var value = line.Substring(split + 1).Trim();
                switch (key)
                {
                    case "commit": commit = value; break;
                    case "commitFull": commitFull = value; break;
                    case "branch": branch = value; break;
                    case "subject": subject = value; break;
                    case "committedAt": committedAt = value; break;
                    case "stampedAt": stampedAt = value; break;
                    case "dirty":
                        dirty = value.Equals("true", StringComparison.OrdinalIgnoreCase)
                            || value == "1";
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(commit))
                return Unstamped;

            return new BuildIdentity(true, commit, commitFull, branch, subject, committedAt, dirty, stampedAt);
        }

        /// <summary>Canonical file body. The stamp script writes the same keys in this order.</summary>
        public string ToFileText()
        {
            var text = new StringBuilder();
            text.Append("commit=").Append(Commit).Append('\n');
            text.Append("commitFull=").Append(CommitFull).Append('\n');
            text.Append("branch=").Append(Branch).Append('\n');
            text.Append("subject=").Append(Subject).Append('\n');
            text.Append("committedAt=").Append(CommittedAt).Append('\n');
            text.Append("dirty=").Append(Dirty ? "true" : "false").Append('\n');
            text.Append("stampedAt=").Append(StampedAt).Append('\n');
            return text.ToString();
        }

        private static void AppendWhen(StringBuilder text, string label, string iso)
        {
            var when = FormatUtc(iso);
            if (string.IsNullOrEmpty(when))
                return;
            text.Append(" · ").Append(label).Append(' ').Append(when);
        }

        /// <summary>UTC clock time, so two Macs show the same instant. Unparseable text is kept as written.</summary>
        public static string FormatUtc(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso))
                return "";
            if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                return parsed.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
            return iso.Trim();
        }
    }
}
